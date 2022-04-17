using System;
using System.Collections.Generic;
using System.Linq;

namespace BruSoftware.ListMmf
{
    public static class ReadOnlyList64Extensions
    {
        /// <summary>
        ///     Copies all the elements of the current one-dimensional array to the specified one-dimensional array
        ///     starting at the specified destination array index. The index is specified as a 64-bit integer.
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
        ///     Copy a section of this list to the given array at the given index.
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
            return new ListReader<T>(list);
        }

        public static IReadOnlyList64<T> ToReadOnlyList64<T>(this IEnumerable<T> enumerable)
        {
            return new ListReader<T>(enumerable.ToList());
        }

        /// <summary>
        ///     Convert an array or list of double to an array of floats.
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
        ///     Convert an array or list of double to an array of floats.
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
        ///     Of course this depends on the array being sorted, ascending!
        ///     Search for a specific element.
        ///     If array does not contain the specified value, the method returns a negative integer.
        ///     You can apply the bitwise complement operator (~ in C#) to the negative result to produce an index.
        ///     If this index is equal to the size of the array, there are no elements larger than value in the array.
        ///     Otherwise, it is the index of the first element that is larger than value.
        ///     T must implement the IComparer&lt;T&gt; generic interface, which is used for comparisons.
        ///     The elements of array must already be sorted in increasing value according to the sort order defined by the IComparer&lt;T&gt; implementation;
        ///     otherwise, the result might be incorrect.
        ///     Duplicate elements are allowed.
        ///     If the Array contains more than one element equal to value, the method returns the index of only one of the occurrences,
        ///     and not necessarily the first one.
        ///     This method is an O(log n) operation, where n is the Length of array.
        /// </summary>
        /// <typeparam name="T">Must be IComparable(T)</typeparam>
        /// <param name="array"></param>
        /// <param name="value">The value to search for.</param>
        /// <param name="index">The starting index for the search.</param>
        /// <param name="length">The length of the array or the length of the section to search.</param>
        /// <returns>
        ///     The index of the specified value in the specified array, if value is found; otherwise, a negative number.
        ///     If value is not found and value is less than one or more elements in array,
        ///     the negative number returned is the bitwise complement of the index of the first element that is larger than value.
        ///     If value is not found and value is greater than all elements in array,
        ///     the negative number returned is the bitwise complement of (the index of the last element plus 1).
        ///     If this method is called with a non-sorted array,
        ///     the return value can be incorrect and a negative number could be returned, even if value is present in array.
        /// </returns>
        public static long BinarySearch<T>(this IReadOnlyList64<T> array, T value, long index, long length) where T : IComparable<T>
        {
            // This is called when the user doesn't specify any comparer.
            // Since T is constrained here, we can call IComparable<T>.CompareTo here.
            // We can avoid boxing for value type and casting for reference types.
            var lo = index;
            var hi = index + length - 1;
            while (lo <= hi)
            {
                var i = lo + ((hi - lo) >> 1);

                //s_logger.ConditionalDebug($"lo={lo} hi={hi} i={i} value={value}");
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
                    //s_logger.ConditionalDebug($"found i={i} arrayValue={arrayValue}");
                    return i;
                }
                if (order < 0)
                {
                    lo = i + 1;

                    //s_logger.ConditionalDebug($"lo={lo} arrayValue={arrayValue}");
                }
                else
                {
                    hi = i - 1;

                    //s_logger.ConditionalDebug($"hi={hi} arrayValue={arrayValue}");
                }
            }

            //s_logger.ConditionalDebug($"Not found, returning ~lo={~lo}");
            return ~lo;
        }

        /// <summary>
        ///     Of course this depends on the array being sorted, ascending!
        ///     Return the index of value in the array, using BinarySearch().
        ///     Return -1 if value is before the first value
        ///     Return -1 if value is after the last value
        ///     Return 0 only if value is equal to the first value
        ///     If value is between two value in the array, return the index for the higher one.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="value">The value to search for.</param>
        /// <returns>negative if not found</returns>
        public static long GetIndex<T>(this IReadOnlyList64<T> array, T value) where T : IComparable<T>
        {
            return GetIndex(array, value, 0, array.Count);
        }

        /// <summary>
        ///     Of course this depends on the array being sorted, ascending!
        ///     Return the index of value in the array, using BinarySearch().
        ///     Return -1 if value is before the first value
        ///     Return -1 if value is after the last value
        ///     Return 0 only if value is equal to the first value
        ///     If value is between two value in the array, return the index for the higher one.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="value">The value to search for.</param>
        /// <param name="index">The starting index for the search.</param>
        /// <param name="length">The length of the array or the length of the section to search.</param>
        /// <returns>negative if not found</returns>
        public static long GetIndex<T>(this IReadOnlyList64<T> array, T value, long index, long length) where T : IComparable<T>
        {
            var result = array.BinarySearch(value, index, length);
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

        /// <summary>
        ///     Of course this depends on the array being sorted, ascending!
        ///     Like std::lower_bound. Returns the index of the first element in the array which does not compare less than value.
        ///     In other words, this is the index of the first element with a value greater than or equal to value
        ///     If higher than any sortedArray element, returns sortedArray.Length.
        ///     T must implement the IComparer&lt;T&gt; generic interface, which is used for comparisons.
        ///     The elements of array must already be sorted in increasing value according to the sort order defined by the IComparer&lt;T&gt; implementation;
        ///     otherwise, the result might be incorrect.
        ///     Duplicate elements are allowed.
        ///     If the Array contains more than one element equal to value, the method returns the index of only one of the occurrences,
        ///     and not necessarily the first one.
        ///     This method is an O(log n) operation, where n is the Length of array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="value">The value to search for.</param>
        /// <returns>The index of the first element in the array which does not compare less than value, or Length if value is above the array.</returns>
        public static long GetLowerBound<T>(this IReadOnlyList64<T> array, T value) where T : IComparable<T>
        {
            return GetLowerBound(array, value, 0, array.Count);
        }

        /// <summary>
        ///     Of course this depends on the array being sorted, ascending!
        ///     Like std::lower_bound. Returns the index of the first element in the array which does not compare less than value.
        ///     In other words, this is the index of the first element with a value greater than or equal to value
        ///     If higher than any sortedArray element, returns sortedArray.Length.
        ///     T must implement the IComparer&lt;T&gt; generic interface, which is used for comparisons.
        ///     The elements of array must already be sorted in increasing value according to the sort order defined by the IComparer&lt;T&gt; implementation;
        ///     otherwise, the result might be incorrect.
        ///     Duplicate elements are allowed.
        ///     If the Array contains more than one element equal to value, the method returns the index of only one of the occurrences,
        ///     and not necessarily the first one.
        ///     This method is an O(log n) operation, where n is the Length of array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="value">The value to search for.</param>
        /// <param name="index">The starting index for the search.</param>
        /// <param name="length">The length of the array or the length of the section to search.</param>
        /// <returns>The index of the first element in the array which does not compare less than value, or Length if value is above the array.</returns>
        public static long GetLowerBound<T>(this IReadOnlyList64<T> array, T value, long index, long length) where T : IComparable<T>
        {
            var result = array.BinarySearch(value, index, length);
            if (result >= 0)
            {
                // BinarySearch found value. We want the index of the lowest value equal to val
                if (result == 0)
                {
                    // We found a result at 0. Don't look lower.
                    return 0;
                }

                result--;
                var arrayValue = array[result];
                while (result > 0 && arrayValue.Equals(value))
                {
                    result--;
                    arrayValue = array[result];
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
            if (result > array.Count)
            {
                var msg = $"result={result} vs. Count={array.Count}";
                throw new ListMmfException(msg);
            }
            return result;
        }

        /// <summary>
        ///     Of course this depends on the array being sorted, ascending!
        ///     Like std::upper_bound. Returns the index of the first element in the array which compares greater than value.
        ///     In other words, this is the index of the first element with a value higher than val
        ///     If higher than any sortedArray element, returns sortedArray.Length.
        ///     T must implement the IComparer&lt;T&gt; generic interface, which is used for comparisons.
        ///     The elements of array must already be sorted in increasing value according to the sort order defined by the IComparer&lt;T&gt; implementation;
        ///     otherwise, the result might be incorrect.
        ///     Duplicate elements are allowed.
        ///     If the Array contains more than one element equal to value, the method returns the index of only one of the occurrences,
        ///     and not necessarily the first one.
        ///     This method is an O(log n) operation, where n is the Length of array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="value">The value to search for.</param>
        /// <param name="index">The starting index for the search.</param>
        /// <param name="length">The length of the array or the length of the section to search.</param>
        /// <returns>The index of the first element in the array which compares greater than value.</returns>
        public static long GetUpperBound<T>(this IReadOnlyList64<T> array, T value, long index = 0, long length = long.MaxValue) where T : IComparable<T>
        {
            if (length == long.MaxValue)
            {
                length = array.Count - index;
            }
            var result = array.BinarySearch(value, index, length);
            if (result >= 0)
            {
                // BinarySearch found value. We want the index of the first arrayValue greater than value
                while (++result < length)
                {
                    var arrayValue = array[result];
                    if (!arrayValue.Equals(value))
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
}