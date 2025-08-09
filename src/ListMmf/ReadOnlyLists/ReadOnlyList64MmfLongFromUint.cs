using System;
using System.Collections;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

/// <summary>
/// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
/// </summary>
// ReSharper disable once InconsistentNaming
public class ReadOnlyList64MmfLongFromUint : IReadOnlyList64Mmf<long>
{
    private readonly IReadOnlyList64Mmf<uint> _list;
    private readonly string _priceTypeName;

    public ReadOnlyList64MmfLongFromUint(IReadOnlyList64Mmf<uint> list, string priceTypeName)
    {
        _list = list;
        _priceTypeName = priceTypeName;
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<long> GetEnumerator()
    {
        return new Enumerator(this);
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    /// <returns></returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public long Count => _list.Count;

    /// <summary>
    /// Returns 0 (default(T)) if the index was Reset()
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public long this[long index]
    {
        get
        {
            var idx = _list[index];
            return idx;
        }
    }

    public long ReadUnchecked(long index)
    {
        return _list.ReadUnchecked(index);
    }

    public ReadOnlySpan<long> GetRange(long start, int length)
    {
        // Get the underlying data in bulk and convert
        var uintSpan = _list.GetRange(start, length);
        var result = new long[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = uintSpan[i];
        }
        return result;
    }

    public ReadOnlySpan<long> GetRange(long start)
    {
        var count = Count;
        if (start < 0 || start >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        var length = (int)(count - start);
        return GetRange(start, length);
    }

    public override string ToString()
    {
        return $"{_list.Count:N0} of {_priceTypeName}";
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    [Serializable]
    public struct Enumerator : IEnumerator<long>
    {
        [NonSerialized] private readonly ReadOnlyList64MmfLongFromUint _list;

        private long _index;

        internal Enumerator(ReadOnlyList64MmfLongFromUint list)
        {
            _list = list;
            _index = 0;
            Current = 0;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            var localList = _list;
            if ((uint)_index < (uint)localList.Count)
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
            Current = 0;
            return false;
        }

        public long Current { get; private set; }

        object IEnumerator.Current
        {
            get
            {
                if (_index == 0 || _index == _list.Count + 1)
                {
                    throw new InvalidOperationException("Enum Op Cant Happen");
                }
                return Current;
            }
        }

        void IEnumerator.Reset()
        {
            _index = 0;
            Current = 0;
        }
    }
}