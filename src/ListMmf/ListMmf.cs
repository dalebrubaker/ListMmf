using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BruSoftware.ListMmf
{
    public unsafe class ListMmf<T> : IList64<T>, IList64, IReadOnlyList64<T>, IDisposable, IEnumerable where T : struct
    {
        internal const int DefaultSize = 0;
        private readonly MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _view;

        /// <summary>
        /// Null means this class is memory-based, not-persisted
        /// Not-null means this class is file-based, persisted, like CreateFromFile()
        /// </summary>
        private FileStream _fileStream;

        private bool _leaveOpen;

        /// <summary>
        /// Not-null means system-wide
        /// </summary>
        private string _mapName;

        /// <summary>
        /// The size of T
        /// </summary>
        private int _sizeOfT;

        /// <summary>
        /// This is the beginning of the View, before the headerReserveBytes and the 8-byte Length of this array.
        /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
        ///     I don't know why they do that, and I don't seem to need it...
        /// </summary>
        protected byte* BasePointerByte;

        public bool IsSynchronized => false;
        public object SyncRoot { get; } = new object();
        public long Count { get; }

        public long Size { get; set; }

        /// <summary>
        /// The capacity of this ListMmf (number of elements)
        /// </summary>
        public long Capacity
        {
            get => CapacityBytes / _sizeOfT;
            set => throw new NotImplementedException(); // reset Mmf etc.
        }

        /// <summary>
        /// The capacity in bytes. Note that for persisted MMFs, the ByteLength can exceed _fileStream.Length, so writes to that region may be going to never-never-land.
        /// </summary>
        public long CapacityBytes => _fileStream?.Length ?? (long)_view.SafeMemoryMappedViewHandle.ByteLength;

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool IsReadOnly { get; }
        public bool IsFixedSize => false;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerReserveBytes"></param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        /// <param name="mmf"></param>
        private ListMmf(long headerReserveBytes, bool noLocking, MemoryMappedFile mmf)
        {
            _mmf = mmf;

            if (!Environment.Is64BitOperatingSystem)
                throw new Exception("Not supported on 32-bit operating system. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            if (!Environment.Is64BitProcess) throw new Exception("Not supported on 32-bit process. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");

            //_mmf = MemoryMappedFile.CreateFromFile("Test", FileMode.Append, "mapName", 1000);
        }

        /// <summary>
        /// Ret/Reset the MMF file etc. 
        /// </summary>
        private void Initialize()
        {
            Dispose(true);

            // TODO build/rebuild

            ResetPointers();
        }

        /// <summary>
        /// This method is called whenever Mmf and View are changed.
        /// Inheritors should first call base.ResetPointers() and then reset their own pointers (if any) from BasePointerByte
        /// </summary>
        public virtual void ResetPointers()
        {
            BasePointerByte = GetPointer(null);
        }

        public static ListMmf<T> CreateFromFile(string path, string mapName = null, long capacityElements = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            bool existed = File.Exists(path);
            FileStream fileStream = CreateFileStreamFromPath(path, access);
            if (capacityElements == 0 && fileStream.Length == 0)
            {
                CleanupFile(fileStream, existed, path);
                throw new ArgumentException($"File at {path} is empty.");
            }
            if (access == MemoryMappedFileAccess.Read && capacityElements > fileStream.Length)
            {
                CleanupFile(fileStream, existed, path);
                throw new ArgumentException($"Read access capacity {capacityElements} is greater than file length {fileStream.Length}");
            }
            var sizeOfT = Unsafe.SizeOf<T>();
            var capacity = capacityElements * sizeOfT;
            if (fileStream.Length > capacity)
            {
                CleanupFile(fileStream, existed, path);
                throw new ArgumentOutOfRangeException(nameof(capacityElements), $"capacity {capacityElements} must be greater than file length {fileStream.Length}");
            }
            return CreateFromFile(fileStream, mapName, capacityElements, access, true, headerReserve, noLocking);
        }

        private static FileStream CreateFileStreamFromPath(string path, MemoryMappedFileAccess access)
        {
            if (string.IsNullOrEmpty(path)) throw new MmfException("A path is required.");
            var fileMode = access == MemoryMappedFileAccess.ReadWrite ? FileMode.OpenOrCreate : FileMode.Open;
            var fileAccess = access == MemoryMappedFileAccess.ReadWrite ? FileAccess.ReadWrite : FileAccess.Read;
            var fileShare = access == MemoryMappedFileAccess.ReadWrite ? FileShare.Read : FileShare.ReadWrite;
            if (access == MemoryMappedFileAccess.Read && !File.Exists(path)) throw new MmfException($"Attempt to read non-existing file: {path}");
            if (access == MemoryMappedFileAccess.ReadWrite)
            {
                // Create the directory if needed
                var directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            }
            try
            {
                var result = new FileStream(path, fileMode, fileAccess, fileShare);
                return result;
            }
            catch (Exception)
            {
                Thread.Sleep(1000);
                return new FileStream(path, fileMode, fileAccess, fileShare);
            }
        }

        // From ndp MemoryMappedFiles.cs
        // clean up: close file handle and delete files we created
        private static void CleanupFile(FileStream fileStream, bool existed, string path)
        {
            fileStream.Close();
            if (!existed) File.Delete(path);
        }

        public static ListMmf<T> CreateFromFile(FileStream fileStream, string mapName = null, long capacityElements = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, bool leaveOpen = false,
            long headerReserve = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            var sizeOfT = Unsafe.SizeOf<T>();
            var capacity = capacityElements * sizeOfT;
            var mmf = MemoryMappedFile.CreateFromFile(fileStream, mapName, capacity, access, HandleInheritability.None, leaveOpen);
            return new ListMmf<T>(headerReserve, noLocking, mmf);
        }

        public static ListMmf<T> OpenExisting(string mapName, MemoryMappedFileRights memoryMappedFileRights = MemoryMappedFileRights.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            if (memoryMappedFileRights != MemoryMappedFileRights.ReadWrite && memoryMappedFileRights != MemoryMappedFileRights.Read)
                throw new ArgumentOutOfRangeException(nameof(memoryMappedFileRights), "Only Read and ReadWrite are allowed.");
            return new ListMmf<T>(headerReserve, noLocking, null);
        }

        public static ListMmf<T> CreateNew(string mapName, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            return new ListMmf<T>(headerReserve, noLocking, null);
        }

        public static ListMmf<T> CreateOrOpen(string mapName, long capacity, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            return new ListMmf<T>(headerReserve, noLocking, null);
        }

        private byte* GetPointer(MemoryMappedViewAccessor mmva)
        {
            var safeBuffer = mmva.SafeMemoryMappedViewHandle;
            RuntimeHelpers.PrepareConstrainedRegions();
            byte* pointer = null;
            try
            {
                safeBuffer.AcquirePointer(ref pointer);
            }
            finally
            {
                if (pointer != null) safeBuffer.ReleasePointer();
            }
            pointer += mmva.PointerOffset;
            return pointer;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _view?.Dispose();
                _mmf?.Dispose();
                if (!_leaveOpen) _fileStream?.Dispose();
            }
        }

        public T this[long index]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        object IList64.this[long index]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public void Add(T item)
        {
            throw new NotImplementedException();
        }

        public long Add(object value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds the elements of the given collection to the end of this array.
        /// If required, the capacity of this array is increased before adding the new elements.
        /// </summary>
        /// <param name="collection"></param>
        /// <exception cref="MmfException">if list won't fit</exception>
        public void AddRange(IEnumerable<T> collection)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Adds the elements of the given IReadOnlyList64 to the end of this array.
        /// If required, the capacity of this array is increased before adding the new elements.
        /// </summary>
        /// <param name="list"></param>
        /// <exception cref="MmfException">if list won't fit</exception>
        public void AddRange(IReadOnlyList64<T> list)
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

        private static bool IsCompatibleObject(object value)
        {
            // Non-null values are fine.  Only accept nulls if T is a class or Nullable<U>.
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>. 
            return value is T;
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

        // Sets the capacity of this list to the size of the list. This method can
        // be used to minimize a list's memory overhead once it is known that no
        // new elements will be added to the list. To completely clear a list and
        // release all memory referenced by the list, execute the following
        // statements:
        // 
        // list.Clear();
        // list.TrimExcess();
        // 
        public void TrimExcess()
        {
            throw new NotImplementedException();

            //int threshold = (int)(((double)_items.Length) * 0.9);             
            //if( _size < threshold ) {
            //    Capacity = _size;                
            //}
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}