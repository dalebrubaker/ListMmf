using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

namespace BruSoftware.ListMmf;

/// <summary>
/// ListMmfTimeSeries is always sorted in ascending order.
/// ListMmfTimeSeries is for DateTimes that wrap a long.
/// Use ListMmfTimeSeriesUnixSeconds for Unix seconds, wrapping an int.
/// </summary>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class ListMmfTimeSeriesDateTime : ListMmfBase<long>, IReadOnlyList64Mmf<DateTime>, IListMmf<DateTime>
{
    /// <summary>
    /// This is the size of the header used by this class
    /// </summary>
    private const int MyHeaderBytes = 0;

    private readonly Action<long, DateTime>? _throwIfEarlierThanPreviousAction;

    private readonly Action<int, long, long>? _throwIfEarlierThanPreviousTicksAction;

    /// <summary>
    /// Open a Writer on path
    /// Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="timeSeriesOrder"></param>
    /// <param name="capacity">
    /// The number of bits to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    /// <param name="parentHeaderBytes"></param>
    // ReSharper disable once MemberCanBePrivate.Global
    protected ListMmfTimeSeriesDateTime(string path, TimeSeriesOrder timeSeriesOrder, long capacity = 0,
        long parentHeaderBytes = 0)
        : base(path, capacity, parentHeaderBytes + MyHeaderBytes)
    {
        ResetView();
        switch (timeSeriesOrder)
        {
            case TimeSeriesOrder.None:
                _throwIfEarlierThanPreviousAction = null;
                _throwIfEarlierThanPreviousTicksAction = null;
                break;
            case TimeSeriesOrder.Ascending:
                _throwIfEarlierThanPreviousAction = ThrowIfEarlierThanPreviousOrEqual;
                _throwIfEarlierThanPreviousTicksAction = ThrowIfEarlierThanPreviousOrEqualTicks;
                break;
            case TimeSeriesOrder.AscendingOrEqual:
                _throwIfEarlierThanPreviousAction = ThrowIfEarlierThanPrevious;
                _throwIfEarlierThanPreviousTicksAction = ThrowIfEarlierThanPreviousTicks;
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
        Version = 0;
        DataType = DataType.DateTime;
    }

    /// <summary>
    /// Open a Writer on path
    /// Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="timeSeriesOrder"></param>
    /// <param name="capacity">
    /// The number of bits to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    public ListMmfTimeSeriesDateTime(string path, TimeSeriesOrder timeSeriesOrder, long capacity = 0)
        : this(path, timeSeriesOrder, capacity, 0)
    {
    }

    public void Add(DateTime value)
    {
        _throwIfEarlierThanPreviousAction?.Invoke(Count - 1, value);
        if (Count + 1 > Capacity)
        {
            GrowCapacity(Count + 1);
        }
        if (value.Ticks == 0)
        {
            throw new ListMmfException("Why are we writing MinValue?");
        }
        UnsafeWrite(Count, value.Ticks);
        Count++; // Change Count AFTER the value, so other processes will get correct
    }

    public void AddRange(IEnumerable<DateTime> collection)
    {
        if (collection == null)
        {
            throw new ArgumentNullException(nameof(collection));
        }
        var currentCount = Count;
        var prevValue = Count == 0L ? 0L : this[Count - 1].Ticks;
        switch (collection)
        {
            case IReadOnlyList64<DateTime> list:
                if (currentCount + list.Count > Capacity)
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
                    UnsafeWrite(currentCount++, ticks);
                    prevValue = ticks;
                }
                break;
            case IList<DateTime> list:
                if (currentCount + list.Count > Capacity)
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
                    UnsafeWrite(currentCount++, ticks);
                    prevValue = ticks;
                }
                break;
            case ICollection<DateTime> c:
                if (currentCount + c.Count > Capacity)
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
                    UnsafeWrite(currentCount++, item.Ticks);
                    prevValue = ticks;
                }
                break;
            default:
                using (var en = collection.GetEnumerator())
                {
                    // Do inline Add
                    while (en.MoveNext())
                    {
                        if (currentCount + 1 > Capacity)
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
                        UnsafeWrite(currentCount++, en.Current.Ticks);
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
    /// Maintains time series ordering validation.
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
        if (newCount > Capacity)
        {
            GrowCapacity(newCount);
        }

        // Validate ordering and convert to ticks
        var prevValue = count == 0L ? 0L : this[count - 1].Ticks;
        for (var i = 0; i < span.Length; i++)
        {
            var dateTime = span[i];
            var ticks = dateTime.Ticks;

            // Apply ordering validation
            _throwIfEarlierThanPreviousTicksAction?.Invoke(i, ticks, prevValue);

            if (ticks == 0)
            {
                throw new ListMmfException("Why are we writing MinValue?");
            }

            // Write directly to underlying storage
            UnsafeWrite(count + i, ticks);
            prevValue = ticks;
        }

        // Update count last
        Count = newCount;
    }

    /// <summary>
    /// Returns a Span&lt;DateTime&gt; representing a range of elements from the time series.
    /// This creates a new array since the underlying storage is in ticks (long) but the interface exposes DateTime.
    /// For zero-copy access to the underlying ticks, cast to ListMmfBase&lt;long&gt; and use GetRange there.
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

        // Use bulk read of underlying ticks data for better performance
        var ticksSpan = base.AsSpan(start, length);
        var result = new DateTime[length];

        // Convert ticks to DateTime in bulk
        for (var i = 0; i < ticksSpan.Length; i++)
        {
            result[i] = new DateTime(ticksSpan[i]);
        }

        return result.AsSpan();
    }

    /// <summary>
    /// Returns a Span&lt;DateTime&gt; representing elements from the start index to the end of the time series.
    /// This creates a new array since the underlying storage is in ticks (long) but the interface exposes DateTime.
    /// </summary>
    /// <param name="start">The starting index (inclusive)</param>
    /// <returns>A Span&lt;DateTime&gt; representing elements from start to the end</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start is invalid</exception>
    /// <exception cref="ListMmfOnlyInt32SupportedException">If the resulting length exceeds int.MaxValue</exception>
    public new Span<DateTime> AsSpan(long start)
    {
        var count = Count;
        if (start < 0 || start > count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        var length = (int)(count - start);
        return AsSpan(start, length);
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
        Debug.Assert(value.Ticks > 0, "Why are we writing MinValue?");
        var count = Count;
        var currValueTicks = UnsafeRead(count - 1);
        var ticks = value.Ticks;
        if (ticks < currValueTicks)
        {
            var lastBarTimestamp = new DateTime(currValueTicks);
            var message = $"{value:yyyyMMdd.HHmmss} must not be less than the last bar timestamp {lastBarTimestamp:yyyyMMdd.HHmmss} for {this}";
            throw new OutOfOrderException(message);
        }
        UnsafeWrite(count - 1, value.Ticks);
    }

    /// <summary>
    /// This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
    /// e.g. are iterating (e.g. in a for loop)
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public new DateTime ReadUnchecked(long index)
    {
        var result = new DateTime(base.ReadUnchecked(index));
        if (result.Ticks == 0)
        {
            throw new ListMmfException("Why are we reading MinValue?");
        }
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
            if ((ulong)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), Count, $"Maximum index is {Count - 1}");
            }
            var result = new DateTime(UnsafeRead(index));
            if (result.Ticks == 0)
            {
                var msg = $"Why are we reading MinValue for {Path}? This can happen on corrupted data during machine crash, don't know why.";
                throw new ListMmfException(msg);
            }
            return result;
        }
        set
        {
            _throwIfEarlierThanPreviousAction?.Invoke(index - 1, value);
            if (value.Ticks == 0)
            {
                throw new ListMmfException("Why are we writing MinValue?");
            }
            UnsafeWrite(index, value.Ticks);
        }
    }

    /// <summary>
    /// This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
    /// e.g. are iterating (e.g. in a for loop)
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    public DateTime ReadUncheckedNoThrowIfZero(long index)
    {
        var result = new DateTime(base.ReadUnchecked(index));
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
            throw new OutOfOrderException(msg);
        }
    }

    private void ThrowIfEarlierThanPreviousOrEqualTicks(int index, long ticks, long prevValueTicks)
    {
        if (ticks == 0)
        {
            throw new ListMmfException("Why are we writing MinValue?");
        }
        if (ticks <= prevValueTicks)
        {
            var dateTime = new DateTime(ticks);
            var prevDateTime = new DateTime(prevValueTicks);
            throw new OutOfOrderException($"{dateTime:yyyyMMdd.HHmmss.fffffff} cannot be "
                                          + $"earlier or equal to the value {prevDateTime:yyyyMMdd.HHmmss.fffffff} "
                                          + $"at {index:N0} for {this}");
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
            throw new OutOfOrderException($"{item:yyyyMMdd.HHmmss.fffffff} cannot be "
                                          + $"earlier than the value {prevValue:yyyyMMdd.HHmmss.fffffff} at {prevIndex:N0} for {this}");
        }
    }

    private void ThrowIfEarlierThanPreviousOrEqual(long prevIndex, DateTime item)
    {
        if (item.Ticks == 0)
        {
            throw new ListMmfException("Why are we writing MinValue?");
        }
        var prevValue = prevIndex < 0 ? DateTime.MinValue : this[prevIndex];
        if (item <= prevValue)
        {
            throw new OutOfOrderException($"{item:yyyyMMdd.HHmmss.fffffff} cannot be "
                                          + $"earlier or equal to the value {prevValue:yyyyMMdd.HHmmss.fffffff} at {prevIndex:N0} for {this}");
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
        var valueTicks = value.Ticks;
        var lo = index;
        var hi = index + length - 1;
        while (lo <= hi)
        {
            var i = lo + ((hi - lo) >> 1);
            var arrayValue = UnsafeRead(i);
            if (arrayValue == valueTicks)
            {
                return i;
            }
            if (arrayValue < valueTicks)
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
    /// Returns an index pointing to the first element in the range [first,last) which does not compare less than val.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public long LowerBound(DateTime value)
    {
        return LowerBound(0, Count, value);
    }

    /// <summary>
    /// Get the lower bound for value in the range from first to last.
    /// Returns an index pointing to the first element in the range [first,last) which does not compare less than val.
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
        var count = Count;
        if (first < 0 || first > count)
        {
            throw new ArgumentOutOfRangeException(nameof(first), "First index is out of range.");
        }
        if (last < first || last > count)
        {
            throw new ArgumentOutOfRangeException(nameof(last), "Last index is out of range.");
        }
        if (last > count)
        {
            throw new ArgumentException($"last={last:N0} must not be higher than {count:N0}", nameof(last));
        }
        var valueTicks = value.Ticks;
        var low = first;
        var high = last;
        while (low < high)
        {
            var mid = low + ((high - low) >> 1);
            var arrayValue = UnsafeRead(mid);
            if (arrayValue < valueTicks)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low;
    }

    /// <summary>
    /// Get the upper bound for value in the entire file
    /// See https://en.cppreference.com/w/cpp/algorithm/upper_bound
    /// </summary>
    /// <param name="value"></param>
    /// <returns>the index of first element in the file such that value is less than element, or Count if no such element is found</returns>
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
        var valueTicks = value.Ticks;
        var low = first;
        var high = last;
        while (low < high)
        {
            var mid = low + ((high - low) >> 1);
            var arrayValue = UnsafeRead(mid);
            if (!(valueTicks < arrayValue))
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }
        return low;
    }
}