using System;
using System.Collections;
using System.Collections.Generic;

namespace BruSoftware.ListMmf;

[Serializable]
public class ListReader64<T> : IReadOnlyList64<T>
{
    private static IReadOnlyList64<T> s_empty;
    private readonly IList<T> _array;
    private readonly int _beginIndex;
    private readonly int _count;

    /// <summary>
    /// Convert array to be IReadOnlyList64
    /// </summary>
    /// <param name="array"></param>
    /// <param name="beginIndex">The start of the view of array included in this list.</param>
    /// <param name="count">
    /// The number of elements of the view of array included in this list. Specify 0 (the default) to include all remaining elements in
    /// the array starting at beginIndex
    /// </param>
    public ListReader64(IList<T> array, int beginIndex = 0, int count = 0)
    {
        _array = array;
        _beginIndex = beginIndex;
        _count = count;
    }

    public static IReadOnlyList64<T> Empty => s_empty ??= new ListReader64<T>(new List<T>());
    public long Count => _count == 0 ? _array.Count - _beginIndex : _count;

    /// <summary>
    /// Gets a value at index in this segment of the underlying array.
    /// index == 0 means _beginIndex, the start of this segment, so you will do for (long i = 0L; i &lt; Count; i++)
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public T this[long index]
    {
        get
        {
            // Following trick can reduce the range check by one
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _array[_beginIndex + (int)index];
        }
    }

    public static IReadOnlyList64<T> GetDefault()
    {
        return Empty;
    }

    public override string ToString()
    {
        return $"{Count:N0} items starting at {_beginIndex:N0} out of {_array.Count:N0} total values of {_array}";
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    [Serializable]
    public struct Enumerator : IEnumerator<T>
    {
        private readonly ListReader64<T> _list;
        private long _index;

        internal Enumerator(ListReader64<T> list)
        {
            _list = list;
            _index = 0;
#pragma warning disable 8601
            Current = default;
#pragma warning restore 8601
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
            _index = _list._beginIndex;
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