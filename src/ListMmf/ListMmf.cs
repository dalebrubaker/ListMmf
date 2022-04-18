using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace BruSoftware.ListMmf;

public class ListMmf<T> : ListMmfBase<T>, IReadOnlyList64Mmf<T>, IListMmf<T> where T : struct
{
    /// <summary>
    ///     This is the size of the header used by this class
    /// </summary>
    private const int MyHeaderBytes = 0;

    protected ListMmf(string path, long capacityItems, MemoryMappedFileAccess access, DataType dataType, long parentHeaderBytes)
        : base(path, capacityItems, access, parentHeaderBytes + MyHeaderBytes)
    {
        ResetView();
        if (access == MemoryMappedFileAccess.ReadWrite)
        {
            Version = 0;
            DataType = dataType;
        }
    }

    /// <summary>
    ///     Open the list in a MemoryMappedFile at path as the exclusive Writer.
    ///     Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="dataType"></param>
    /// <param name="capacityItems">
    ///     The number of items to initialize the list.
    ///     If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    /// <param name="access">Must be either Read or ReadWrite</param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="IOException">Another Writer is open on this path</exception>
    /// <exception cref="ListMmfException"></exception>
    public ListMmf(string path, DataType dataType, long capacityItems = 0, MemoryMappedFileAccess access = MemoryMappedFileAccess.Read)
        : this(path, capacityItems, access, dataType, 0)
    {
        ResetView();
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void Add(T value)
    {
        lock (SyncRoot)
        {
            Debug.Assert(!IsReadOnly);
            // if (IsReadOnly)
            // {
            //     throw new ListMmfException($"{nameof(Add)} cannot be done on this Read-Only list.");
            // }
            var count = UnsafeGetCount();
            if (count + 1 > _capacity)
            {
                GrowCapacity(count + 1);
            }
            // if (Path.Contains("Volumes"))
            // {
            // }
            UnsafeWriteNoLock(count, value);
            UnsafeSetCount(count + 1); // Change Count AFTER the value, so other processes will get correct
        }
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void AddRange(IEnumerable<T> collection)
    {
        lock (SyncRoot)
        {
            if (IsReadOnly)
            {
                throw new ListMmfException($"{nameof(AddRange)} cannot be done on this Read-Only list.");
            }
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }
            var currentCount = UnsafeGetCount();
            switch (collection)
            {
                case IReadOnlyList64<T> list:
                    if (currentCount + list.Count > _capacity)
                    {
                        GrowCapacity(currentCount + list.Count);
                    }
                    for (var i = 0; i < list.Count; i++)
                    {
                        UnsafeWriteNoLock(currentCount++, list[i]);
                    }
                    break;
                case IList<T> list:
                    if (currentCount + list.Count > _capacity)
                    {
                        GrowCapacity(currentCount + list.Count);
                    }
                    for (var i = 0; i < list.Count; i++)
                    {
                        UnsafeWriteNoLock(currentCount++, list[i]);
                    }
                    break;
                case ICollection<T> c:
                    if (currentCount + c.Count > _capacity)
                    {
                        GrowCapacity(currentCount + c.Count);
                    }
                    foreach (var item in collection)
                    {
                        UnsafeWriteNoLock(currentCount++, item);
                    }
                    break;
                default:
                    using (var en = collection.GetEnumerator())
                    {
                        // Do inline Add
                        Add(en.Current);
                        while (en.MoveNext())
                        {
                            if (currentCount + 1 > _capacity)
                            {
                                GrowCapacity(currentCount + 1);
                            }
                            UnsafeWriteNoLock(currentCount++, en.Current);
                        }
                    }
                    break;
            }

            // Set Count last so readers won't access items before they are written
            UnsafeSetCount(currentCount);
        }
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void Truncate(long newCapacityItems)
    {
        lock (SyncRoot)
        {
            var count = UnsafeGetCount();
            if (IsReadOnly || newCapacityItems >= count)
            {
                // nothing to do
                return;
            }
            if (newCapacityItems < 0)
            {
                throw new ArgumentException("Truncate new length cannot be negative");
            }

            // Change Count first so readers won't use a wrong value
            Count = newCapacityItems;
            ResetCapacity(newCapacityItems);
        }
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void SetLast(T value)
    {
        lock (SyncRoot)
        {
            var count = UnsafeGetCount();
            UnsafeWriteNoLock(count - 1, value);
        }
    }

    public T this[long index]
    {
        get
        {
            lock (SyncRoot)
            {
                // Following trick can reduce the range check by one
                var count = Count;
                if ((ulong)index >= (uint)count)
                {
                    var msg = $"index={index:N0} but maximum index is {count - 1:N0}";
                    // Perhaps the file was truncated. Allow the user to handle ListMmfTruncatedException
                    throw new ListMmfTruncatedException(msg);
                }
                // if (Path.Contains("Volumes"))
                // {
                // }
                var result = UnsafeReadNoLock(index);
                return result;
            }
        }
        // set
        // {
        //     lock (SyncRoot)
        //     {
        //         // Following trick can reduce the range check by one
        //         if ((ulong)index >= (uint)Count)
        //         {
        //             var msg = $"index={index:N0} but maximum index is {Count - 1:N0}";
        //             throw new ArgumentOutOfRangeException(nameof(index), Count, msg);
        //         }
        //         UnsafeWriteNoLock(index, value);
        //     }
        // }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<T> GetEnumerator()
    {
        lock (SyncRoot)
        {
            return new Enumerator(this);
        }
    }

    /// <summary>
    ///     ToArray returns a new Object array containing the contents of the List.
    ///     This requires copying the List, which is an O(n) operation.
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public T[] ToArray()
    {
        lock (SyncRoot)
        {
            var count = UnsafeGetCount();
            if (Count > int.MaxValue)
            {
                throw new NotSupportedException("ToArray() is not possible when Count is higher than int.MaxValue");
            }
            var result = new T[count];
            for (var i = 0; i < count; i++)
            {
                result[i] = UnsafeReadNoLock(i);
            }
            return result;
        }
    }

    /// <summary>
    ///     ToList returns a new List  containing the contents of the List.
    ///     This requires copying the List, which is an O(n) operation.
    ///     Not in the List API
    /// </summary>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public List<T> ToList()
    {
        lock (SyncRoot)
        {
            var count = UnsafeGetCount();
            if (count > int.MaxValue)
            {
                throw new NotSupportedException("ToArray() is not possible when Count is higher than int.MaxValue");
            }
            var result = new List<T>((int)count);
            for (var i = 0; i < count; i++)
            {
                result.Add(UnsafeReadNoLock(i));
            }
            return result;
        }
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