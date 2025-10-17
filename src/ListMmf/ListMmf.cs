using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace BruSoftware.ListMmf;

/// <summary>
/// Provides a memory-mapped implementation of <see cref="IListMmf{T}"/> for unmanaged value types.
/// Data is persisted to disk and can be shared between processes with zero-copy access via spans.
/// </summary>
/// <typeparam name="T">The unmanaged value type stored in the list (e.g., int, long, double, or custom structs).</typeparam>
/// <remarks>
/// <para><strong>Type Safety and Overflow Protection:</strong></para>
/// <para>
/// ListMmf stores values using the exact type T specified. Unlike SmallestInt64ListMmf, it does NOT automatically
/// upgrade storage when values exceed the type's range. Instead, overflow protection occurs at the caller's cast site:
/// </para>
/// <list type="bullet">
/// <item><description>Use <c>checked</c> casts to throw OverflowException on overflow: <c>list.Add(checked((short)value))</c></description></item>
/// <item><description>Avoid <c>unchecked</c> casts which silently truncate and corrupt data</description></item>
/// <item><description>Choose appropriately-sized types upfront (prefer int/long over short/byte for production data)</description></item>
/// </list>
/// <para><strong>Best Practices:</strong></para>
/// <list type="number">
/// <item><description><strong>Production Data:</strong> Use Int32 (±2.1B) or Int64 (±9.2E+18) to avoid overflow risks</description></item>
/// <item><description><strong>Python Interop:</strong> Standard types (int, long, float, double) enable zero-copy via numpy.memmap</description></item>
/// <item><description><strong>Handle OverflowException:</strong> Catch and log when unexpected data ranges occur</description></item>
/// <item><description><strong>Thread Safety:</strong> Single writer, multiple readers supported (lock-free for ≤8 byte types)</description></item>
/// </list>
/// <para><strong>Performance:</strong></para>
/// <para>
/// Memory-mapped files provide near-memory-speed access for cached pages, automatic OS paging for large datasets,
/// and zero-copy data sharing between processes. Use AsSpan() for bulk operations without allocations.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Create a price list (Int32 supports up to $21M per share if storing cents)
/// using var prices = new ListMmf&lt;int&gt;("prices.mmf", DataType.Int32);
///
/// // Add values safely
/// prices.Add(10050);  // $100.50 in cents
///
/// // Handle realtime data with overflow protection
/// int realtimePrice = GetPriceFromFeed();
/// try
/// {
///     prices.Add(realtimePrice);
/// }
/// catch (OverflowException)
/// {
///     Logger.Error($"Price {realtimePrice} exceeds Int32 range");
///     // Handle gracefully - alert operators, use fallback, etc.
/// }
///
/// // Zero-copy bulk access
/// ReadOnlySpan&lt;int&gt; lastHour = prices.AsSpan(prices.Count - 3600, 3600);
/// int average = CalculateAverage(lastHour);
///
/// // Share with another process
/// using var reader = new ListMmf&lt;int&gt;("prices.mmf", DataType.Int32);
/// Console.WriteLine($"Shared data: {reader.Count} prices");
/// </code>
/// </example>
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
    /// <param name="logger">Optional logger</param>
    /// <param name="isReadOnly">If true, opens in read-only mode with no locks and FileShare.ReadWrite</param>
    protected ListMmf(string path, long capacityItems, DataType dataType, long parentHeaderBytes, Microsoft.Extensions.Logging.ILogger? logger = null, bool isReadOnly = false)
        : base(path, capacityItems, parentHeaderBytes + MyHeaderBytes, logger, isReadOnly)
    {
        ResetView();
        if (!isReadOnly)
        {
            Version = 0;
            DataType = dataType;
        }
    }

    /// <summary>
    /// Open the list in a MemoryMappedFile at path as the exclusive Writer, or as a read-only Reader.
    /// Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite or Read</param>
    /// <param name="dataType"></param>
    /// <param name="capacityItems">
    /// The number of items to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// Ignored in read-only mode.
    /// </param>
    /// <param name="logger">Optional logger</param>
    /// <param name="isReadOnly">If true, opens in read-only mode with no locks and FileShare.ReadWrite</param>
    /// <exception cref="ArgumentException"></exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    /// <exception cref="IOException">Another Writer is open on this path</exception>
    /// <exception cref="ListMmfException"></exception>
    public ListMmf(string path, DataType dataType, long capacityItems = 0, Microsoft.Extensions.Logging.ILogger? logger = null, bool isReadOnly = false)
        : this(path, capacityItems, dataType, 0, logger, isReadOnly)
    {
        ResetView();
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void Add(T value)
    {
        if (IsReadOnly)
            throw new NotSupportedException("Cannot add items in read-only mode");

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
        if (IsReadOnly)
            throw new NotSupportedException("Cannot add items in read-only mode");

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
        if (IsReadOnly)
            throw new NotSupportedException("Cannot add items in read-only mode");

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
        if (IsReadOnly)
            throw new NotSupportedException("Cannot modify items in read-only mode");

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
