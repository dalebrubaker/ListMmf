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
    //[DebuggerDisplay("{" + nameof(AccessName) + ",nq} {" + nameof(Capacity) + ",nq}")]
    public unsafe partial class ListMmf<T> : ListMmf, IList64Disposable<T>, IList64, IReadOnlyList64Disposable<T> where T : struct
    {
        private readonly long _headerReserveBytes;
        private readonly MemoryMappedFileAccess _access;
        private readonly Semaphore _semaphore;
        private Mutex _mutex; // for locking when T > 8

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
        private bool _isDisposed;

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
        /// <param name="semaphore">The named semaphore unique to this Name and access</param>
        /// <param name="headerReserveBytes">Bytes to reserve in header. Evenly divisible by 8 for alignment</param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        /// <param name="mmf"></param>
        /// <param name="access">Only Read and ReadWrite are allowed</param>
        /// <param name="fileStream"><c>null</c> means Memory not File-backed</param>
        /// <param name="mapName"><c>null</c> with non-null fileStream means created from file but not sharing</param>
        /// <param name="leaveOpen"></param>
        protected ListMmf(Semaphore semaphore, long headerReserveBytes, bool noLocking, MemoryMappedFile mmf, MemoryMappedFileAccess access, FileStream fileStream, string mapName,
            bool leaveOpen = false)
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
            _semaphore = semaphore;
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
        public long Count
        {
            get
            {
                using (_locker.Lock())
                {
                    return Unsafe.Read<long>(_ptrCount);
                }
            }
            private set
            {
                using (_locker.Lock())
                {
                    Unsafe.Write(_ptrCount, value);
                }
            }
        }

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
                using (_locker.Lock())
                {
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
        }

        public T this[long index]
        {
            get
            {
                using (_locker.Lock())
                {
                    // Following trick can reduce the range check by one
                    if ((ulong)index >= (uint)Unsafe.Read<long>(_ptrCount))
                    {
                        throw new ArgumentOutOfRangeException(nameof(index), Count, $"Maximum index is {Count - 1}");
                    }
                    return Unsafe.Read<T>(_ptrArray + index * _sizeOfT);
                }
            }
            set
            {
                using (_locker.Lock())
                {
                    if ((ulong)index >= (uint)Unsafe.Read<long>(_ptrCount))
                    {
                        throw new ArgumentOutOfRangeException(nameof(index), Count, $"Maximum index is {Count - 1}");
                    }
                    Unsafe.Write(_ptrArray + index * _sizeOfT, value);
                }
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
            using (_locker.Lock())
            {
                EnsureCapacity(Count + 1);
                Unsafe.Write(_ptrArray + Count * _sizeOfT, item);
                Count++; // Change Count AFTER the value, so other processes will get correct
            }
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
            using (_locker.Lock())
            {
                return Count - 1;
            }
        }

        /// <summary>
        /// Adds the elements of the given collection to the end of this array.
        /// If required, the capacity of this array is increased before adding the new elements.
        /// </summary>
        /// <param name="collection"></param>
        /// <exception cref="ListMmfException">if list won't fit</exception>
        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }
            using (_locker.Lock())
            {
                var currentCount = Count;
                long count;
                switch (collection)
                {
                    case IList<T> list:
                        EnsureCapacity(currentCount + list.Count);
                        for (int i = 0; i < list.Count; i++)
                        {
                            Unsafe.Write(_ptrArray + currentCount++ * _sizeOfT, list[i]);
                        }
                        break;
                    case ICollection<T> c:
                        EnsureCapacity(currentCount + c.Count);
                        foreach (var item in collection)
                        {
                            Unsafe.Write(_ptrArray + currentCount++ * _sizeOfT, item);
                        }
                        break;
                    case ICollection64<T> c64:
                        EnsureCapacity(currentCount + c64.Count);
                        foreach (var item in collection)
                        {
                            Unsafe.Write(_ptrArray + currentCount++ * _sizeOfT, item);
                        }
                        break;
                    default:
                        using (var en = collection.GetEnumerator())
                        {
                            // Do inline Add
                            Add(en.Current);
                            while (en.MoveNext())
                            {
                                EnsureCapacity(currentCount + 1);
                                Unsafe.Write(_ptrArray + currentCount * _sizeOfT, en.Current);
                            }
                        }
                        return;
                }
                Count = currentCount; // Set this last so readers won't access items before they are written
            }
        }

        /// <summary>
        /// Adds the elements of the given IReadOnlyList64 to the end of this array.
        /// If required, the capacity of this array is increased before adding the new elements.
        /// </summary>
        /// <param name="list"></param>
        /// <exception cref="ListMmfException">if list won't fit</exception>
        public void AddRange(IReadOnlyList64<T> list)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        public ReadOnlyListMmf<T> AsReadOnly() => new ReadOnlyListMmf<T>(this);

        public void Clear()
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        void ICollection64<T>.Clear()
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Contains returns true if the specified element is in the List.
        /// It does a linear, O(n) search.  Equality is determined by calling
        /// EqualityComparer<T>.Default.Equals().
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(T item)
        {
            using (_locker.Lock())
            {
                return Count != 0 && IndexOf(item) != -1;
            }
        }

        /// <summary>
        /// Contains returns true if the specified element is in the List.
        /// It does a linear, O(n) search.  Equality is determined by calling
        /// EqualityComparer<T>.Default.Equals().
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool Contains(object item)
        {
            if (IsCompatibleObject(item))
            {
                return Contains((T)item);
            }
            return false;
        }

        /// <summary>
        /// Copies this List into array, which must be of a compatible array type.
        /// </summary>
        /// <param name="array"></param>
        public void CopyTo(T[] array) => CopyTo(array, 0);

        /// <summary>
        /// Copies this List into array, which must be of a compatible array type.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            // Throw exception if Count is greater than int.MaxValue
            var count = (int)Count;
            CopyTo(0, array, 0, count);
        }

        /// <summary>
        /// Copies this List into array, which must be of a compatible array type.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        public void CopyTo(Array array, int arrayIndex)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Copy a section of this list to the given array at the given index.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        /// <param name="count"></param>
        public void CopyTo(int index, T[] array, int arrayIndex, int count)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }
            if (array.Rank != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(CopyTo), "Multi-Dimensional Array Rank is not supported.");
            }
            using (_locker.Lock())
            {
                if (Count - index < count)
                {
                    throw new ArgumentException($"There are not {count:N0} elements starting at {index:N0} in this list of {Count:N0} elements");
                }
                var ptr = _ptrArray + index * _sizeOfT;
                for (int i = 0; i < count; i++)
                {
                    var value = Unsafe.Read<T>(ptr);
                    array[i] = value;
                    ptr += _sizeOfT;
                }
            }

            // Delegate rest of error checking to Array.Copy.
            //Array.Copy(_items, index, array, arrayIndex, count);
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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the index of the first occurrence of a given value in a range of
        /// this list. The list is searched forwards from beginning to end.
        /// The elements of the list are compared to the given value using the
        /// Object.Equals method.
        /// Returns the index of the first occurrence of a given value in a range of
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public long IndexOf(T item)
        {
            using (_locker.Lock())
            {
                for (long i = 0L; i < Count; i++)
                {
                    var value = Unsafe.Read<T>(_ptrArray + i * _sizeOfT);
                    if (item.Equals(value))
                    {
                        return i;
                    }
                }
                return -1;
            }
        }

        /// <summary>
        /// Returns the index of the first occurrence of a given value in a range of
        /// this list. The list is searched forwards from beginning to end.
        /// The elements of the list are compared to the given value using the
        /// Object.Equals method.
        /// Returns the index of the first occurrence of a given value in a range of
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public long IndexOf(object item)
        {
            if (IsCompatibleObject(item))
            {
                return IndexOf((T)item);
            }
            return -1;
        }

        public void Insert(long index, T item)
        {
            using (_locker.Lock())
            {
                // Copy will EnsureCapacity()
                Copy(index, index + 1, 1);
                var destination = _ptrArray + index * _sizeOfT;
                Unsafe.Write(destination, item);

                // Remember tht Copy already updated Count if required
            }
        }

        public void Insert(long index, object item)
        {
            try
            {
                Insert(index, (T)item);
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException(nameof(item), "Unable to cast object to T.");
            }
        }

        public bool Remove(T item)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        public void Remove(object value)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        void IList64<T>.RemoveAt(long index)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        void IList64.RemoveAt(long index)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => new Enumerator(this);

        public IEnumerator<T> GetEnumerator() => new Enumerator(this);

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }
        }

        public int BinarySearch(T item)
        {
            using (_locker.Lock())
            {
                return BinarySearch(0, Count, item, null);
            }
        }

        public int BinarySearch(T item, IComparer<T> comparer)
        {
            using (_locker.Lock())
            {
                return BinarySearch(0, Count, item, comparer);
            }
        }

        public bool Exists(Predicate<T> match)
        {
            using (_locker.Lock())
            {
                return FindIndex(match) != -1;
            }
        }

        public T Find(Predicate<T> match)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

            //return FindIndex(0, _size, match);
        }

        public int FindIndex(int startIndex, Predicate<T> match)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

            //return FindIndex(startIndex, _size - startIndex, match);
        }

        public int FindIndex(int startIndex, int count, Predicate<T> match)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

            //Contract.Ensures(Contract.Result<int>() >= -1);
            //Contract.Ensures(Contract.Result<int>() < Count);
            //return FindLastIndex(_size - 1, _size, match);
        }

        public int FindLastIndex(int startIndex, Predicate<T> match)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

            //Contract.Ensures(Contract.Result<int>() >= -1);
            //Contract.Ensures(Contract.Result<int>() <= startIndex);
            //return FindLastIndex(startIndex, startIndex + 1, match);
        }

        public int FindLastIndex(int startIndex, int count, Predicate<T> match)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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

        public List<T> GetRange(long index, long count)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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

        /// <summary>
        /// Copy count values beginning at sourceIndex to destinationIndex.
        /// Resets Count if we are copying past the current Count
        /// Handles overlapping range.
        /// Not in List(T) API
        /// </summary>
        /// <param name="sourceIndex"></param>
        /// <param name="destinationIndex"></param>
        /// <param name="count"></param>
        public void Copy(long sourceIndex, long destinationIndex, long count)
        {
            if (count == 0 || destinationIndex == sourceIndex)
            {
                return;
            }
            if (sourceIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex), sourceIndex, $"Must not 0 or greater.");
            }
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, $"Must not 0 or greater.");
            }
            if (destinationIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(destinationIndex), destinationIndex, $"Must not 0 or greater.");
            }
            using (_locker.Lock())
            {
                var newCount = Math.Max(Count, destinationIndex + count);
                if (newCount > Count)
                {
                    EnsureCapacity(newCount);
                }
                var isOverlapping = IsOverlapping(sourceIndex, destinationIndex, count);
                if (isOverlapping)
                {
                    if (destinationIndex > sourceIndex)
                    {
                        // Copying forwards. Copy the overlap area FORWARDS one item at at time
                        while (destinationIndex <= sourceIndex + count - 1)
                        {
                            var value = Unsafe.Read<T>(_ptrArray + sourceIndex * _sizeOfT);
                            Unsafe.Write(_ptrArray + destinationIndex * _sizeOfT, value);
                            sourceIndex++;
                            count--;
                        }
                    }
                    else
                    {
                        // Copying backwards. Copy the overlap area BACKWARDS one item at at time
                        while (destinationIndex + count - 1 >= sourceIndex)
                        {
                            var fromIndex = sourceIndex + count - 1;
                            var value = Unsafe.Read<T>(_ptrArray + fromIndex * _sizeOfT);
                            var toIndex = destinationIndex + count - 1;
                            Unsafe.Write(_ptrArray + toIndex * _sizeOfT, value);
                            count--;
                        }
                    }
                }
                
                // Copy the non-overlapping block 
                CopyBlock(sourceIndex, destinationIndex, count);
                if (Count > 0)
                {
                    CopyBlock(sourceIndex, count, destinationIndex);
                }
                if (newCount > Count)
                {
                    // Increase Count to reflect the end of the copied values
                    Count = newCount; // Set this last so readers won't access items before they are written
                }
            }
        }

        /// <summary>
        /// Copy count items starting at sourceIndex to destinationIndex, int.MaxValue bytes at a time.
        /// Does NOT handle overlaps
        /// </summary>
        /// <param name="sourceIndex"></param>
        /// <param name="destinationIndex"></param>
        /// <param name="count"></param>
        private void CopyBlock(long sourceIndex, long destinationIndex, long count)
        {
            var isOverlapping = IsOverlapping(sourceIndex, destinationIndex, count);
            if (isOverlapping)
            {
                // Block copy would corrupt data
                throw new ArgumentException($"{nameof(CopyBlock)} cannot copy overlapping regions.");
            }
            var byteCount = count * _sizeOfT;
            var bytesCopiedSoFar = 0L;
            var source = _ptrArray + sourceIndex * _sizeOfT;
            var destination = _ptrArray + destinationIndex * _sizeOfT;
            do
            {
                // Limit copies to uint.MaxValue because Unsafe.CopyBlock can copy only uint.MaxValue at a time
                var bytesToCopy = Math.Min(byteCount - bytesCopiedSoFar, uint.MaxValue);
                Unsafe.CopyBlock(destination, source, (uint)bytesToCopy);
                bytesCopiedSoFar += bytesToCopy;
                source += bytesToCopy;
                destination += bytesToCopy;
            } while (bytesCopiedSoFar < byteCount);
        }

        private bool IsOverlapping(in long sourceIndex, in long destinationIndex, in long count)
        {
            if (destinationIndex >= sourceIndex && destinationIndex <= sourceIndex + count - 1)
            {
                // Copying forwards and overlaps
                return true;
            }
            if (destinationIndex <= sourceIndex && destinationIndex + count - 1 >= sourceIndex)
            {
                // Copying backwards and overlaps
                return true;
            }
            return false;
        }

        /// <summary>
        /// Inserts the elements of the given collection at a given index. If
        /// required, the capacity of the list is increased to twice the previous
        /// capacity or the new size (Count), whichever is larger.  Ranges may be added
        /// to the end of the list by setting index to the List's size (Count).
        /// </summary>
        /// <param name="index"></param>
        /// <param name="collection"></param>
        public void InsertRange(long index, IEnumerable<T> collection)
        {
            if (collection == null)
            {
                throw new ArgumentNullException(nameof(collection));
            }
            using (_locker.Lock())
            {
                if ((ulong)index > (ulong)Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(collection));
                }
                
                // TODO Handle IList<T> as in AddRange()
                
                
                long count;
                bool isThis;
                switch (collection)
                {
                    case ICollection64<T> c64:
                        count = c64.Count;
                        isThis = this == c64;
                        break;
                    case ICollection<T> c:
                        count = c.Count;
                        isThis = false;
                        break;
                    default:
                        using (IEnumerator<T> en = collection.GetEnumerator())
                        {
                            while (en.MoveNext())
                            {
                                Insert(index++, en.Current);
                            }
                        }
                        return;
                }
                if (count > 0)
                {
                    EnsureCapacity(Count + count);
                    if (index < Count)
                    {
                        // Copy items starting at index to make room for the collection
                        Copy(index, index + count, count);
                    }

                    // If we're inserting a List into itself, we want to be able to deal with that.
                    if (isThis)
                    {
                        // Copy first part of _items to insert location
                        Copy(0, index, index);

                        //Array.Copy(_items, 0, _items, index, index);

                        // Copy last part of _items back to inserted location
                        Copy(index + count, index * 2, Count - index);

                        //Array.Copy(_items, index + count, _items, index * 2, size - index);
                    }
                    else
                    {
                        // Copy the collection into the list beginning at index
                        var destination = _ptrArray + index * _sizeOfT;
                        foreach (var value in collection)
                        {
                            Unsafe.Write(destination, value);
                            destination += _sizeOfT;
                        }

                        //c.CopyTo(_items, index);
                    }
                    Count += count;
                }
            }
        }

        /// <summary>
        /// Returns the index of the last occurrence of a given value in a range of
        /// this list. The list is searched backwards, starting at the end 
        /// and ending at the first element in the list. The elements of the list 
        /// are compared to the given value using the Object.Equals method.
        /// 
        /// This method uses the Array.LastIndexOf method to perform the
        /// search.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public int LastIndexOf(T item)
        {
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                Sort(0, Count, null);
            }
        }

        // Sorts the elements in this list.  Uses Array.Sort with the
        // provided comparer.
        public void Sort(IComparer<T> comparer)
        {
            using (_locker.Lock())
            {
                Sort(0, Count, comparer);
            }
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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

            //if( comparison == null) {
            //    ThrowHelper.ThrowArgumentNullException(ExceptionArgument.match);
            //}
            //Contract.EndContractBlock();

            //if( _size > 0) {
            //    IComparer<T> comparer = new Array.FunctorComparer<T>(comparison);
            //    Array.Sort(_items, 0, _size, comparer);
            //}
        }

        /// <summary>
        /// ToArray returns a new Object array containing the contents of the List.
        /// This requires copying the List, which is an O(n) operation.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public T[] ToArray()
        {
            if (Count > int.MaxValue)
            {
                throw new NotSupportedException("ToArray() is not possible when Count is higher than int.MaxValue");
            }
            using (_locker.Lock())
            {
                var result = new T[Count];
                for (int i = 0; i < Count; i++)
                {
                    result[i] = Unsafe.Read<T>(_ptrArray + i * _sizeOfT);
                }
                return result;
            }
        }

        /// <summary>
        /// ToList returns a new List  containing the contents of the List.
        /// This requires copying the List, which is an O(n) operation.
        /// Not in the List API
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public List<T> ToList()
        {
            if (Count > int.MaxValue)
            {
                throw new NotSupportedException("ToArray() is not possible when Count is higher than int.MaxValue");
            }
            using (_locker.Lock())
            {
                var result = new List<T>((int)Count);
                for (int i = 0; i < Count; i++)
                {
                    result.Add(Unsafe.Read<T>(_ptrArray + i * _sizeOfT));
                }
                return result;
            }
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
            using (_locker.Lock())
            {
                throw new NotImplementedException();
            }

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
            if (IsReadOnly || minCapacityElements <= _capacity)
            {
                // nothing to do
                return;
            }

            // Grow by the smaller of Capacity (doubling file size) or 1 GB (we don't want to double a 500 GB file)
            var extraCapacity = Math.Min(Capacity, 1024 * 1024 * 1024);
            var newCapacityElements = Math.Max(_capacity + extraCapacity, minCapacityElements);
            Capacity = newCapacityElements; // Use the property to reset _mmf etc. if needed
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
                var mutexName = "M-" + GetSemaphoreName(Name, IsReadOnly);
                _mutex = new Mutex(false, mutexName);
                _locker = new Locker(_mutex);
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
            using (_locker.Lock())
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
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                using (_locker.Lock())
                {
                    _isDisposed = true;
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
                    _mutex?.Dispose();
                    DisposeSemaphore();
                }
            }
            base.Dispose(disposing);
        }

        private void DisposeSemaphore()
        {
            try
            {
                // _semaphore must be owned by the thread in order to block for Open methods
                // But sometimes another thread will dispose
                var count = _semaphore?.Release(1);
            }
            catch (SemaphoreFullException)
            {
                // ??? ignore this, always happen when we aren't opening exclusive
            }
            finally
            {
                _semaphore?.Dispose();
            }
        }

        public override string ToString()
        {
            var basedOnStr = _isFileBased ? "File" : "Memory";
            var count = _isDisposed || _ptrCount == null ? 0 : Count;
            var result = $"{basedOnStr} {count:N0}/{_capacity:N0} {AccessName}";
#if DEBUG
            result += base.ToString();
#endif
            return result;
        }

        public struct Enumerator : IEnumerator<T>, IEnumerator
        {
            private readonly ListMmf<T> _list;
            private long _index;
            private T _current;

            internal Enumerator(ListMmf<T> list)
            {
                _list = list;
                _index = 0;
                _current = default;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                var localList = _list;

                if ((ulong)_index < (ulong)localList.Count)
                {
                    _current = localList[_index];
                    _index++;
                    return true;
                }
                return MoveNextRare();
            }

            private bool MoveNextRare()
            {
                _index = _list.Count + 1;
                _current = default;
                return false;
            }

            public T Current => _current;

            object IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || _index == _list.Count + 1)
                    {
                        throw new InvalidOperationException("Can't happen!");
                    }
                    return Current;
                }
            }

            void IEnumerator.Reset()
            {
                _index = 0;
                _current = default;
            }
        }
    }
}
