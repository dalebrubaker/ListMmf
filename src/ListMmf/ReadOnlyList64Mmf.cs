using System;
using System.Collections;
using System.Collections.Generic;

namespace BruSoftware.ListMmf;

/// <summary>
/// Show a ListMmf as a IReadOnlyList64Mmf
/// </summary>
/// <typeparam name="T"></typeparam>
public class ReadOnlyList64Mmf<T>(IReadOnlyList64Mmf<T> list) : IReadOnlyList64Mmf<T>
{
    private readonly IReadOnlyList64Mmf<T> _list = list ?? throw new ArgumentNullException(nameof(list));

    public long Count => _list.Count;

    public T this[long index] => _list[index];

    public T ReadUnchecked(long index)
    {
        return _list.ReadUnchecked(index);
    }

    public ReadOnlySpan<T> GetRange(long start, int length)
    {
        return _list.GetRange(start, length);
    }

    public ReadOnlySpan<T> GetRange(long start)
    {
        return _list.GetRange(start);
    }

    public override string ToString()
    {
        return $"{Count:N0} values of {typeof(T)}";
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly IReadOnlyList64<T> _list;
        private long _index;

        internal Enumerator(IReadOnlyList64<T> list)
        {
            _list = list;
            _index = 0;
            Current = default;
        }

        public void Dispose()
        {
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
                    throw new InvalidOperationException("Enum Op Can't Happen");
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

    #region Implementation of IEnumerable

    /// <summary>Returns an enumerator that iterates through the collection.</summary>
    /// <returns>An enumerator that can be used to iterate through the collection.</returns>
    public IEnumerator<T> GetEnumerator()
    {
        return new Enumerator(this);
    }

    /// <summary>Returns an enumerator that iterates through a collection.</summary>
    /// <returns>An <see cref="T:System.Collections.IEnumerator" /> object that can be used to iterate through the collection.</returns>
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    #endregion Implementation of IEnumerable
}