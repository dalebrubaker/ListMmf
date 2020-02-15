using System;
using System.Collections.Generic;

namespace BruSoftware.ListMmf.Interfaces
{
    public interface IMmfArray<T> : IMmfArray, IReadOnlyList64<T>
    {
        ///// <summary>
        ///// Return the value of index
        ///// NO CHECKING is done on index. You are responsible for your own bounds checking.
        ///// Index MUST be in the range from BeginIndex through EndIndex.
        ///// This is considerably faster than the indexer.
        ///// </summary>
        ///// <param name="index">an Int64 index relative to the start of the array</param>
        ///// <returns></returns>
        ///// <exception cref="NullReferenceException">if you forgot to ResetView()</exception>
        //T ReadUnchecked(long index);

        ///// <summary>
        ///// Write value at index
        ///// NO CHECKING is done on index. You are responsible for your own bounds checking.
        ///// Index MUST be in the range from BeginIndex through EndIndex.
        ///// This is considerably faster than the indexer.
        ///// </summary>
        ///// <param name="index">an Int64 index relative to the start of the array</param>
        ///// <param name="value"></param>
        ///// <exception cref="NullReferenceException">if you forgot to ResetView()</exception>
        ///// <exception cref="AccessViolationException">if you try to write a value without ReadWrite access</exception>
        //void WriteUnchecked(long index, T value);

        /// <summary>
        /// Adds the given object to the end of this array. The size of the array is increased by one.
        /// If required, the capacity of the array is increased before adding the new element.
        /// </summary>
        /// <param name="item"></param>
        void Add(T item);

        /// <summary>
        /// Adds the elements of the given collection to the end of this array.
        /// If required, the capacity of this array is increased before adding the new elements.
        /// </summary>
        /// <param name="collection"></param>
        /// <exception cref="ListMmfException">if list won't fit</exception>
        void AddRange(IEnumerable<T> collection);

        /// <summary>
        /// Adds the elements of the given IReadOnlyList64 to the end of this array.
        /// If required, the capacity of this array is increased before adding the new elements.
        /// </summary>
        /// <param name="list"></param>
        /// <exception cref="ListMmfException">if list won't fit</exception>
        void AddRange(IReadOnlyList64<T> list);

        /// <summary>
        /// Contains returns true if the specified element is in the array.
        /// It does a linear, O(n) search.  Equality is determined by calling item.Equals().
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool Contains(T item);

        /// <summary>
        ///  CopyTo copies a section of this array into an Array targetArray, starting at a particular index into the targetArray.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        void CopyTo(T[] array, long arrayIndex);

        /// <summary>
        /// Copy a section of this list to the given array at the given index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        /// <param name="count"></param>
        void CopyTo(int index, T[] array, int arrayIndex, long count);

        /// <summary>
        /// Returns the index of a particular item, if it is in this array.
        /// The array is searched forwards from beginning to end.
        /// Equality is determined by calling item.Equals().
        /// Returns -1 if the item isn't in the array.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        long IndexOf(T item);

        /// <summary>
        /// Gets or Sets the value at index into the array AFTER ensuring that the view is moved, if necessary, to include index.
        /// </summary>
        /// <param name="index">an Int64 index relative to the start of the array</param>
        /// <returns></returns>
        /// <exception cref="AccessViolationException">if you try to set a value without ReadWrite access</exception>
        new T this[long index] { get; set; }

        /// <summary>
        /// Return an IReadOnlyList64 consisting of Count elements starting at lowerBound
        /// If count is NOT long.MaxValue, Count is fixed at count.
        /// If count is long.MaxValue, Count = list.Count - lowerBound.
        /// </summary>
        /// <param name="lowerBound">The start of the view of array included in this list.</param>
        /// <param name="count">long.MaxValue means the count is not fixed and Count is list.Count - lowerBound, growing or shrinking as list grows or shrinks</param>
        /// <returns></returns>
        IReadOnlyList64<T> GetReadOnlyList64(long lowerBound, long count = long.MaxValue);
    }

    public interface IMmfArray : IDisposable
    {
        /// <summary>
        /// Number of items in the array.
        /// </summary>
        long Length { get; }

        bool IsReadOnly { get; }

        void Clear();


        ///// <summary>
        ///// Extend the array (underlying file) to at least the given minimum number of elements.
        ///// Adding a value to the array is expensive, as the underlying file etc. must closed and re-opened.
        ///// The file will shrink back to Length when it is Disposed().
        ///// </summary>
        ///// <param name="minNumElements"></param>
        //void EnsureCapacity(long minNumElements);

        ///// <summary>
        ///// Extend the array to Length == newLength, setting added elements to zero.
        ///// </summary>
        ///// <param name="newLength"></param>
        //void Extend(long newLength);

        ///// <summary>
        ///// Truncate Length to newCapacity elements.
        ///// If no other writer or reader is accessing the file, this also reduces Capacity, the size of the file.
        ///// This method is only allowed for the Writer, not the Reader.
        ///// </summary>
        ///// <param name="newLength"></param>
        ///// <exception cref="NotSupportedException">The array is read-only.</exception>
        //void Truncate(long newLength);


        ///// <summary>
        ///// Get additional information about this array
        ///// </summary>
        ///// <returns></returns>
        //string GetInfo();

        ///// <summary>
        ///// The fully-qualified path to the file
        ///// </summary>
        //string FilePath { get; }

        ///// <summary>
        ///// The identifier for the container holding this array
        ///// </summary>
        //MmfContainerIdentifier MmfContainerIdentifier { get; }
    }
}
