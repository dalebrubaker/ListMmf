using System;
using System.Collections;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

/// <summary>
/// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
/// </summary>
// ReSharper disable once InconsistentNaming
public class ReadOnlyList64MmfCalculated<T> : IReadOnlyList64Mmf<T> where T : struct
{
    private readonly Func<long, T> _funcGetCalculatedValueAtIndex;
    private readonly Func<long> _funcGetCount;
    private readonly string _priceTypeName;

    public ReadOnlyList64MmfCalculated(Func<long> funcGetCount, Func<long, T> funcGetCalculatedValueAtIndex, string priceTypeName)
    {
        _funcGetCount = funcGetCount;
        _funcGetCalculatedValueAtIndex = funcGetCalculatedValueAtIndex;
        _priceTypeName = priceTypeName;
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    /// <returns></returns>
    public IEnumerator<T> GetEnumerator()
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

    public long Count => _funcGetCount();

    /// <summary>
    /// Returns 0 (default(T)) if the index was Reset()
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public T this[long index] => _funcGetCalculatedValueAtIndex(index);

    public T ReadUnchecked(long index)
    {
        return _funcGetCalculatedValueAtIndex(index);
    }

    public ReadOnlySpan<T> GetRange(long start, int length)
    {
        // For calculated lists, we must compute each value individually
        if (start < 0 || start + length > Count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        
        var result = new T[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = _funcGetCalculatedValueAtIndex(start + i);
        }
        return result;
    }

    public ReadOnlySpan<T> GetRange(long start)
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
        return $"{_funcGetCount:N0} of {_priceTypeName}";
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    [Serializable]
    public struct Enumerator : IEnumerator<T>
    {
        [NonSerialized] private readonly ReadOnlyList64MmfCalculated<T> _list;

        private long _index;

        internal Enumerator(ReadOnlyList64MmfCalculated<T> list)
        {
            _list = list;
            _index = 0;
            Current = default;
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
                    throw new InvalidOperationException("Enum Op Cant Happen");
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