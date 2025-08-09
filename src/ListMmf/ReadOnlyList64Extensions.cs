using System;
using System.Collections.Generic;
using System.Linq;

namespace BruSoftware.ListMmf;

public static class ReadOnlyList64Extensions
{
    /// <summary>
    /// Copies all the elements of the current one-dimensional array to the specified one-dimensional array
    /// starting at the specified destination array index. The index is specified as a 64-bit integer.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sourceArray"></param>
    /// <param name="array"></param>
    /// <param name="index">A 64-bit integer that represents the index in array at which copying begins.</param>
    /// <returns></returns>
    public static void CopyTo<T>(this IReadOnlyList64<T> sourceArray, T[] array, long index)
    {
        if (sourceArray.Count > array.Length - index)
        {
            // From https://docs.microsoft.com/en-us/dotnet/api/system.array.copyto?view=netframework-4.7.1#System_Array_CopyTo_System_Array_System_Int64_
            // The number of elements in the source array is greater than the available number of elements from index to the end of the destination array.
            throw new ArgumentException("Not enough room in the target array.");
        }
        for (var i = 0L; i < sourceArray.Count; i++)
        {
            array[i + index] = sourceArray[i];
        }
    }

    /// <summary>
    /// Copy a section of this list to the given array at the given index.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="sourceArray"></param>
    /// <param name="index"></param>
    /// <param name="array"></param>
    /// <param name="arrayIndex"></param>
    /// <param name="count"></param>
    public static void CopyTo<T>(this IReadOnlyList64<T> sourceArray, int index, T[] array, int arrayIndex, int count)
    {
        if (count > array.Length - arrayIndex)
        {
            // From https://docs.microsoft.com/en-us/dotnet/api/system.array.copyto?view=netframework-4.7.1#System_Array_CopyTo_System_Array_System_Int64_
            // The number of elements in the source array is greater than the available number of elements from index to the end of the destination array.
            throw new ArgumentException("Not enough room in the target array.");
        }
        if (sourceArray.Count - index < count)
        {
            throw new ArgumentException("Not enough values in the source array.");
        }
        for (var i = 0; i < count; i++)
        {
            var value = sourceArray[index + i];
            array[i + arrayIndex] = value;
        }
    }

    public static IReadOnlyList64<T> ToReadOnlyList64<T>(this IList<T> list)
    {
        return new ListReader64<T>(list);
    }

    public static IReadOnlyList64<T> ToReadOnlyList64<T>(this IEnumerable<T> enumerable)
    {
        return new ListReader64<T>(enumerable.ToList());
    }

    /// <summary>
    /// Convert an array or list of double to an array of floats.
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public static float[] ToFloatArray(this IReadOnlyList64<double> values)
    {
        var floats = new float[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            floats[i] = (float)values[i];
        }
        return floats;
    }

    /// <summary>
    /// Convert an array or list of double to an array of floats.
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public static float[] ToFloatArray(this IEnumerable<double> values)
    {
        var count = values.Count();
        var floats = new float[count];
        var i = 0;
        foreach (var value in values)
        {
            floats[i] = (float)value;
            i++;
        }
        return floats;
    }

    /// <summary>
    /// Of course this depends on the array being sorted, ascending!
    /// Search for a specific element.
    /// If array does not contain the specified value, the method returns a negative integer.
    /// You can apply the bitwise complement operator (~ in C#) to the negative result to produce an index.
    /// If this index is equal to the size of the array, there are no elements larger than value in the array.
    /// Otherwise, it is the index of the first element that is larger than value.
    /// T must implement the IComparer&lt;T&gt; generic interface, which is used for comparisons.
    /// The elements of array must already be sorted in increasing value according to the sort order defined by the IComparer&lt;T&gt; implementation;
    /// otherwise, the result might be incorrect.
    /// Duplicate elements are allowed.
    /// If the Array contains more than one element equal to value, the method returns the index of only one of the occurrences,
    /// and not necessarily the first one.
    /// This method is an O(log n) operation, where n is the Length of array.
    /// </summary>
    /// <typeparam name="T">Must be IComparable(T)</typeparam>
    /// <param name="array"></param>
    /// <param name="value">The value to search for.</param>
    /// <param name="index">The starting index for the search.</param>
    /// <param name="length">The length of the array or the length of the section to search.</param>
    /// <returns>
    /// The index of the specified value in the specified array, if value is found; otherwise, a negative number.
    /// If value is not found and value is less than one or more elements in array,
    /// the negative number returned is the bitwise complement of the index of the first element that is larger than value.
    /// If value is not found and value is greater than all elements in array,
    /// the negative number returned is the bitwise complement of (the index of the last element plus 1).
    /// If this method is called with a non-sorted array,
    /// the return value can be incorrect and a negative number could be returned, even if value is present in array.
    /// </returns>
    public static long BinarySearch<T>(this IReadOnlyList64<T> array, T value, long index, long length) where T : IComparable<T>
    {
        // Since T is constrained here, we can call IComparable<T>.CompareTo here.
        // We can avoid boxing for value type and casting for reference types.
        GuardForListMmfTimeSeries(array);
        var lo = index;
        var hi = index + length - 1;
        while (lo <= hi)
        {
            var i = lo + ((hi - lo) >> 1);
            int order;
            var arrayValue = array[i];
            if (arrayValue == null)
            {
                order = value == null ? 0 : -1;
            }
            else
            {
                order = arrayValue.CompareTo(value);
            }
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

    /// <summary>
    /// Get the lower bound for value in the entire file
    /// See https://en.cppreference.com/w/cpp/algorithm/lower_bound
    /// </summary>
    /// <param name="array"></param>
    /// <param name="value"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>the index of first element in the file that does not satisfy element less than value, or Count if no such element is found</returns>
    public static long LowerBound<T>(this IReadOnlyList64<T> array, T value) where T : IComparable<T>
    {
        return LowerBound(array, 0, array.Count, value);
    }

    /// <summary>
    /// Get the lower bound for value in the range from first to last.
    /// See https://en.cppreference.com/w/cpp/algorithm/lower_bound
    /// </summary>
    /// <param name="list"></param>
    /// <param name="first">This first index to search, must be 0 or higher</param>
    /// <param name="last">The index one higher than the highest index in the range (e.g. Count)</param>
    /// <param name="value"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>
    /// the index of first element in the range [first, last) that does not satisfy element less than value, or last (Count) if no such element is
    /// found
    /// </returns>
    public static long LowerBound<T>(this IReadOnlyList64<T> list, long first, long last, T value) where T : IComparable<T>
    {
        GuardForListMmfTimeSeries(list);
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }
        if (first < 0 || first > list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(first), "First index is out of range.");
        }
        if (last < first || last > list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(last), "Last index is out of range.");
        }
        var low = first;
        var high = last;
        while (low < high)
        {
            var mid = low + ((high - low) >> 1);
            if (list[mid].CompareTo(value) < 0)
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
    /// <param name="array"></param>
    /// <param name="value"></param>
    /// <returns>the index of first element in the file such that value is less than element, or Count if no such element is found</returns>
    public static long UpperBound<T>(this IReadOnlyList64<T> array, T value) where T : IComparable<T>
    {
        return UpperBound(array, 0, array.Count, value);
    }

    /// <summary>
    /// Get the upper bound for value in the range from first to last.
    /// See https://en.cppreference.com/w/cpp/algorithm/upper_bound
    /// </summary>
    /// <param name="list"></param>
    /// <param name="first">This first index to search, must be 0 or higher</param>
    /// <param name="last">The index one higher than the highest index in the range (e.g. Count)</param>
    /// <param name="value"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns>the index of first element in the range [first, last) such that value is less than element, or last (Count) if no such element is found</returns>
    public static long UpperBound<T>(this IReadOnlyList64<T> list, long first, long last, T value) where T : IComparable<T>
    {
        GuardForListMmfTimeSeries(list);
        if (list == null)
        {
            throw new ArgumentNullException(nameof(list));
        }
        if (first < 0 || first > list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(first), "First index is out of range.");
        }
        if (last < first || last > list.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(last), "Last index is out of range.");
        }
        var low = first;
        var high = last;
        while (low < high)
        {
            var mid = low + ((high - low) >> 1);
            if (list[mid].CompareTo(value) <= 0)
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

    private static void GuardForListMmfTimeSeries<T>(IReadOnlyList64<T> array) where T : IComparable<T>
    {
        if (array is ListMmfTimeSeriesDateTimeSeconds or ListMmfTimeSeriesDateTime)
        {
            throw new ListMmfException("Use much faster methods in ListMmfTimeSeriesDateTimeSeconds");
        }
    }
}