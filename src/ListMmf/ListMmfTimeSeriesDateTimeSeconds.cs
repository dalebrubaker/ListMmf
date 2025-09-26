using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace BruSoftware.ListMmf;

/// <summary>
/// ListMmfTimeSeries is always sorted in ascending order.
/// ListMmfTimeSeries is for DateTimes that wrap a long.
/// Use ListMmfTimeSeriesDateTimeSeconds for Unix seconds that wrap an int.
/// </summary>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class ListMmfTimeSeriesDateTimeSeconds : ListMmfBase<int>, IReadOnlyList64Mmf<DateTime>, IListMmf<DateTime>
{
    /// <summary>
    /// This is the size of the header used by this class
    /// </summary>
    private const int MyHeaderBytes = 0;

    private readonly Action<long, DateTime> _throwIfEarlierThanPreviousAction;

    private readonly Action<int, long, long> _throwIfEarlierThanPreviousTicksAction;
    private readonly TimeSeriesOrder _timeSeriesOrder;

    /// <summary>
    /// Open a Writer on path
    /// Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="timeSeriesOrder"></param>
    /// <param name="capacity">
    /// The number of items to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    /// <param name="parentHeaderBytes"></param>
    private ListMmfTimeSeriesDateTimeSeconds(string path, TimeSeriesOrder timeSeriesOrder, long capacity = 0,
        long parentHeaderBytes = 0)
        : base(path, capacity, parentHeaderBytes + MyHeaderBytes)
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
        Version = 0;
        DataType = DataType.UnixSeconds;
    }

    /// <summary>
    /// Open a Writer on path
    /// Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="timeSeriesOrder"></param>
    /// <param name="capacity">
    /// The number of items to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    public ListMmfTimeSeriesDateTimeSeconds(string path, TimeSeriesOrder timeSeriesOrder, long capacity = 0)
        : this(path, timeSeriesOrder, capacity, 0)
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
        Debug.Assert(value.Ticks != 0);
        var count = Count;
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
        UnsafeWrite(count, seconds);
        Count = count + 1; // Change Count AFTER the writing value, so other processes will get correct
    }

    public void AddRange(IEnumerable<DateTime> collection)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        var count = Count;
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
                    UnsafeWrite(currentCount++, seconds);
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
                    UnsafeWrite(currentCount++, seconds);
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
                    UnsafeWrite(currentCount++, seconds);
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
                        UnsafeWrite(currentCount++, seconds);
                        prevValue = ticks;
                    }
                }
                return;
        }

        // Set Count last so readers won't access items before they are written
        Count = currentCount;
    }

    /// <summary>
    /// Appends the elements of a ReadOnlySpan&lt;DateTime&gt; to the end of the time series using bulk copy operations.
    /// This provides significantly better performance than adding elements individually.
    /// Maintains time series ordering validation and converts to Unix seconds storage.
    /// </summary>
    /// <param name="span">The span containing DateTime elements to append</param>
    /// <exception cref="ArgumentException">If the span would cause the list to exceed capacity limits</exception>
    /// <exception cref="OutOfOrderException">If any DateTime in the span violates ordering constraints</exception>
    public void AddRange(ReadOnlySpan<DateTime> span)
    {
        if (span.IsEmpty)
        {
            return;
        }

        var count = Count;
        var newCount = count + span.Length;

        // Ensure capacity
        if (newCount > _capacity)
        {
            GrowCapacity(newCount);
        }

        // Validate ordering and convert to Unix seconds
        var prevValue = count == 0L ? 0L : this[count - 1].Ticks;
        for (var i = 0; i < span.Length; i++)
        {
            var dateTime = span[i];
            var ticks = dateTime.Ticks;
            Debug.Assert(ticks != 0);

            // Apply ordering validation
            _throwIfEarlierThanPreviousTicksAction?.Invoke(i, ticks, prevValue);

            if (ticks == 0)
            {
                throw new ListMmfException("Why are we writing MinValue?");
            }

            // Convert to Unix seconds and write
            var seconds = dateTime.ToUnixSeconds();
            UnsafeWrite(count + i, seconds);
            prevValue = ticks;
        }

        // Update count last
        Count = newCount;
    }

    /// <summary>
    /// Returns a Span&lt;DateTime&gt; representing a range of elements from the time series.
    /// This creates a new array since the underlying storage is in Unix seconds (int) but the interface exposes DateTime.
    /// For zero-copy access to the underlying seconds, cast to ListMmfBase&lt;int&gt; and use GetRange there.
    /// Uses bulk operations for improved performance compared to individual conversions.
    /// </summary>
    /// <param name="start">The starting index (inclusive)</param>
    /// <param name="length">The number of elements to include in the span</param>
    /// <returns>A Span&lt;DateTime&gt; representing the requested range</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start or length is invalid</exception>
    /// <exception cref="ListMmfOnlyInt32SupportedException">If length exceeds int.MaxValue</exception>
    public new Span<DateTime> AsSpan(long start, int length)
    {
        // Bounds validation
        var count = Count;
        if (start < 0 || start > count)
        {
            throw new ArgumentOutOfRangeException(nameof(start), $"start={start} must be >= 0 and <= Count={count}");
        }
        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length), "length must be >= 0");
        }
        if (start + length > count)
        {
            throw new ArgumentOutOfRangeException(nameof(length), $"start + length ({start + length}) exceeds Count={count}");
        }

        if (length == 0)
        {
            return Array.Empty<DateTime>().AsSpan();
        }

        // Use bulk read of underlying Unix seconds data for better performance
        var secondsSpan = base.AsSpan(start, length);
        var result = new DateTime[length];

        // Convert Unix seconds to DateTime in bulk
        for (var i = 0; i < secondsSpan.Length; i++)
        {
            result[i] = secondsSpan[i].FromUnixSecondsToDateTime();
        }

        return result.AsSpan();
    }

    /// <summary>
    /// Returns a Span&lt;DateTime&gt; representing elements from the start index to the end of the time series.
    /// This creates a new array since the underlying storage is in Unix seconds (int) but the interface exposes DateTime.
    /// </summary>
    /// <param name="start">The starting index (inclusive)</param>
    /// <returns>A Span&lt;DateTime&gt; representing elements from start to the end</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start is invalid</exception>
    /// <exception cref="ListMmfOnlyInt32SupportedException">If the resulting length exceeds int.MaxValue</exception>
    public new Span<DateTime> AsSpan(long start)
    {
        var count = Count;
        var length = count - start;
        if (length > int.MaxValue)
        {
            throw new ListMmfOnlyInt32SupportedException(length);
        }

        return AsSpan(start, (int)length);
    }

    public new Span<DateTime> GetRange(long start, int length)
    {
        return AsSpan(start, length);
    }

    public new Span<DateTime> GetRange(long start)
    {
        return AsSpan(start);
    }

    // Explicit interface implementations for ReadOnlySpan return type
    ReadOnlySpan<DateTime> IReadOnlyList64Mmf<DateTime>.GetRange(long start, int length)
    {
        return AsSpan(start, length);
    }

    ReadOnlySpan<DateTime> IReadOnlyList64Mmf<DateTime>.GetRange(long start)
    {
        return AsSpan(start);
    }

    ReadOnlySpan<DateTime> IReadOnlyList64Mmf<DateTime>.AsSpan(long start, int length)
    {
        return AsSpan(start, length);
    }

    ReadOnlySpan<DateTime> IReadOnlyList64Mmf<DateTime>.AsSpan(long start)
    {
        return AsSpan(start);
    }

    /// <summary>
    /// This class only allows writing to the last item in the list, or Add() or AddRange() to append.
    /// </summary>
    /// <param name="value"></param>
    public void SetLast(DateTime value)
    {
        // if (value.Kind == DateTimeKind.Utc)
        // {
        //     throw new ListMmfException("We only use local times;");
        // }
        Debug.Assert(value.Ticks > 0, "Why are we writing MinValue?");
        var count = Count;
        var currValueSeconds = UnsafeRead(count - 1);
        var seconds = value.ToUnixSeconds();
        if (seconds < currValueSeconds)
        {
            var lastBarTimestamp = currValueSeconds.FromUnixSecondsToDateTime();
            var message = $"{value:yyyyMMdd.HHmmss} must not be less than the last bar timestamp {lastBarTimestamp:yyyyMMdd.HHmmss} for {this}";
            throw new OutOfOrderException(message);
        }
        UnsafeWrite(count - 1, seconds);
    }

    /// <summary>
    /// This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
    /// e.g. are iterating (e.g. in a for loop)
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
        return new ReadOnlyList64Enumerator<DateTime>(this);
    }

    public IEnumerator GetEnumerator()
    {
        return new ReadOnlyList64Enumerator<DateTime>(this);
    }

    /// <summary>
    /// Gets or Sets the value at index into the list, guaranteeing that ascending order is maintained
    /// </summary>
    /// <param name="index">an Int64 index relative to the start of the list</param>
    /// <returns></returns>
    /// <exception cref="AccessViolationException">if you try to set a value without ReadWrite access</exception>
    public DateTime this[long index]
    {
        get
        {
            // Following trick can reduce the range check by one
            var count = Count;
            if ((ulong)index >= (uint)count)
            {
                if (_isDisposed)
                {
                    return DateTime.MinValue;
                }
                Debugger.Break();
                throw new ArgumentOutOfRangeException(nameof(index), count, $"Maximum index is {count - 1}");
            }
            var seconds = base.ReadUnchecked(index);
            if (seconds == 0)
            {
                var (lastValidIndex, lastValidTimestamp) = GetLastValidTimestampForErrorReporting();
                var msg = $"Why are we reading MinValue for {Path}? Count={count:N0} "
                          + $"but last valid timestamp is {lastValidTimestamp:O} at index={lastValidIndex:N0}.\n"
                          + $"This can happen on corrupted data during machine crash, don't know why.\n"
                          + $"Fix this by using Repository to truncate back to {lastValidTimestamp:O} or earlier.";
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

    /// <summary>
    /// Return the index of the last timestamp with a valid (non-zero) value, or -1 if not found
    /// This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
    /// e.g. are iterating (e.g. in a for loop)
    /// Uses bulk operations for improved performance compared to individual ReadUnchecked calls.
    /// </summary>
    /// <returns></returns>
    public long GetIndexOfLastNonZeroTimestamp()
    {
        var count = Count;
        if (count == 0)
        {
            return -1;
        }

        // Process in chunks from the end backwards for better performance
        const int chunkSize = 1000;
        var remaining = count;

        while (remaining > 0)
        {
            var currentChunkSize = (int)Math.Min(remaining, chunkSize);
            var startIndex = remaining - currentChunkSize;

            // Get a chunk of underlying Unix seconds data
            var chunk = base.AsSpan(startIndex, currentChunkSize);

            // Search backwards within this chunk
            for (var i = chunk.Length - 1; i >= 0; i--)
            {
                if (chunk[i] > 0)
                {
                    return startIndex + i;
                }
            }

            remaining -= currentChunkSize;
        }

        return -1;
    }

    /// <summary>
    /// Helper method to find the last valid timestamp index and value for error reporting.
    /// Uses bulk operations for improved performance compared to individual ReadUnchecked calls.
    /// </summary>
    /// <returns>Tuple of (index, timestamp) of the last valid entry, or (-1, DateTime.MinValue) if none found</returns>
    private (long index, DateTime timestamp) GetLastValidTimestampForErrorReporting()
    {
        var count = Count;
        if (count == 0)
        {
            return (-1, DateTime.MinValue);
        }

        // Process in chunks from the end backwards for better performance
        const int chunkSize = 1000;
        var remaining = count;

        while (remaining > 0)
        {
            var currentChunkSize = (int)Math.Min(remaining, chunkSize);
            var startIndex = remaining - currentChunkSize;

            // Get a chunk of underlying Unix seconds data
            var chunk = base.AsSpan(startIndex, currentChunkSize);

            // Search backwards within this chunk
            for (var i = chunk.Length - 1; i >= 0; i--)
            {
                if (chunk[i] > 0)
                {
                    var globalIndex = startIndex + i;
                    var timestamp = chunk[i].FromUnixSecondsToDateTime();
                    return (globalIndex, timestamp);
                }
            }

            remaining -= currentChunkSize;
        }

        return (-1, DateTime.MinValue);
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
            throw new OutOfOrderException(message);
        }
    }

    /// <summary>
    /// Search for a specific element.
    /// If this time series does not contain the specified value, the method returns a negative integer.
    /// You can apply the bitwise complement operator (~ in C#) to the negative result to produce an index.
    /// If this index is equal to the size of this time series, there are no items larger than value in this time series.
    /// Otherwise, it is the index of the first element that is larger than value.
    /// Duplicate time series items are allowed.
    /// If this time series contains more than one item equal to value, the method returns the index of only one of the occurrences,
    /// and not necessarily the first one.
    /// This method is an O(log n) operation, where n is the length of the section to search.
    /// </summary>
    /// <param name="value">The value to search for.</param>
    /// <param name="index">The starting index for the search.</param>
    /// <param name="length">The length of this array or the length of the section to search. The default long.MaxValue means to use Count</param>
    /// <returns></returns>
    public long BinarySearch(DateTime value, long index = 0, long length = long.MaxValue)
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
            var arrayValue = UnsafeRead(i);
            if (arrayValue == valueSeconds)
            {
                return i;
            }
            if (arrayValue < valueSeconds)
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

    /// <summary>
    /// Get the lower bound for value in the entire file
    /// See https://en.cppreference.com/w/cpp/algorithm/lower_bound
    /// </summary>
    /// <param name="value"></param>
    /// <returns>the index of first element in file) that does not satisfy element less than value, or Count if no such element is found</returns>
    public long LowerBound(DateTime value)
    {
        return LowerBound(0, Count, value);
    }

    /// <summary>
    /// Get the lower bound for value in the range from first to last.
    /// See https://en.cppreference.com/w/cpp/algorithm/lower_bound
    /// </summary>
    /// <param name="first">This first index to search, must be 0 or higher</param>
    /// <param name="last">The index one higher than the highest index in the range (e.g. Count)</param>
    /// <param name="value"></param>
    /// <returns>
    /// the index of first element in the range [first, last) that does not satisfy element less than value, or last (Count) if no such element is
    /// found
    /// </returns>
    public long LowerBound(long first, long last, DateTime value)
    {
        if (first < 0)
        {
            throw new ArgumentException($"first={first:N0} must not be negative", nameof(first));
        }
        var count = Count;
        if (last > count)
        {
            throw new ArgumentException($"last={last:N0} must not be higher than {count:N0}", nameof(last));
        }
        var valueSeconds = value.ToUnixSeconds();
        count = last - first;
        while (count > 0)
        {
            var step = count / 2;
            var i = first + step;
            var arrayValue = UnsafeRead(i);
            if (arrayValue < valueSeconds)
            {
                first = ++i;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }
        return first;
    }

    /// <summary>
    /// Get the upper bound for value in the entire file
    /// See https://en.cppreference.com/w/cpp/algorithm/upper_bound
    /// </summary>
    /// <param name="value"></param>
    /// <returns>the index of first element in the range [first, last) such that value is less than element, or last (Count) if no such element is found</returns>
    public long UpperBound(DateTime value)
    {
        return UpperBound(0, Count, value);
    }

    /// <summary>
    /// Get the upper bound for value in the range from first to last.
    /// See https://en.cppreference.com/w/cpp/algorithm/upper_bound
    /// </summary>
    /// <param name="first">This first index to search, must be 0 or higher</param>
    /// <param name="last">The index one higher than the highest index in the range (e.g. Count)</param>
    /// <param name="value"></param>
    /// <returns>the index of first element in the range [first, last) such that value is less than element, or last (Count) if no such element is found</returns>
    public long UpperBound(long first, long last, DateTime value)
    {
        if (first < 0)
        {
            throw new ArgumentException($"first={first:N0} must not be negative", nameof(first));
        }
        var count = Count;
        if (last > count)
        {
            throw new ArgumentException($"last={last:N0} must not be higher than {count:N0}", nameof(last));
        }
        var valueSeconds = value.ToUnixSeconds();
        count = last - first;
        while (count > 0)
        {
            var step = count / 2;
            var i = first + step;
            var arrayValue = UnsafeRead(i);
            if (!(valueSeconds < arrayValue))
            {
                first = ++i;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }
        return first;
    }

    /// <summary>
    /// Of course this depends on this list being sorted, ascending!
    /// Return the index of value in this list, using BinarySearch().
    /// Return -1 if value is before the first value
    /// Return -1 if value is after the last value
    /// Return 0 only if value is equal to the first value
    /// If value is between two value in this list, return the index for the higher one.
    /// </summary>
    /// <param name="value">The value to search for.</param>
    /// <param name="index">The starting index for the search.</param>
    /// <param name="length">The length of this list or the length of the section to search.</param>
    /// <returns>negative if not found</returns>
    public long GetIndex(DateTime value, long index, long length)
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

    public override string ToString()
    {
        return base.ToString() + " " + _timeSeriesOrder;
    }
}