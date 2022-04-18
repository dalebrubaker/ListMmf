using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace BruSoftware.ListMmf;

public unsafe class ListMmfBitArray : ListMmfBase<int>, IListMmf<bool>, IReadOnlyList64Mmf<bool>
{
    /// <summary>
    ///     This is the size of the header used by this class
    /// </summary>
    private const int MyHeaderBytes = 8;

    /// <summary>
    ///     XPerY=n means that n X's can be stored in 1 Y.
    /// </summary>
    private const int BitsPerInt32 = 32;

    /// <summary>
    ///     The long* into the BitArrayLength location in the view, which is the first location in the view
    ///     This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
    ///     I don't know why they do that, and I don't seem to need it...
    /// </summary>
    private long* _ptrBitArrayLength;

    /// <summary>
    ///     Open a Writer on path
    ///     Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="capacity">
    ///     The number of bits to initialize the list.
    ///     If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    /// <param name="access">Must be either Read or ReadWrite</param>
    /// <param name="parentHeaderBytes"></param>
    private ListMmfBitArray(string path, long capacity = 0, MemoryMappedFileAccess access = MemoryMappedFileAccess.Read, long parentHeaderBytes = 0)
        : base(path, capacity == 0 ? 0 : GetArrayLength(capacity), access, parentHeaderBytes + MyHeaderBytes)
    {
        ResetView();
        if (capacity > Length)
        {
            Length = capacity;
        }
        if (access == MemoryMappedFileAccess.ReadWrite)
        {
            Version = 0;
            base.DataType = DataType.Bit;
        }
    }

    /// <summary>
    ///     ListMmfBitArray
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="access">Must be either Read or ReadWrite</param>
    /// <param name="capacity">
    ///     The number of bits to initialize the list.
    ///     If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    public ListMmfBitArray(string path, long capacity = 0, MemoryMappedFileAccess access = MemoryMappedFileAccess.Read)
        : this(path, capacity, access, 0)
    {
    }

    /// <summary>
    ///     This is the length of the Bit Array, about 32 times the Count of the underlying int32s
    /// </summary>
    public long Length
    {
        get
        {
            lock (SyncRoot)
            {
                return Unsafe.Read<long>(_ptrBitArrayLength);
            }
        }
        set
        {
            lock (SyncRoot)
            {
                if (IsReadOnly)
                {
                    throw new ListMmfException($"{nameof(Add)} cannot be done on this Read-Only list.");
                }
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
                if (value > Length)
                {
                    // clear high bit values in the last int
                    var last = GetArrayLength(Length) - 1;
                    var bits = (int)(Length % 32);
                    if (bits > 0)
                    {
                        var newValue = UnsafeReadNoLock(last);
                        newValue &= (1 << bits) - 1;
                        UnsafeWriteNoLock(last, newValue);
                    }

                    // clear remaining int values
                    for (var i = 0; i < newInts - last - 1; i++)
                    {
                        var index = i + last + 1;
                        UnsafeWriteNoLock(index, 0);
                    }
                }
                Unsafe.Write(_ptrBitArrayLength, value);
            }
        }
    }

    // hide ListMmf.Capacity
    public new long Capacity
    {
        get
        {
            lock (SyncRoot)
            {
                return base.Capacity * BitsPerInt32;
            }
        }
        set
        {
            lock (SyncRoot)
            {
                base.Capacity = GetArrayLength(value);
            }
        }
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
        lock (SyncRoot)
        {
            Length++;
            this[Length - 1] = value;
        }
    }

    public void AddRange(IEnumerable<bool> collection)
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
    }

    public void Truncate(long newLength)
    {
        lock (SyncRoot)
        {
            if (IsReadOnly || newLength >= Length)
            {
                // nothing to do
                return;
            }
            if (newLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(newLength), "ArgumentOutOfRange value must be >= 0");
            }
            var newInt32s = GetArrayLength(newLength);
            if (newInt32s < Capacity)
            {
                // Change Length first so readers won't use a wrong value
                Length = newLength;
                ResetCapacity(Count);
            }
        }
    }

    public void SetLast(bool value)
    {
        Set(Count - 1, value);
    }

    // Disallow the use of the public member in the base
    public IEnumerator<bool> GetEnumerator()
    {
        lock (SyncRoot)
        {
            return new Enumerator(this);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    ///     This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
    ///     e.g. are iterating (e.g. in a for loop)
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public new bool ReadUnchecked(long index)
    {
        var value = base.ReadUnchecked(index / 32);
        return (value & (1 << (int)(index % 32))) != 0;
    }

    /// <summary>
    ///     Returns the bit value at position index.
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">if index &lt; 0 or index &gt;= Length</exception>
    public bool Get(long index)
    {
        lock (SyncRoot)
        {
            // Following trick can reduce the range check by one
            if ((ulong)index >= (ulong)Length)
            {
                var msg = $"index={index:N0} but Length={Length:N0}";
                throw new ArgumentOutOfRangeException(nameof(index), $"ArgumentOutOfRange_Index {msg}");
            }
            var value = UnsafeReadNoLock(index / 32);
            var result = (value & (1 << (int)(index % 32))) != 0;
            return result;
        }
    }

    /// <summary>
    ///     Sets the bit value at position index to value.
    /// </summary>
    /// <param name="index"></param>
    /// <param name="value"></param>
    /// <exception cref="ArgumentOutOfRangeException">if index &lt; 0 or index &gt;= Length</exception>
    public void Set(long index, bool value)
    {
        if (index < 0 || index >= Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), "ArgumentOutOfRange_Index");
        }
        lock (SyncRoot)
        {
            var newValue = UnsafeReadNoLock(index / 32);
            if (value)
            {
                newValue |= 1 << (int)(index % 32);
            }
            else
            {
                newValue &= ~(1 << (int)(index % 32));
            }
            UnsafeWriteNoLock(index / 32, newValue);
        }
    }

    /// <summary>
    ///     Return the number of set bits in this bitArray
    ///     https://stackoverflow.com/questions/5063178/counting-bits-set-in-a-net-bitarray-class
    /// </summary>
    /// <returns></returns>
    public long GetCardinality()
    {
        lock (SyncRoot)
        {
            var count = 0L;
            var arrayCount = GetArrayLength(Length); // ignore extra array values
            for (long i = 0; i < arrayCount; i++)
            {
                var c = UnsafeReadNoLock(i);

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
    }

    /// <summary>
    ///     Returns a reference to the current instance ANDed with value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">if value == null or value.Length != this.Length</exception>
    public ListMmfBitArray And(ListMmfBitArray value)
    {
        lock (SyncRoot)
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
                var newValue = UnsafeReadNoLock(i);
                newValue &= value.UnsafeReadNoLock(i);
                UnsafeWriteNoLock(i, newValue);
            }
            return this;
        }
    }

    /// <summary>
    ///     Returns a reference to the current instance ORed with value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">if value == null or value.Length != this.Length</exception>
    public ListMmfBitArray Or(ListMmfBitArray value)
    {
        lock (SyncRoot)
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
                var newValue = UnsafeReadNoLock(i);
                newValue |= value.UnsafeReadNoLock(i);
                UnsafeWriteNoLock(i, newValue);
            }
            return this;
        }
    }

    /// <summary>
    ///     Returns a reference to the current instance XORed with value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException">if value == null or value.Length != this.Length</exception>
    public ListMmfBitArray Xor(ListMmfBitArray value)
    {
        lock (SyncRoot)
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
                var newValue = UnsafeReadNoLock(i);
                newValue ^= value.UnsafeReadNoLock(i);
                UnsafeWriteNoLock(i, newValue);
            }
            return this;
        }
    }

    public ListMmfBitArray Not()
    {
        lock (SyncRoot)
        {
            var newCountInt32 = GetArrayLength(Length);
            for (var i = 0; i < newCountInt32; i++)
            {
                var newValue = UnsafeReadNoLock(i);
                newValue = ~newValue;
                UnsafeWriteNoLock(i, newValue);
            }
            return this;
        }
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

    protected override int ResetPointers()
    {
        lock (SyncRoot)
        {
            var countBaseBytes = base.ResetPointers();

            _ptrBitArrayLength = (long*)(_basePointerView + countBaseBytes);
            return countBaseBytes + MyHeaderBytes;
        }
    }

    /// <summary>
    ///     Return the number of int32s required to hold length of n
    /// </summary>
    /// <param name="n"></param>
    /// <returns></returns>
    private static long GetArrayLength(long n)
    {
        return n > 0 ? (n - 1) / BitsPerInt32 + 1 : 0;
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