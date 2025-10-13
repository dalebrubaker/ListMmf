using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace BruSoftware.ListMmf;

public unsafe class ListMmfBitArray : ListMmfBase<int>, IListMmf<bool>, IReadOnlyList64Mmf<bool>
{
    /// <summary>
    /// This is the size of the header used by this class
    /// </summary>
    private const int MyHeaderBytes = 8;

    /// <summary>
    /// XPerY=n means that n X's can be stored in 1 Y.
    /// </summary>
    private const int BitsPerInt32 = 32;

    /// <summary>
    /// The long* into the BitArrayLength location in the view, which is the first location in the view
    /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
    /// I don't know why they do that, and I don't seem to need it...
    /// </summary>
    private long* _ptrBitArrayLength;

    /// <summary>
    /// Open a Writer on path
    /// Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="capacity">
    /// The number of bits to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    /// <param name="parentHeaderBytes"></param>
    private ListMmfBitArray(string path, long capacity = 0, long parentHeaderBytes = 0)
        : base(path, capacity == 0 ? 0 : GetArrayLength(capacity), parentHeaderBytes + MyHeaderBytes)
    {
        ResetView();
        Version = 0;
        base.DataType = DataType.Bit;
    }

    /// <summary>
    /// ListMmfBitArray
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="access">Must be either Read or ReadWrite</param>
    /// <param name="capacity">
    /// The number of bits to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    public ListMmfBitArray(string path, long capacity = 0)
        : this(path, capacity, 0)
    {
    }

    /// <summary>
    /// This is the length of the Bit Array, one more than the highest index that has been set
    /// </summary>
    public long Length
    {
        get => Unsafe.Read<long>(_ptrBitArrayLength);
        private set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "ArgumentOutOfRange value must be >= 0");
            }
            var newInts = GetArrayLength(value);
            if (newInts > base.Count)
            {
                // Extend with zeros
                if (newInts > base.Capacity)
                {
                    base.Capacity = newInts;
                }
                base.Count = newInts;
            }
            Unsafe.Write(_ptrBitArrayLength, value);
        }
    }

    // hide ListMmf.Capacity
    public new long Capacity
    {
        get => base.Capacity * BitsPerInt32;
        set => base.Capacity = GetArrayLength(value);
    }

    public new int WidthBits => 1;

    public new DataType DataType => DataType.Bit;

    // hide ListMmf.Count
    public new long Count => Length;

    long IListMmf.Capacity
    {
        get => Capacity;
        set => Capacity = value;
    }

    public bool this[long index]
    {
        get => Get(index);
        set => Set(index, value);
    }

    public void Add(bool value)
    {
        Length++;
        this[Length - 1] = value;
    }

    public void AddRange(IEnumerable<bool> collection)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        var currentLength = Length;
        switch (collection)
        {
            case IReadOnlyList64<bool> list:
                if (currentLength + list.Count > Capacity)
                {
                    var newCount = GetArrayLength(currentLength + list.Count);
                    GrowCapacity(newCount);
                }
                for (var i = 0; i < list.Count; i++)
                {
                    Set(currentLength++, list[i]);
                }
                break;
            case IList<bool> list:
                if (currentLength + list.Count > Capacity)
                {
                    var newCount = GetArrayLength(currentLength + list.Count);
                    GrowCapacity(newCount);
                }
                for (var i = 0; i < list.Count; i++)
                {
                    Add(list[i]);
                }
                break;
            case ICollection<bool> c:
                if (currentLength + c.Count > Capacity)
                {
                    var newCount = GetArrayLength(currentLength + c.Count);
                    GrowCapacity(newCount);
                }
                foreach (var item in collection)
                {
                    Add(item);
                }
                break;
            default:
                using (var en = collection.GetEnumerator())
                {
                    while (en.MoveNext())
                    {
                        var newCount = GetArrayLength(currentLength + 1);
                        if (newCount > Capacity)
                        {
                            GrowCapacity(newCount);
                        }
                        Add(en.Current);
                    }
                }
                return;
        }

        // Set Length last so readers won't access items before they are written
        Length = currentLength;
    }

    public new void Truncate(long newCount)
    {
        if (newCount >= Length)
        {
            // nothing to do
            return;
        }
        if (newCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(newCount), "ArgumentOutOfRange value must be >= 0");
        }
        if (Path.Contains("Directions"))
        {
        }
        var newInt32s = GetArrayLength(newCount);
        if (newInt32s < Capacity)
        {
            // Change Length first so readers won't use a wrong value
            Length = newCount;
            base.Truncate(newInt32s);
        }
    }

    public new void TruncateBeginning(long newCount, IProgress<long>? progress = null)
    {
        var count = Length;
        if (newCount > count)
        {
            throw new ListMmfException($"TruncateBeginning {newCount} must not be greater than Count={count}");
        }

        if (newCount == count)
        {
            return; // No-op optimization
        }

        var beginIndex = count - newCount;
        var chunkSize = Math.Max(1, newCount / 100); // 1% chunks for progress reporting
        var bitsProcessed = 0L;

        // Process in chunks for progress reporting
        while (bitsProcessed < newCount)
        {
            var remainingBits = newCount - bitsProcessed;
            var currentChunkSize = Math.Min(chunkSize, remainingBits);

            // Process this chunk of bits
            for (var i = 0L; i < currentChunkSize; i++)
            {
                var sourceBitIndex = beginIndex + bitsProcessed + i;
                var destBitIndex = bitsProcessed + i;
                var value = Get(sourceBitIndex);
                Set(destBitIndex, value);
            }

            bitsProcessed += currentChunkSize;

            // Report progress
            progress?.Report(bitsProcessed);
        }

        // Clear any remaining bits in the last Int32 that weren't overwritten
        // This prevents wrong values from leftover bits in the highest underlying Int32
        var lastValidBitIndex = newCount - 1;
        var lastInt32Index = lastValidBitIndex / 32;
        var bitsInLastInt32 = (int)(lastValidBitIndex % 32) + 1;

        // Clear bits beyond newCount in the last Int32 to prevent garbage values
        if (bitsInLastInt32 < 32)
        {
            var lastInt32 = UnsafeRead(lastInt32Index);
            var mask = (1u << bitsInLastInt32) - 1; // Create mask for valid bits
            lastInt32 &= (int)mask; // Clear high bits
            UnsafeWrite(lastInt32Index, lastInt32);
        }

        // Change Length first so readers won't use a wrong value
        Length = newCount;
        ResetCapacity(newCount);
    }

    public void SetLast(bool value)
    {
        Set(Count - 1, value);
    }

    // Disallow the use of the public member in the base
    public IEnumerator<bool> GetEnumerator()
    {
        return new Enumerator(this);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
    /// e.g. are iterating (e.g. in a for loop)
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public new bool ReadUnchecked(long index)
    {
        var value = base.ReadUnchecked(index / 32);
        return (value & (1 << (int)(index % 32))) != 0;
    }

    /// <summary>
    /// Returns the bit value at position index.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">if index &lt; 0 or index &gt;= Length</exception>
    public bool Get(long index)
    {
        // Following trick can reduce the range check by one
        if ((ulong)index >= (ulong)Length)
        {
            var msg = $"index={index:N0} but Length={Length:N0}";
            throw new ArgumentOutOfRangeException(nameof(index), $"ArgumentOutOfRange_Index {msg}");
        }
        var value = UnsafeRead(index / 32);
        var result = (value & (1 << (int)(index % 32))) != 0;
        return result;
    }

    /// <summary>
    /// Sets the bit value at position index to value.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentOutOfRangeException">if index &lt; 0 or index &gt;= Length</exception>
    public void Set(long index, bool value)
    {
        if (index + 1 > Length)
        {
            // Increase Length to include the highest value that has been Set()
            Length = index + 1;
        }
        if (index < 0 || index >= Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "ArgumentOutOfRange_Index");
        }
        var newValue = UnsafeRead(index / 32);
        if (value)
        {
            newValue |= 1 << (int)(index % 32);
        }
        else
        {
            newValue &= ~(1 << (int)(index % 32));
        }
        UnsafeWrite(index / 32, newValue);
    }

    /// <summary>
    /// Return the number of set bits in this bitArray
    /// https://stackoverflow.com/questions/5063178/counting-bits-set-in-a-net-bitarray-class
    /// </summary>
    /// <returns></returns>
    public long GetCardinality()
    {
        var count = 0L;
        var arrayCount = GetArrayLength(Length); // ignore extra array values
        for (long i = 0; i < arrayCount; i++)
        {
            var c = UnsafeRead(i);

            // magic (http://graphics.stanford.edu/~seander/bithacks.html#CountBitsSetParallel)
            unchecked
            {
                c -= (c >> 1) & 0x55555555;
                c = (c & 0x33333333) + ((c >> 2) & 0x33333333);
                c = (((c + (c >> 4)) & 0xF0F0F0F) * 0x1010101) >> 24;
            }
            count += c;
        }
        return count;
    }

    /// <summary>
    /// Returns a reference to the current instance ANDed with value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">if value == null or value.Length != this.Length</exception>
    public ListMmfBitArray And(ListMmfBitArray value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        if (Length != value.Length)
        {
            throw new ArgumentException("Array Lengths Differ");
        }
        var newCountInt32 = GetArrayLength(Length);
        for (long i = 0; i < newCountInt32; i++)
        {
            var newValue = UnsafeRead(i);
            newValue &= value.UnsafeRead(i);
            UnsafeWrite(i, newValue);
        }
        return this;
    }

    /// <summary>
    /// Returns a reference to the current instance ORed with value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">if value == null or value.Length != this.Length</exception>
    public ListMmfBitArray Or(ListMmfBitArray value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        if (Length != value.Length)
        {
            throw new ArgumentException("Arg_ArrayLengthsDiffer");
        }
        var newCountInt32 = GetArrayLength(Length);
        for (var i = 0; i < newCountInt32; i++)
        {
            var newValue = UnsafeRead(i);
            newValue |= value.UnsafeRead(i);
            UnsafeWrite(i, newValue);
        }
        return this;
    }

    /// <summary>
    /// Returns a reference to the current instance XORed with value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">if value == null or value.Length != this.Length</exception>
    public ListMmfBitArray Xor(ListMmfBitArray value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        if (Length != value.Length)
        {
            throw new ArgumentException("Array Lengths Differ");
        }
        var newCountInt32 = GetArrayLength(Length);
        for (var i = 0; i < newCountInt32; i++)
        {
            var newValue = UnsafeRead(i);
            newValue ^= value.UnsafeRead(i);
            UnsafeWrite(i, newValue);
        }
        return this;
    }

    public ListMmfBitArray Not()
    {
        var newCountInt32 = GetArrayLength(Length);
        for (var i = 0; i < newCountInt32; i++)
        {
            var newValue = UnsafeRead(i);
            newValue = ~newValue;
            UnsafeWrite(i, newValue);
        }
        return this;
    }

    // Disallow the use of the public member in the base
    public int[] ToArray()
    {
        throw new NotSupportedException(nameof(ToArray));
    }

    // Disallow the use of the public member in the base
    public List<int> ToList()
    {
        throw new NotSupportedException(nameof(ToList));
    }

    /// <summary>
    /// Returns a Span&lt;bool&gt; representing a range of bits from the BitArray.
    /// Creates a new bool array since the underlying storage is bit-packed.
    /// </summary>
    /// <param name="start">The starting bit index (inclusive)</param>
    /// <param name="length">The number of bits to include in the span</param>
    /// <returns>A Span&lt;bool&gt; representing the requested range</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start or length is invalid</exception>
    /// <exception cref="ListMmfOnlyInt32SupportedException">If length exceeds int.MaxValue</exception>
    public new Span<bool> AsSpan(long start, int length)
    {
        // Bounds validation
        var count = Length;
        if (start < 0 || start > count)
        {
            throw new ArgumentOutOfRangeException(nameof(start), $"start={start} must be >= 0 and <= Length={count}");
        }
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "length must be >= 0");
        }
        if (start + length > count)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"start + length ({start + length}) exceeds Length={count}");
        }

        // Create bool array using existing Get() method
        var result = new bool[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = Get(start + i);
        }
        return result.AsSpan();
    }

    /// <summary>
    /// Returns a Span&lt;bool&gt; representing bits from the start index to the end of the BitArray.
    /// Creates a new bool array since the underlying storage is bit-packed.
    /// </summary>
    /// <param name="start">The starting bit index (inclusive)</param>
    /// <returns>A Span&lt;bool&gt; representing bits from start to the end</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start is invalid</exception>
    /// <exception cref="ListMmfOnlyInt32SupportedException">If the resulting length exceeds int.MaxValue</exception>
    public new Span<bool> AsSpan(long start)
    {
        var count = Length;
        var length = count - start;
        if (length > int.MaxValue)
        {
            throw new ListMmfOnlyInt32SupportedException(length);
        }

        return AsSpan(start, (int)length);
    }

    public new Span<bool> GetRange(long start, int length)
    {
        return AsSpan(start, length);
    }

    public new Span<bool> GetRange(long start)
    {
        return AsSpan(start);
    }

    // Explicit interface implementations for ReadOnlySpan return type
    ReadOnlySpan<bool> IReadOnlyList64Mmf<bool>.GetRange(long start, int length)
    {
        return AsSpan(start, length);
    }

    ReadOnlySpan<bool> IReadOnlyList64Mmf<bool>.GetRange(long start)
    {
        return AsSpan(start);
    }

    ReadOnlySpan<bool> IReadOnlyList64Mmf<bool>.AsSpan(long start, int length)
    {
        return AsSpan(start, length);
    }

    ReadOnlySpan<bool> IReadOnlyList64Mmf<bool>.AsSpan(long start)
    {
        return AsSpan(start);
    }

    /// <summary>
    /// Appends the elements of a ReadOnlySpan&lt;bool&gt; to the end of the BitArray.
    /// Uses the existing Set() method for each bit.
    /// </summary>
    /// <param name="span">The span containing bool elements to append</param>
    /// <exception cref="ArgumentException">If the span would cause the array to exceed capacity limits</exception>
    public void AddRange(ReadOnlySpan<bool> span)
    {
        if (span.IsEmpty)
        {
            return;
        }

        var currentLength = Length;
        var newLength = currentLength + span.Length;

        // Ensure capacity (similar to existing AddRange logic)
        if (newLength > Capacity)
        {
            var newCount = GetArrayLength(newLength);
            GrowCapacity(newCount);
        }

        // Append bits using existing Set() method
        for (var i = 0; i < span.Length; i++)
        {
            Set(currentLength + i, span[i]);
        }

        // Set Length last so readers won't access items before they are written
        Length = newLength;
    }

    protected override int ResetPointers()
    {
        var countBaseBytes = base.ResetPointers();

        _ptrBitArrayLength = (long*)(_basePointerView + countBaseBytes);
        return countBaseBytes + MyHeaderBytes;
    }

    /// <summary>
    /// Return the number of int32s required to hold length of n
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    private static long GetArrayLength(long n)
    {
        return n > 0 ? (n - 1) / BitsPerInt32 + 1 : 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            TrimExcess();
            base.Dispose(true);
        }
    }
}

public struct Enumerator : IEnumerator<bool>
{
    private readonly ListMmfBitArray _list;
    private long _index;

    internal Enumerator(ListMmfBitArray list)
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

    public bool Current { get; private set; }

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