using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;

namespace BruSoftware.ListMmf
{
    /// <summary>
    ///     ListMmfTimeSeries is always sorted in ascending order.
    ///     ListMmfTimeSeries is for DateTimes that wrap a long.
    ///     Use ListMmfTimeSeriesUnixSeconds for Unix seconds that wrap an int.
    /// </summary>
    // ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
    public class ListMmfTimeSeriesDateTimeSeconds : ListMmfBase<int>, IReadOnlyList64Mmf<DateTime>, IListMmf<DateTime>
    {
        /// <summary>
        ///     This is the size of the header used by this class
        /// </summary>
        private const int MyHeaderBytes = 0;

        private readonly Action<long, DateTime> _throwIfEarlierThanPreviousAction;

        private readonly Action<int, long, long> _throwIfEarlierThanPreviousTicksAction;
        private readonly TimeSeriesOrder _timeSeriesOrder;

        /// <summary>
        ///     Open a Writer on path
        ///     Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
        /// </summary>
        /// <param name="path">The path to open ReadWrite</param>
        /// <param name="timeSeriesOrder"></param>
        /// <param name="capacity">
        ///     The number of bits to initialize the list.
        ///     If 0, will be set to some default amount for a new file. Is ignored for an existing one.
        /// </param>
        /// <param name="access">Must be either Read or ReadWrite</param>
        /// <param name="parentHeaderBytes"></param>
        protected ListMmfTimeSeriesDateTimeSeconds(string path, TimeSeriesOrder timeSeriesOrder, long capacity = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.Read, long parentHeaderBytes = 0)
            : base(path, capacity, access, parentHeaderBytes + MyHeaderBytes)
        {
            _timeSeriesOrder = timeSeriesOrder;
            ResetView();
            switch (timeSeriesOrder)
            {
                case TimeSeriesOrder.None:
                    _throwIfEarlierThanPreviousAction = null;
                    _throwIfEarlierThanPreviousTicksAction = null;
                    break;
                case TimeSeriesOrder.Ascending:
                    _throwIfEarlierThanPreviousAction = ThrowIfEarlierOrEqualThanPrevious;
                    _throwIfEarlierThanPreviousTicksAction = ThrowIfEarlierOrEqualThanPreviousTicks;
                    break;
                case TimeSeriesOrder.AscendingOrEqual:
                    _throwIfEarlierThanPreviousAction = ThrowIfEarlierThanPrevious;
                    _throwIfEarlierThanPreviousTicksAction = ThrowIfEarlierThanPreviousTicks;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            if (access == MemoryMappedFileAccess.ReadWrite)
            {
                Version = 0;
                DataType = DataType.UnixSeconds;
            }
        }

        /// <summary>
        ///     Open a Writer on path
        ///     Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
        /// </summary>
        /// <param name="path">The path to open ReadWrite</param>
        /// <param name="timeSeriesOrder"></param>
        /// <param name="capacity">
        ///     The number of bits to initialize the list.
        ///     If 0, will be set to some default amount for a new file. Is ignored for an existing one.
        /// </param>
        /// <param name="access">Must be either Read or ReadWrite</param>
        public ListMmfTimeSeriesDateTimeSeconds(string path, TimeSeriesOrder timeSeriesOrder, long capacity = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.Read)
            : this(path, timeSeriesOrder, capacity, access, 0)
        {
        }

        public void Add(DateTime value)
        {
// #if DEBUG
//             if (value > DateTime.Now)
//             {
//                 throw new ListMmfException("Is this intended?. Likely okay if realtime.");
//             }
// #endif
            // if (value.Kind == DateTimeKind.Utc)
            // {
            //     throw new ListMmfException("We only use local times;");
            // }
            lock (SyncRoot)
            {
                Debug.Assert(!IsReadOnly);
                Debug.Assert(value.Ticks != 0);
                var count = UnsafeGetCount();
                _throwIfEarlierThanPreviousAction?.Invoke(count - 1, value);
                // if (IsReadOnly)
                // {
                //     throw new ListMmfException($"{nameof(Add)} cannot be done on this Read-Only list.");
                // }
                if (count + 1 > _capacity)
                {
                    GrowCapacity(count + 1);
                }
                // if (value.Ticks == 0)
                // {
                //     throw new ListMmfException("Why are we writing MinValue?");
                // }
                var seconds = value.ToUnixSeconds();
                UnsafeWriteNoLock(count, seconds);
                UnsafeSetCount(count + 1); // Change Count AFTER the value, so other processes will get correct
            }
        }

        public void AddRange(IEnumerable<DateTime> collection)
        {
            if (IsReadOnly)
            {
                throw new ListMmfException($"{nameof(AddRange)} cannot be done on this Read-Only list.");
            }
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }
            lock (SyncRoot)
            {
                var count = UnsafeGetCount();
                var currentCount = count;
                var prevValue = count == 0L ? 0L : this[count - 1].Ticks;
                switch (collection)
                {
                    case IReadOnlyList64<DateTime> list:
                        if (currentCount + list.Count > _capacity)
                        {
                            GrowCapacity(currentCount + list.Count);
                        }
                        for (var i = 0; i < list.Count; i++)
                        {
                            var ticks = list[i].Ticks;
                            _throwIfEarlierThanPreviousTicksAction?.Invoke(i, ticks, prevValue);
                            if (ticks == 0)
                            {
                                throw new ListMmfException("Why are we writing MinValue?");
                            }
                            var seconds = list[i].ToUnixSeconds();
                            UnsafeWriteNoLock(currentCount++, seconds);
                            prevValue = ticks;
                        }
                        break;
                    case IList<DateTime> list:
                        if (currentCount + list.Count > _capacity)
                        {
                            GrowCapacity(currentCount + list.Count);
                        }
                        for (var i = 0; i < list.Count; i++)
                        {
                            var ticks = list[i].Ticks;
                            _throwIfEarlierThanPreviousTicksAction?.Invoke(i, ticks, prevValue);
                            if (ticks == 0)
                            {
                                throw new ListMmfException("Why are we writing MinValue?");
                            }
                            var seconds = list[i].ToUnixSeconds();
                            UnsafeWriteNoLock(currentCount++, seconds);
                            prevValue = ticks;
                        }
                        break;
                    case ICollection<DateTime> c:
                        if (currentCount + c.Count > _capacity)
                        {
                            GrowCapacity(currentCount + c.Count);
                        }
                        foreach (var item in collection)
                        {
                            var ticks = item.Ticks;
                            if (ticks < prevValue)
                            {
                                var dateTime = new DateTime(ticks);
                                var prevDateTime = new DateTime(prevValue);
                                throw new OutOfOrderException($"{dateTime:yyyyMMdd.HHmmss.fffffff} cannot be "
                                                              + $"earlier than the value {prevDateTime:yyyyMMdd.HHmmss.fffffff} "
                                                              + $"at {Count - 1:N0}");
                            }
                            if (ticks == 0)
                            {
                                throw new ListMmfException("Why are we writing MinValue?");
                            }
                            var seconds = item.ToUnixSeconds();
                            UnsafeWriteNoLock(currentCount++, seconds);
                            prevValue = ticks;
                        }
                        break;
                    default:
                        using (var en = collection.GetEnumerator())
                        {
                            // Do inline Add
                            while (en.MoveNext())
                            {
                                if (currentCount + 1 > _capacity)
                                {
                                    GrowCapacity(currentCount + 1);
                                }
                                var ticks = en.Current.Ticks;
                                if (ticks < prevValue)
                                {
                                    var dateTime = new DateTime(ticks);
                                    var prevDateTime = new DateTime(prevValue);
                                    throw new OutOfOrderException($"{dateTime:yyyyMMdd.HHmmss.fffffff} cannot be "
                                                                  + $"earlier than the value {prevDateTime:yyyyMMdd.HHmmss.fffffff} at {Count - 1:N0}");
                                }
                                if (en.Current.Ticks == 0)
                                {
                                    throw new ListMmfException("Why are we writing MinValue?");
                                }
                                var seconds = en.Current.ToUnixSeconds();
                                UnsafeWriteNoLock(currentCount++, seconds);
                                prevValue = ticks;
                            }
                        }
                        return;
                }

                // Set Count last so readers won't access items before they are written
                UnsafeSetCount(currentCount);
            }
        }

        /// <summary>
        ///     This class only allows writing to the last item in the list, or Add() or AddRange() to append.
        /// </summary>
        /// <param name="value"></param>
        public void SetLast(DateTime value)
        {
            // if (value.Kind == DateTimeKind.Utc)
            // {
            //     throw new ListMmfException("We only use local times;");
            // }
            lock (SyncRoot)
            {
                Debug.Assert(value.Ticks > 0, "Why are we writing MinValue?");
                var count = UnsafeGetCount();
                var currValueSeconds = UnsafeRead(count - 1);
                var seconds = value.ToUnixSeconds();
                if (seconds < currValueSeconds)
                {
                    var lastBarTimestamp = currValueSeconds.FromUnixSecondsToDateTime();
                    var message = $"{value:yyyyMMdd.HHmmss} must not be less than the last bar timestamp {lastBarTimestamp:yyyyMMdd.HHmmss} for {this}";
                    Logger.Error(message);
                    throw new OutOfOrderException(message);
                }
                UnsafeWriteNoLock(count - 1, seconds);
            }
        }

        public virtual void Truncate(long newCapacityItems)
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
                UnsafeSetCount(newCapacityItems);
                ResetCapacity(newCapacityItems);
            }
        }

        /// <summary>
        ///     This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
        ///     e.g. are iterating (e.g. in a for loop)
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public new DateTime ReadUnchecked(long index)
        {
            var seconds = base.ReadUnchecked(index);
            if (seconds == 0)
            {
                throw new ListMmfException("Why are we reading MinValue?");
            }
            var result = seconds.FromUnixSecondsToDateTime();
            return result;
        }

        IEnumerator<DateTime> IEnumerable<DateTime>.GetEnumerator()
        {
            lock (SyncRoot)
            {
                return new ReadOnlyList64Enumerator<DateTime>(this);
            }
        }

        public IEnumerator GetEnumerator()
        {
            lock (SyncRoot)
            {
                return new ReadOnlyList64Enumerator<DateTime>(this);
            }
        }

        /// <summary>
        ///     Gets or Sets the value at index into the list, guaranteeing that ascending order is maintained
        /// </summary>
        /// <param name="index">an Int64 index relative to the start of the list</param>
        /// <returns></returns>
        /// <exception cref="AccessViolationException">if you try to set a value without ReadWrite access</exception>
        public DateTime this[long index]
        {
            get
            {
                lock (SyncRoot)
                {
                    // Following trick can reduce the range check by one
                    var count = Count;
                    if ((ulong)index >= (uint)count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index), count, $"Maximum index is {count - 1}");
                    }
                    var seconds = base.ReadUnchecked(index);
                    if (seconds == 0)
                    {
                        var msg = $"Why are we reading MinValue for {Path}? This can happen on corrupted data during machine crash, don't know why.";
                        Logger.Error(msg);
                        throw new ListMmfException(msg);
                    }
                    var result = seconds.FromUnixSecondsToDateTime();
                    // if (result.Kind == DateTimeKind.Utc)
                    // {
                    //     throw new ListMmfException("We only use local times;");
                    // }
                    return result;
                }
            }
            // set
            // {
            //     lock (SyncRoot)
            //     {
            //         _throwIfEarlierThanPreviousAction?.Invoke(index - 1, value);
            //         if (value.Ticks == 0)
            //         {
            //             throw new ListMmfException("Why are we writing MinValue?");
            //         }
            //         var seconds = value.ToUnixSeconds();
            //         UnsafeWriteNoLock(index, seconds);
            //     }
            // }
        }

        /// <summary>
        ///     This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
        ///     e.g. are iterating (e.g. in a for loop)
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public DateTime ReadUncheckedNoThrowIfZero(long index)
        {
            var seconds = base.ReadUnchecked(index);
            var result = seconds.FromUnixSecondsToDateTime();
            return result;
        }

        private void ThrowIfEarlierThanPreviousTicks(int index, long ticks, long prevValueTicks)
        {
            if (ticks < prevValueTicks)
            {
                var dateTime = new DateTime(ticks);
                var prevDateTime = new DateTime(prevValueTicks);
                var msg = $"{dateTime:yyyyMMdd.HHmmss.fffffff} cannot be "
                          + $"earlier than the value {prevDateTime:yyyyMMdd.HHmmss.fffffff} "
                          + $"at {index:N0} for {this}";
                Logger.Error(msg);
                throw new OutOfOrderException(msg);
            }
        }

        private void ThrowIfEarlierOrEqualThanPreviousTicks(int index, long ticks, long prevValueTicks)
        {
            if (ticks == 0)
            {
                throw new ListMmfException("Why are we writing MinValue?");
            }
            if (ticks <= prevValueTicks)
            {
                var dateTime = new DateTime(ticks);
                var prevDateTime = new DateTime(prevValueTicks);
                var msg = $"{dateTime:yyyyMMdd.HHmmss.fffffff} cannot be "
                          + $"earlier or equal to the value {prevDateTime:yyyyMMdd.HHmmss.fffffff} "
                          + $"at {index:N0} for {this}";
                Logger.Error(msg);
                throw new OutOfOrderException(msg);
            }
        }

        private void ThrowIfEarlierThanPrevious(long prevIndex, DateTime item)
        {
            if (item.Ticks == 0)
            {
                throw new ListMmfException("Why are we writing MinValue?");
            }
            var prevValue = prevIndex < 0 ? DateTime.MinValue : this[prevIndex];
            if (item < prevValue)
            {
                var message = $"{item:yyyyMMdd.HHmmss} cannot be earlier than the value {prevValue:yyyyMMdd.HHmmss} at {prevIndex:N0} for {this}";
                Logger.Error(message);
                throw new OutOfOrderException(message);
            }
        }

        private void ThrowIfEarlierOrEqualThanPrevious(long prevIndex, DateTime item)
        {
            if (item.Ticks == 0)
            {
                throw new ListMmfException("Why are we writing MinValue?");
            }
            var prevValue = prevIndex < 0 ? DateTime.MinValue : this[prevIndex];
            if (item <= prevValue)
            {
                var message = $"{item:yyyyMMdd.HHmmss.fffffff} cannot be "
                              + $"earlier or equal to the value {prevValue:yyyyMMdd.HHmmss.fffffff} at {prevIndex:N0} for {this}";
                Logger.Error(message);
                throw new OutOfOrderException(message);
            }
        }

        /// <summary>
        ///     Search for a specific element.
        ///     If this time series does not contain the specified value, the method returns a negative integer.
        ///     You can apply the bitwise complement operator (~ in C#) to the negative result to produce an index.
        ///     If this index is equal to the size of this time series, there are no items larger than value in this time series.
        ///     Otherwise, it is the index of the first element that is larger than value.
        ///     Duplicate time series items are allowed.
        ///     If this time series contains more than one item equal to value, the method returns the index of only one of the occurrences,
        ///     and not necessarily the first one.
        ///     This method is an O(log n) operation, where n is the length of the section to search.
        /// </summary>
        /// <param name="value">The value to search for.</param>
        /// <param name="index">The starting index for the search.</param>
        /// <param name="length">The length of this array or the length of the section to search. The default long.MaxValue means to use Count</param>
        /// <returns></returns>
        public long BinarySearch(DateTime value, long index = 0, long length = long.MaxValue)
        {
            lock (SyncRoot)
            {
                if (length == long.MaxValue)
                {
                    length = Count - index;
                }
                var valueSeconds = value.ToUnixSeconds();
                var lo = index;
                var hi = index + length - 1;
                while (lo <= hi)
                {
                    var i = lo + ((hi - lo) >> 1);
                    var arrayValue = UnsafeReadNoLock(i);
                    var order = arrayValue.CompareTo(valueSeconds);
                    if (order == 0)
                    {
                        return i;
                    }
                    if (order < 0)
                    {
                        lo = i + 1;
                    }
                    else
                    {
                        hi = i - 1;
                    }
                }
                return ~lo;
            }
        }

        /// <summary>
        ///     Like std::upper_bound. Returns the index of the first item in this array which compares greater than value.
        ///     In other words, this is the index of the first item with a value higher than value
        ///     If higher than any item, returns Count.
        ///     Duplicate items are allowed.
        ///     If this array contains more than one item equal to value, the method returns the index one higher than the last duplicate.
        ///     This method is an O(log n) operation, where n is the length of the section to search.
        /// </summary>
        /// <param name="value">The value to search for.</param>
        /// <param name="index">The starting index for the search.</param>
        /// <param name="length">The length of this array or the length of the section to search. The default long.MaxValue means to use Count</param>
        /// <returns>The index of the first element in this array which compares greater than value.</returns>
        public long GetUpperBound(DateTime value, long index = 0, long length = long.MaxValue)
        {
            lock (SyncRoot)
            {
                if (length == long.MaxValue)
                {
                    length = Count - index;
                }
                var valueSeconds = value.ToUnixSeconds();
                var result = BinarySearch(value, index, length);
                if (result >= 0)
                {
                    // BinarySearch found value. We want the index of the first arrayValue greater than value
                    while (++result < length)
                    {
                        var arrayValue = UnsafeReadNoLock(result);
                        if (!arrayValue.Equals(valueSeconds))
                        {
                            break;
                        }
                    }
                }
                else
                {
                    // BinarySearch returns the complement of the index to the first element higher than value
                    result = ~result;
                }
                return result;
            }
        }

        /// <summary>
        ///     Like std::lower_bound. Returns the index of the first item in this array which does not compare less than value.
        ///     In other words, this is the index of the first item with a value greater than or equal to value
        ///     If higher than any item, returns Count.
        ///     If lower than any item, returns 0.
        ///     Duplicate elements are allowed.
        ///     If this array contains more than one item equal to value, the method returns the index of the first duplicate.
        ///     This method is an O(log n) operation, where n is the length of the section to search.
        /// </summary>
        /// <param name="value">The value to search for.</param>
        /// <param name="index">The starting index for the search.</param>
        /// <param name="length">The length of this array or the length of the section to search. The default long.MaxValue means to use Count</param>
        /// <returns>The index of the first element in this array which compares greater than value.</returns>
        public long GetLowerBound(DateTime value, long index = 0, long length = long.MaxValue)
        {
            lock (SyncRoot)
            {
                if (length == long.MaxValue)
                {
                    length = Count - index;
                }
                var valueSeconds = value.ToUnixSeconds();
                var result = BinarySearch(value, index, length);
                if (result >= 0)
                {
                    // BinarySearch found value. We want the index of the lowest value equal to val
                    if (result == 0)
                    {
                        // We found a result at 0. Don't look lower.
                        return 0;
                    }

                    result--;
                    var arrayValue = UnsafeReadNoLock(result);
                    while (result > 0 && arrayValue.Equals(valueSeconds))
                    {
                        result--;
                        arrayValue = UnsafeReadNoLock(result);
                    }

                    // we went one too far
                    result++;
                }
                else
                {
                    // BinarySearch returns the complement of the index to the first element higher than value
                    // The lower bound is one lower (it doesn't fit in the 0 slot)
                    result = ~result - 1;
                }
                return result;
            }
        }

        /// <summary>
        ///     Of course this depends on this list being sorted, ascending!
        ///     Return the index of value in this list, using BinarySearch().
        ///     Return -1 if value is before the first value
        ///     Return -1 if value is after the last value
        ///     Return 0 only if value is equal to the first value
        ///     If value is between two value in this list, return the index for the higher one.
        /// </summary>
        /// <param name="value">The value to search for.</param>
        /// <param name="index">The starting index for the search.</param>
        /// <param name="length">The length of this list or the length of the section to search.</param>
        /// <returns>negative if not found</returns>
        public long GetIndex(DateTime value, long index, long length)
        {
            lock (SyncRoot)
            {
                if (length == long.MaxValue)
                {
                    length = Count - index;
                }
                var result = BinarySearch(value, index, length);
                if (result >= 0)
                {
                    // Found value
                    return result;
                }

                // BinarySearch returns the complement of the index to the first element higher than value
                result = ~result;
                if (result == 0 || result >= index + length)
                {
                    // above or below the range
                    return -1;
                }

                // Return the index to the next higher value
                return result;
            }
        }

        public override string ToString()
        {
            return base.ToString() + " " + _timeSeriesOrder;
        }
    }
}