﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace BruSoftware.ListMmf;

/// <summary>
///     Show a view of an IReadOnlyList64 consisting of Count elements starting at _lowerBound, optionally capped at _countOverride
/// </summary>
/// <typeparam name="T"></typeparam>
public class ReadOnlyList64View<T> : IReadOnlyList64<T>
{
    private readonly bool _isCountFixed;
    private readonly IReadOnlyList64<T> _list;
    private readonly long _lowerBound;
    private long _countOverride;

    /// <summary>
    ///     Show a view of an IReadOnlyList64 consisting of Count elements starting at lowerBound
    ///     If count is NOT long.MaxValue, Count is fixed at count.
    ///     If count is long.MaxValue, Count = list.Count - lowerBound.
    /// </summary>
    /// <param name="list"></param>
    /// <param name="lowerBound">The start of the view of array included in this list.</param>
    /// <param name="count">
    ///     long.MaxValue means the count is not fixed and Count is list.Count - lowerBound,
    ///     growing or shrinking as list grows or shrinks
    /// </param>
    public ReadOnlyList64View(IReadOnlyList64<T> list, long lowerBound, long count = long.MaxValue)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
        _lowerBound = lowerBound;
        _countOverride = count;
        _isCountFixed = count != long.MaxValue;
        if (Count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count can't be set to negative");
        }
    }

    /// <summary>
    ///     Return the number of list elements starting at _lowerBound, possibly stopped at _countOverride.
    /// </summary>
    public long Count
    {
        get
        {
            if (_isCountFixed && _countOverride + _lowerBound > _list.Count)
            {
                // Maybe file was truncated?
                _countOverride = _list.Count - _lowerBound;
            }
            var result = _isCountFixed ? _countOverride : _list.Count - _lowerBound;
            return result;
        }
    }

    /// <summary>
    ///     Gets a value at index in this segment of the underlying list.
    ///     index == 0 means _lowerBound, the start of this segment, so you will do for (long i = 0L; i &lt; Count; i++)
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public T this[long index]
    {
        get
        {
            var absoluteIndex = _lowerBound + index;
            if (absoluteIndex < 0)
            {
                var msg = $"index={index:N0} when _lowerBound={_lowerBound:N0} gives absoluteIndex={absoluteIndex:N0}";
                throw new ArgumentOutOfRangeException(nameof(index), msg);
            }
            if (index >= Count)
            {
                var msg = $"index={index:N0} too high, Count={Count:N0}";
                throw new ArgumentOutOfRangeException(nameof(index), msg);
            }
            return _list[absoluteIndex];
        }
    }

    public override string ToString()
    {
        return $"{Count:N0} values of {typeof(T)} starting at {_lowerBound:N0}";
    }

    /// <summary>
    ///     This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
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
        ///     This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
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