using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BruSoftware.ListMmf
{
    public unsafe partial class ListMmf<T> : ListMmfBase, IList64<T>, IList64, IReadOnlyList64<T>, IDisposable, IEnumerable where T : struct
    {
        public const string SemaphoreUniquePrefix = "LM";
        private readonly long _headerReserveBytes;
        private readonly MemoryMappedFileAccess _access;
        private string _semaphoreUniqueName;
        private Semaphore _semaphoreUnique; // Semaphore created for _semapahoreUniqueName

        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _view;

        /// <summary>
        /// This is the field corresponding to Capacity (in Elements, not Bytes)
        /// </summary>
        private long _capacity;

        public object SyncRoot { get; }

        /// <summary>
        /// Null means this class is memory-based, not-persisted
        /// Not-null means this class is file-based, persisted, like CreateFromFile()
        /// </summary>
        private FileStream _fileStream;

        /// <summary>
        /// <c>false</c> means this is Memory based (not persisted)
        /// </summary>
        private readonly bool _isFileBased;

        private readonly bool _leaveOpen;

        /// <summary>
        /// Not-null means system-wide
        /// </summary>
        private readonly string _mapName;

        /// <summary>
        /// The size of T
        /// </summary>
        private readonly int _sizeOfT;

        /// <summary>
        /// This is the beginning of the View, before the headerReserveBytes and the 8-byte Length of this array.
        /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
        ///     I don't know why they do that, and I don't seem to need it...
        /// </summary>
        protected byte* _basePointerView;

        /// <summary>
        /// This is the beginning of the Array, after the headerReserveBytes and the 8-byte Length of this array.
        /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
        ///     I don't know why they do that, and I don't seem to need it...
        /// </summary>
        protected byte* _ptrArray;

        /// <summary>
        /// The long* into the Count location in the view
        /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
        ///     I don't know why they do that, and I don't seem to need it...
        /// </summary>
        private long* _ptrCount;

        private Locker _locker;

        /// <summary>
        /// The capacity in bytes. Note that for persisted MMFs, the ByteLength can exceed _fileStream.Length, so writes to that region may be going to never-never-land.
        /// </summary>
        public long CapacityBytes => (long)_view.SafeMemoryMappedViewHandle.ByteLength;

        public bool IsReadOnly { get; }

        /// <summary>
        /// The given mapName or path or _fileStream.Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Reader {Name} or Writer {Name}
        /// </summary>
        public string AccessName { get; set; }

        public bool IsFixedSize => false;
        public bool IsSynchronized => false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerReserveBytes">Bytes to reserve in header. Evenly divisible by 8 for alignment</param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        /// <param name="mmf"></param>
        /// <param name="access">Only Read and ReadWrite are allowed</param>
        /// <param name="fileStream"><c>null</c> means Memory not File-backed</param>
        /// <param name="mapName"><c>null</c> with non-null fileStream means created from file but not sharing</param>
        /// <param name="leaveOpen"></param>
        private ListMmf(long headerReserveBytes, bool noLocking, MemoryMappedFile mmf, MemoryMappedFileAccess access, FileStream fileStream, string mapName, bool leaveOpen = false)
            : base(fileStream == null ? mapName : fileStream.Name)
        {
            if (!Environment.Is64BitOperatingSystem)
            {
                throw new ListMmfException("Not supported on 32-bit operating system. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            }
            if (!Environment.Is64BitProcess)
            {
                throw new ListMmfException("Not supported on 32-bit process. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            }
            if (headerReserveBytes % 8 != 0)
            {
                throw new ListMmfException($"{nameof(headerReserveBytes)} is required to be a multiple of 8 bytes.");
            }
            _headerReserveBytes = headerReserveBytes;
            _mmf = mmf;
            _access = access;
            _fileStream = fileStream;
            _isFileBased = _fileStream != null;
            _mapName = mapName;
            _leaveOpen = leaveOpen;
            _sizeOfT = Unsafe.SizeOf<T>();
            IsReadOnly = access == MemoryMappedFileAccess.Read;
            SyncRoot = new object();
            Name = fileStream == null ? mapName : fileStream.Name;
            AccessName = IsReadOnly ? "Reader " : "Writer " + Name;
            SetSemaphoreUniqueName();
            SetLocking(noLocking);
            ResetView();
        }

        /// <summary>
        /// This method is called whenever _mmf and View are changed.
        /// Inheritors should first call base.ResetPointers() and then reset their own pointers (if any) from BasePointerByte
        /// </summary>
        protected virtual void ResetPointers()
        {
            // First set BasePointerByte
            var safeBuffer = _view.SafeMemoryMappedViewHandle;
            RuntimeHelpers.PrepareConstrainedRegions();
            _basePointerView = null;
            try
            {
                safeBuffer.AcquirePointer(ref _basePointerView);
            }
            finally
            {
                if (_basePointerView != null)
                {
                    safeBuffer.ReleasePointer();
                }
            }
            _basePointerView += _view.PointerOffset;
            _ptrCount = (long*)(_basePointerView + _headerReserveBytes);
            _ptrArray = _basePointerView + _headerReserveBytes + 8; // 8 for the Count position at _ptrCount
        }

        /// <summary>
        /// Read-only property describing how many elements are in the List.
        /// Same as _size in list.cs
        /// </summary>
        public long Count => Unsafe.Read<long>(_ptrCount);

        /// <summary>
        /// The capacity of this ListMmf (number of elements)
        /// </summary>
        public long Capacity
        {
            get => _capacity;
            set
            {
                if (value == _capacity)
                {
                    // no change
                    return;
                }
                if (IsReadOnly)
                {
                    throw new ListMmfException($"{nameof(Capacity)} cannot be set on this Read-Only list.");
                }
                if (value < Count)
                {
                    throw new ListMmfException($"{nameof(Capacity)} cannot be set to {value} because Count={Count}. Use Truncate() to reduce the size of this list.");
                }
                if (!_isFileBased)
                {
                    throw new ListMmfException("A memory (not-persisted) ListMmf can NOT be expanded or truncated.");
                }

                // Note that this method is called by TrimExcess() to shrink Capacity (but not below Count)
                var capacityBytes = CapacityElementsToBytes(value, _headerReserveBytes);
                _view?.Dispose();
                _view = null;
                _mmf?.Dispose();
                _mmf = null;
                _fileStream.SetLength(capacityBytes);
                _mmf = MemoryMappedFile.CreateFromFile(_fileStream, _mapName, capacityBytes, _access, HandleInheritability.None, true);
                ResetView();
            }
        }

        public T this[long index]
        {
            get
            {
                return Unsafe.Read<T>(_ptrArray + index * _sizeOfT);
            }
            set
            {
                if ((ulong)index >= (uint)Unsafe.Read<long>(_ptrCount))
                {
                    throw new ArgumentOutOfRangeException(nameof(index), Count, $"Maximum index is {Count - 1}");
                }
                Unsafe.Write(_ptrArray + index * _sizeOfT, value);
            }
        }

        object IList64.this[long index]
        {
            get => this[index];
            set
            {
                try
                {
                    this[index] = (T)value;
                }
                catch (InvalidCastException)
                {
                    throw new InvalidCastException($"Cannot cast {value.GetType()} to {typeof(T)}");
                }
            }
        }

        /// <summary>
        /// Adds the given object to the end of this list. The size of the list is
        /// increased by one. If required, the capacity of the list is doubled
        /// before adding the new element.
        /// </summary>
        /// <param name="item"></param>
        public void Add(T item)
        {
            var size = Unsafe.Read<long>(_ptrCount);
            if ((ulong)size < (ulong)Capacity)
            {
                Unsafe.Write(_ptrArray + size * _sizeOfT, item);
                Unsafe.Write(_ptrCount, size + 1); // Write Count AFTER the value, so other processes will get correct 
            }
            else
            {
                AddWithResize(item);
            }
        }

        // Non-inline from List.Add to improve its code quality as uncommon path
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddWithResize(T item)
        {
            var size = Unsafe.Read<long>(_ptrCount);
            EnsureCapacity(size + 1);
            Unsafe.Write((void*)_basePointerView[size * _sizeOfT], item);
            Unsafe.Write(_ptrCount, size + 1); // Write Count AFTER the value, so other processes will get correct 
        }

        long IList64.Add(object item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }
            try
            {
                Add((T)item);
            }
            catch (InvalidCastException)
            {
                throw new InvalidCastException($"Cannot cast {item.GetType()} to {typeof(T)}");
            }
            return Unsafe.Read<long>(_ptrCount) - 1;
        }

        /// <summary>
        /// Adds the elements of the given collection to the end of this array.
        /// If required, the capacity of this array is increased before adding the new elements.
        /// </summary>
        /// <param name="collection"></param>
        /// <exception cref="ListMmfException">if list won't fit</exception>
        public void AddRange(IEnumerable<T> collection)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds the elements of the given IReadOnlyList64 to the end of this array.
        /// If required, the capacity of this array is increased before adding the new elements.
        /// </summary>
        /// <param name="list"></param>
        /// <exception cref="ListMmfException">if list won't fit</exception>
        public void AddRange(IReadOnlyList64<T> list)
        {
            throw new NotImplementedException();
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        void ICollection64<T>.Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            throw new NotImplementedException();
        }

        public bool Contains(object value)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Array array, int index)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Copy a section of this list to the given array at the given index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        /// <param name="count"></param>
        public void CopyTo(int index, T[] array, int arrayIndex, long count)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Return an IReadOnlyList64 consisting of Count elements starting at lowerBound
        /// If count is NOT long.MaxValue, Count is fixed at count.
        /// If count is long.MaxValue, Count = list.Count - lowerBound.
        /// </summary>
        /// <param name="lowerBound">The start of the view of array included in this list.</param>
        /// <param name="count">long.MaxValue means the count is not fixed and Count is list.Count - lowerBound, growing or shrinking as list grows or shrinks</param>
        /// <returns></returns>
        public IReadOnlyList64<T> GetReadOnlyList64(long lowerBound, long count = long.MaxValue)
        {
            throw new NotImplementedException();
        }

        public long IndexOf(T item)
        {
            throw new NotImplementedException();
        }

        public long IndexOf(object value)
        {
            throw new NotImplementedException();
        }

        public void Insert(long index, T item)
        {
            throw new NotImplementedException();
        }

        public void Insert(long index, object value)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public void Remove(object value)
        {
            throw new NotImplementedException();
        }

        void IList64<T>.RemoveAt(long index)
        {
            throw new NotImplementedException();
        }

        void IList64.RemoveAt(long index)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Truncate Length to newCapacity elements.
        /// If no other writer or reader is accessing the file, this also reduces Capacity, the size of the file.
        /// This method is only allowed for the Writer, not the Reader.
        /// </summary>
        /// <param name="newLength"></param>
        /// <exception cref="NotSupportedException">The array is read-only.</exception>
        public void Truncate(long newLength)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        // Searches a section of the list for a given element using a binary search
        // algorithm. Elements of the list are compared to the search value using
        // the given IComparer interface. If comparer is null, elements of
        // the list are compared to the search value using the IComparable
        // interface, which in that case must be implemented by all elements of the
        // list and the given search value. This method assumes that the given
        // section of the list is already sorted; if this is not the case, the
        // result will be incorrect.
        //
        // The method returns the index of the given value in the list. If the
        // list does not contain the given value, the method returns a negative
        // integer. The bitwise complement operator (~) can be applied to a
        // negative result to produce the index of the first element (if any) that
        // is larger than the given search value. This is also the index at which
        // the search value should be inserted into the list in order for the list
        // to remain sorted.
        // 
        // The method uses the Array.BinarySearch method to perform the
        // search.
        // 
        public int BinarySearch(long index, long count, T item, IComparer<T> comparer)
        {
            throw new NotImplementedException();
        }

        public int BinarySearch(T item)
        {
            return BinarySearch(0, Count, item, null);
        }

        public int BinarySearch(T item, IComparer<T> comparer)
        {
            return BinarySearch(0, Count, item, comparer);
        }

        public bool Exists(Predicate<T> match)
        {
            return FindIndex(match) != -1;
        }

        public T Find(Predicate<T> match)
        {
            throw new NotImplementedException();

            //if( match == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.EndContractBlock();

            //for(int i = 0 ; i < _size; i++) {
            //    if(match(_items[i])) {
            //        return _items[i];
            //    }
            //}
            //return default(T);
        }

        public List<T> FindAll(Predicate<T> match)
        {
            throw new NotImplementedException();

            //if( match == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.EndContractBlock();

            //List<T> list = new List<T>(); 
            //for(int i = 0 ; i < _size; i++) {
            //    if(match(_items[i])) {
            //        list.Add(_items[i]);
            //    }
            //}
            //return list;
        }

        public int FindIndex(Predicate<T> match)
        {
            throw new NotImplementedException();

            //return FindIndex(0, _size, match);
        }

        public int FindIndex(int startIndex, Predicate<T> match)
        {
            throw new NotImplementedException();

            //return FindIndex(startIndex, _size - startIndex, match);
        }

        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            throw new NotImplementedException();

            //if( (uint)startIndex > (uint)_size ) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.startIndex, ExceptionResource.ArgumentOutOfRange_Index);                
            //}

            //if (count < 0 || startIndex > _size - count) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_Count);
            //}

            //if( match == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.Ensures(Contract.Result<int>() >= -1);
            //Contract.Ensures(Contract.Result<int>() < startIndex + count);
            //Contract.EndContractBlock();

            //int endIndex = startIndex + count;
            //for( int i = startIndex; i < endIndex; i++) {
            //    if( match(_items[i])) return i;
            //}
            //return -1;
        }

        public T FindLast(Predicate<T> match)
        {
            throw new NotImplementedException();

            //if( match == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.EndContractBlock();

            //for(int i = _size - 1 ; i >= 0; i--) {
            //    if(match(_items[i])) {
            //        return _items[i];
            //    }
            //}
            //return default(T);
        }

        public int FindLastIndex(Predicate<T> match)
        {
            throw new NotImplementedException();

            //Contract.Ensures(Contract.Result<int>() >= -1);
            //Contract.Ensures(Contract.Result<int>() < Count);
            //return FindLastIndex(_size - 1, _size, match);
        }

        public int FindLastIndex(int startIndex, Predicate<T> match)
        {
            throw new NotImplementedException();

            //Contract.Ensures(Contract.Result<int>() >= -1);
            //Contract.Ensures(Contract.Result<int>() <= startIndex);
            //return FindLastIndex(startIndex, startIndex + 1, match);
        }

        public int FindLastIndex(int startIndex, int count, Predicate<T> match)
        {
            throw new NotImplementedException();

            //if( match == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.Ensures(Contract.Result<int>() >= -1);
            //Contract.Ensures(Contract.Result<int>() <= startIndex);
            //Contract.EndContractBlock();

            //if(_size == 0) {
            //    // Special case for 0 length List
            //    if( startIndex != -1) {
            //        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.startIndex, ExceptionResource.ArgumentOutOfRange_Index);
            //    }
            //}
            //else {
            //    // Make sure we're not out of range            
            //    if ( (uint)startIndex >= (uint)_size) {
            //        ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.startIndex, ExceptionResource.ArgumentOutOfRange_Index);
            //    }
            //}

            //// 2nd have of this also catches when startIndex == MAXINT, so MAXINT - 0 + 1 == -1, which is < 0.
            //if (count < 0 || startIndex - count + 1 < 0) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_Count);
            //}

            //int endIndex = startIndex - count;
            //for( int i = startIndex; i > endIndex; i--) {
            //    if( match(_items[i])) {
            //        return i;
            //    }
            //}
            //return -1;
        }

        public void ForEach(Action<T> action)
        {
            throw new NotImplementedException();

            //if( action == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.EndContractBlock();

            //int version = _version;

            //for(int i = 0 ; i < _size; i++) {
            //    if (version != _version && BinaryCompatibility.TargetsAtLeast_Desktop_V4_5) {
            //        break;
            //    }
            //    action(_items[i]);
            //}

            //if (version != _version && BinaryCompatibility.TargetsAtLeast_Desktop_V4_5)
            //    ThrowHelper.ThrowInvalidOperationException(ExceptionResource.InvalidOperation_EnumFailedVersion);
        }

        public List<T> GetRange(int index, int count)
        {
            throw new NotImplementedException();

            //if (index < 0) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}

            //if (count < 0) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}

            //if (_size - index < count) {
            //    ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);                
            //}
            //Contract.Ensures(Contract.Result<List<T>>() != null);
            //Contract.EndContractBlock();

            //List<T> list = new List<T>(count);
            //Array.Copy(_items, index, list._items, 0, count);            
            //list._size = count;
            //return list;
        }

        // Inserts the elements of the given collection at a given index. If
        // required, the capacity of the list is increased to twice the previous
        // capacity or the new size, whichever is larger.  Ranges may be added
        // to the end of the list by setting index to the List's size.
        //
        public void InsertRange(int index, IEnumerable<T> collection)
        {
            throw new NotImplementedException();

            //if (collection==null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.collection);
            //}

            //if ((uint)index > (uint)_size) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_Index);
            //}
            //Contract.EndContractBlock();

            //ICollection<T> c = collection as ICollection<T>;
            //if( c != null ) {    // if collection is ICollection<T>
            //    int count = c.Count;
            //    if (count > 0) {
            //        EnsureCapacity(_size + count);
            //        if (index < _size) {
            //            Array.Copy(_items, index, _items, index + count, _size - index);
            //        }

            //        // If we're inserting a List into itself, we want to be able to deal with that.
            //        if (this == c) {
            //            // Copy first part of _items to insert location
            //            Array.Copy(_items, 0, _items, index, index);
            //            // Copy last part of _items back to inserted location
            //            Array.Copy(_items, index+count, _items, index*2, _size-index);
            //        }
            //        else {
            //            T[] itemsToInsert = new T[count];
            //            c.CopyTo(itemsToInsert, 0);
            //            itemsToInsert.CopyTo(_items, index);                    
            //        }
            //        _size += count;
            //    }                
            //}
            //else {
            //    using(IEnumerator<T> en = collection.GetEnumerator()) {
            //        while(en.MoveNext()) {
            //            Insert(index++, en.Current);                                    
            //        }                
            //    }
            //}
            //_version++;            
        }

        // Returns the index of the last occurrence of a given value in a range of
        // this list. The list is searched backwards, starting at the end 
        // and ending at the first element in the list. The elements of the list 
        // are compared to the given value using the Object.Equals method.
        // 
        // This method uses the Array.LastIndexOf method to perform the
        // search.
        // 
        public int LastIndexOf(T item)
        {
            throw new NotImplementedException();

            //Contract.Ensures(Contract.Result<int>() >= -1);
            //Contract.Ensures(Contract.Result<int>() < Count);
            //if (_size == 0) {  // Special case for empty list
            //    return -1;
            //}
            //else {
            //    return LastIndexOf(item, _size - 1, _size);
            //}
        }

        // Returns the index of the last occurrence of a given value in a range of
        // this list. The list is searched backwards, starting at index
        // index and ending at the first element in the list. The 
        // elements of the list are compared to the given value using the 
        // Object.Equals method.
        // 
        // This method uses the Array.LastIndexOf method to perform the
        // search.
        // 
        public int LastIndexOf(T item, int index)
        {
            throw new NotImplementedException();

            //if (index >= _size)
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_Index);
            //Contract.Ensures(Contract.Result<int>() >= -1);
            //Contract.Ensures(((Count == 0) && (Contract.Result<int>() == -1)) || ((Count > 0) && (Contract.Result<int>() <= index)));
            //Contract.EndContractBlock();
            //return LastIndexOf(item, index, index + 1);
        }

        // Returns the index of the last occurrence of a given value in a range of
        // this list. The list is searched backwards, starting at index
        // index and upto count elements. The elements of
        // the list are compared to the given value using the Object.Equals
        // method.
        // 
        // This method uses the Array.LastIndexOf method to perform the
        // search.
        // 
        public int LastIndexOf(T item, int index, int count)
        {
            throw new NotImplementedException();

            //if ((Count != 0) && (index < 0)) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}

            //if ((Count !=0) && (count < 0)) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}
            //Contract.Ensures(Contract.Result<int>() >= -1);
            //Contract.Ensures(((Count == 0) && (Contract.Result<int>() == -1)) || ((Count > 0) && (Contract.Result<int>() <= index)));
            //Contract.EndContractBlock();

            //if (_size == 0) {  // Special case for empty list
            //    return -1;
            //}

            //if (index >= _size) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_BiggerThanCollection);
            //}

            //if (count > index + 1) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_BiggerThanCollection);
            //} 

            //return Array.LastIndexOf(_items, item, index, count);
        }

        // This method removes all items which matches the predicate.
        // The complexity is O(n).   
        public int RemoveAll(Predicate<T> match)
        {
            throw new NotImplementedException();

            //if( match == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.Ensures(Contract.Result<int>() >= 0);
            //Contract.Ensures(Contract.Result<int>() <= Contract.OldValue(Count));
            //Contract.EndContractBlock();

            //int freeIndex = 0;   // the first free slot in items array

            //// Find the first item which needs to be removed.
            //while( freeIndex < _size && !match(_items[freeIndex])) freeIndex++;            
            //if( freeIndex >= _size) return 0;

            //int current = freeIndex + 1;
            //while( current < _size) {
            //    // Find the first item which needs to be kept.
            //    while( current < _size && match(_items[current])) current++;            

            //    if( current < _size) {
            //        // copy item to the free slot.
            //        _items[freeIndex++] = _items[current++];
            //    }
            //}                       

            //Array.Clear(_items, freeIndex, _size - freeIndex);
            //int result = _size - freeIndex;
            //_size = freeIndex;
            //_version++;
            //return result;
        }

        // Removes a range of elements from this list.
        // 
        public void RemoveRange(int index, int count)
        {
            throw new NotImplementedException();

            //if (index < 0) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}

            //if (count < 0) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}

            //if (_size - index < count)
            //    ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
            //Contract.EndContractBlock();

            //if (count > 0) {
            //    int i = _size;
            //    _size -= count;
            //    if (index < _size) {
            //        Array.Copy(_items, index + count, _items, index, _size - index);
            //    }
            //    Array.Clear(_items, _size, count);
            //    _version++;
            //}
        }

        // Reverses the elements in this list.
        public void Reverse()
        {
            throw new NotImplementedException();

            //Reverse(0, Count);
        }

        // Reverses the elements in a range of this list. Following a call to this
        // method, an element in the range given by index and count
        // which was previously located at index i will now be located at
        // index index + (index + count - i - 1).
        // 
        // This method uses the Array.Reverse method to reverse the
        // elements.
        // 
        public void Reverse(int index, int count)
        {
            throw new NotImplementedException();

            //if (index < 0) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}

            //if (count < 0) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}

            //if (_size - index < count)
            //    ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
            //Contract.EndContractBlock();
            //Array.Reverse(_items, index, count);
            //_version++;
        }

        // Sorts the elements in this list.  Uses the default comparer and 
        // Array.Sort.
        public void Sort()
        {
            Sort(0, Count, null);
        }

        // Sorts the elements in this list.  Uses Array.Sort with the
        // provided comparer.
        public void Sort(IComparer<T> comparer)
        {
            Sort(0, Count, comparer);
        }

        // Sorts the elements in a section of this list. The sort compares the
        // elements to each other using the given IComparer interface. If
        // comparer is null, the elements are compared to each other using
        // the IComparable interface, which in that case must be implemented by all
        // elements of the list.
        // 
        // This method uses the Array.Sort method to sort the elements.
        // 
        public void Sort(long index, long count, IComparer<T> comparer)
        {
            throw new NotImplementedException();

            //if (index < 0) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.index, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}

            //if (count < 0) {
            //    ThrowHelper.ThrowArgumentOutOfRangeException(ExceptionArgument.count, ExceptionResource.ArgumentOutOfRange_NeedNonNegNum);
            //}

            //if (_size - index < count)
            //    ThrowHelper.ThrowArgumentException(ExceptionResource.Argument_InvalidOffLen);
            //Contract.EndContractBlock();

            //Array.Sort<T>(_items, index, count, comparer);
            //_version++;
        }

        public void Sort(Comparison<T> comparison)
        {
            //if( comparison == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.EndContractBlock();

            //if( _size > 0) {
            //    IComparer<T> comparer = new Array.FunctorComparer<T>(comparison);
            //    Array.Sort(_items, 0, _size, comparer);
            //}
        }

        // ToArray returns a new Object array containing the contents of the List.
        // This requires copying the List, which is an O(n) operation.
        public T[] ToArray()
        {
            throw new NotImplementedException();

            //Contract.Ensures(Contract.Result<T[]>() != null);
            //Contract.Ensures(Contract.Result<T[]>().Length == Count);

            //T[] array = new T[_size];
            //Array.Copy(_items, 0, array, 0, _size);
            //return array;
        }

        /// <summary>
        /// Sets the capacity of this list to the size of the list. This method can
        /// be used to minimize a list's memory overhead once it is known that no
        /// new elements will be added to the list. To completely clear a list and
        /// release all memory referenced by the list, execute the following
        /// statements:
        /// 
        /// list.Clear();
        /// list.TrimExcess();
        /// 
        /// </summary>
        public void TrimExcess()
        {
            var threshold = (long)(Capacity * 0.9);
            if (Count < threshold)
            {
                Capacity = Count;
            }
        }

        public bool TrueForAll(Predicate<T> match)
        {
            throw new NotImplementedException();

            //if( match == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.EndContractBlock();

            //for(int i = 0 ; i < _size; i++) {
            //    if( !match(_items[i])) {
            //        return false;
            //    }
            //}
            //return true;
        }

        // Ensures that the capacity of this list is at least the given minimum
        // value. If the correct capacity of the list is less than min, the
        // capacity is increased to twice the current capacity or to min,
        // whichever is larger.
        private void EnsureCapacity(long minCapacityElements)
        {
            if (IsReadOnly || minCapacityElements <= Count)
            {
                // nothing to do
                return;
            }

            // Grow by the smaller of Capacity (doubling file size) or 1 GB (we don't want to double a 500 GB file)
            var extraCapacity = Math.Min(Capacity, 1024 * 1024 * 1024);
            var newCapacityElements = Math.Max(_capacity + extraCapacity, minCapacityElements);
            Capacity = newCapacityElements;
        }

        private void SetSemaphoreUniqueName()
        {
            var cleanName = Name.RemoveCharFromString(Path.DirectorySeparatorChar);
            cleanName = cleanName.RemoveCharFromString(',');
            cleanName = cleanName.RemoveCharFromString(' ');
            var prefix = IsReadOnly ? "R-" : "W-";
            _semaphoreUniqueName = $"Global\\{prefix}{cleanName}";
            if (_semaphoreUniqueName.Length > 260)
            {
                throw new ListMmfException($"Too long semaphore name, exceeds 260: {_semaphoreUniqueName}");
            }
        }

        private void SetLocking(bool noLocking)
        {
            if (noLocking || IsReadOnly && _sizeOfT <= 8)
            {
                // no locking
                _locker = new Locker();
            }
            else if (_sizeOfT > 8)
            {
                // Full SLOW locking
                _locker = new Locker(_semaphoreUniqueName);
            }
            else
            {
                // Monitor Enter/Exit so this Writer can handle recreating _mmf and _view
                Debug.Assert(!IsReadOnly && _sizeOfT <= 8);
                _locker = new Locker(SyncRoot);
            }
        }

        /// <summary>
        /// Initialize/Reset the _mmf file etc. 
        /// </summary>
        private void ResetView()
        {
            _view?.Dispose();
            _view = _mmf.CreateViewAccessor(0, 0, _access);
            if (_isFileBased && _fileStream.Length != CapacityBytes)
            {
                // Set the file length up to the view length so we don't write off the end
                _fileStream.SetLength(CapacityBytes);
            }
            ResetPointers();
            _capacity = CapacityBytesToElements(CapacityBytes, _headerReserveBytes);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_isFileBased)
                {
                    TrimExcess();
                }
                _view?.Dispose();
                _view = null;
                _mmf.Dispose();
                _mmf = null;
                if (!_leaveOpen)
                {
                    _fileStream?.Dispose();
                    _fileStream = null;
                }
            }
            base.Dispose(disposing);
        }

        public override string ToString()
        {
            var basedOnStr = _isFileBased ? "File" : "Memory";
            var count = Unsafe.Read<long>(_ptrCount);
            var result = $"{basedOnStr} {count:N0}/{Capacity:N0} {AccessName}";
#if DEBUG
            result += base.ToString();
#endif
            return result;
        }
    }
}
