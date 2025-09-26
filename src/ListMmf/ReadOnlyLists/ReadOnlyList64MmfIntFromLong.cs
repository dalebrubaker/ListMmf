using System;
using System.Collections;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

/// <summary>
/// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
/// </summary>
// ReSharper disable once InconsistentNaming
public class ReadOnlyList64MmfIntFromLong : IReadOnlyList64Mmf<int>
{
    private readonly IReadOnlyList64Mmf<long> _list;
    private readonly string _priceTypeName;

    public ReadOnlyList64MmfIntFromLong(IReadOnlyList64Mmf<long> list, string priceTypeName)
    {
        _list = list;
        _priceTypeName = priceTypeName;
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<int> GetEnumerator()
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
    public int this[long index]
    {
        get
        {
            var idx = _list[index];
            return (int)idx;
        }
    }

    public int ReadUnchecked(long index)
    {
        return (int)_list.ReadUnchecked(index);
    }

    public ReadOnlySpan<int> AsSpan(long start, int length)
    {
        // Get the underlying data in bulk and convert
        var longSpan = _list.AsSpan(start, length);
        var result = new int[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = (int)longSpan[i];
        }
        return result;
    }

    public ReadOnlySpan<int> AsSpan(long start)
    {
        var count = Count;
        if (start < 0 || start >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        var length = (int)(count - start);
        return AsSpan(start, length);
    }

    public ReadOnlySpan<int> GetRange(long start, int length)
    {
        return AsSpan(start, length);
    }

    public ReadOnlySpan<int> GetRange(long start)
    {
        return AsSpan(start);
    }

    public override string ToString()
    {
        return $"{_list.Count:N0} of {_priceTypeName}";
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    [Serializable]
    public struct Enumerator : IEnumerator<int>
    {
        [NonSerialized] private readonly ReadOnlyList64MmfIntFromLong _list;

        private long _index;

        internal Enumerator(ReadOnlyList64MmfIntFromLong list)
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

        public int Current { get; private set; }

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