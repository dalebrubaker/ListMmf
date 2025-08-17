using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace BruSoftware.ListMmf;

/// <summary>
/// Provides a memory-mapped implementation of <see cref="IListMmf{T}"/> for unmanaged value types.
/// </summary>
/// <typeparam name="T">The value type stored in the list.</typeparam>
public unsafe class ListMmf<T> : ListMmfBase<T>, IReadOnlyList64Mmf<T>, IListMmf<T> where T : struct
{
    /// <summary>
    /// This is the size of the header used by this class
    /// </summary>
    private const int MyHeaderBytes = 0;

    /// <summary>
    /// Initializes a new instance for derived types.
    /// </summary>
    /// <param name="path">The file path backing the list.</param>
    /// <param name="capacityItems">The initial capacity in items.</param>
    /// <param name="dataType">The data type stored in the list.</param>
    /// <param name="parentHeaderBytes">Header bytes used by the parent class.</param>
    protected ListMmf(string path, long capacityItems, DataType dataType, long parentHeaderBytes)
        : base(path, capacityItems, parentHeaderBytes + MyHeaderBytes)
    {
        ResetView();
        Version = 0;
        DataType = dataType;
    }

    /// <summary>
    /// Open the list in a MemoryMappedFile at path as the exclusive Writer.
    /// Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="dataType"></param>
    /// <param name="capacityItems">
    /// The number of items to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="IOException">Another Writer is open on this path</exception>
    /// <exception cref="ListMmfException"></exception>
    public ListMmf(string path, DataType dataType, long capacityItems = 0)
        : this(path, capacityItems, dataType, 0)
    {
        ResetView();
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void Add(T value)
    {
        var count = Count;
        if (count + 1 > _capacity)
        {
            GrowCapacity(count + 1);
        }
        // if (Path.Contains("Volumes"))
        // {
        // }
        UnsafeWrite(count, value);
        Count = count + 1; // Change Count AFTER writing the value, so other processes will get correct
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void AddRange(IEnumerable<T> collection)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        var currentCount = Count;
        switch (collection)
        {
            case IReadOnlyList64<T> list:
                if (currentCount + list.Count > _capacity)
                {
                    GrowCapacity(currentCount + list.Count);
                }
                for (var i = 0; i < list.Count; i++)
                {
                    UnsafeWrite(currentCount++, list[i]);
                }
                break;
            case IList<T> list:
                if (currentCount + list.Count > _capacity)
                {
                    GrowCapacity(currentCount + list.Count);
                }
                for (var i = 0; i < list.Count; i++)
                {
                    UnsafeWrite(currentCount++, list[i]);
                }
                break;
            case ICollection<T> c:
                if (currentCount + c.Count > _capacity)
                {
                    GrowCapacity(currentCount + c.Count);
                }
                foreach (var item in collection)
                {
                    UnsafeWrite(currentCount++, item);
                }
                break;
            default:
                using (var en = collection.GetEnumerator())
                {
                    while (en.MoveNext())
                    {
                        if (currentCount + 1 > _capacity)
                        {
                            GrowCapacity(currentCount + 1);
                        }
                        UnsafeWrite(currentCount++, en.Current);
                    }
                }
                break;
        }

        // Set Count last so readers won't access items before they are written
        Count = currentCount;
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void AddRange(ReadOnlySpan<T> span)
    {
        if (span.IsEmpty)
        {
            return;
        }

        var count = Count;
        var newCount = count + span.Length;

        // Ensure capacity (using existing growth strategy)
        if (newCount > _capacity)
        {
            GrowCapacity(newCount);
        }

        // Bulk copy using span operations
        var targetSpan = new Span<T>(_ptrArray + count * _width, span.Length);
        span.CopyTo(targetSpan);

        // Update count last (append-only pattern - readers see consistent state)
        Count = newCount;
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void SetLast(T value)
    {
        var count = Count;
        UnsafeWrite(count - 1, value);
    }

    public T this[long index]
    {
        get
        {
            // Following trick can reduce the range check by one
            var count = Count;
            if ((ulong)index >= (uint)count)
            {
                var msg = $"index={index:N0} but maximum index is {count - 1:N0} for {Path}";
                // Perhaps the file was truncated. Allow the user to handle ListMmfTruncatedException
                throw new ListMmfTruncatedException(msg);
            }
            var result = UnsafeRead(index);
            return result;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return new Enumerator(this);
    }

    private struct Enumerator : IEnumerator<T>
    {
        private readonly ListMmf<T> _list;
        private long _index;

        internal Enumerator(ListMmf<T> list)
        {
            _list = list;
            _index = 0;
            Current = default;
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            var localList = _list;

            if ((ulong)_index < (ulong)localList.Count)
            {
                Current = localList[_index];
                _index++;
                return true;
            }
            return MoveNextRare();
        }

        private bool MoveNextRare()
        {
            _index = _list.Count + 1;
            Current = default;
            return false;
        }

        public T Current { get; private set; }

        object IEnumerator.Current
        {
            get
            {
                if (_index == 0 || _index == _list.Count + 1)
                {
                    throw new InvalidOperationException("Can't happen!");
                }
                return Current;
            }
        }

        void IEnumerator.Reset()
        {
            _index = 0;
            Current = default;
        }
    }
}