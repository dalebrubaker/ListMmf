using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security;
using NLog;
using Unsafe = System.Runtime.CompilerServices.Unsafe;

namespace BruSoftware.ListMmf
{
    /// <summary>
    /// This is the class from which inheritors should derive, NOT ListMmf which exposes more public methods than necessary
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public unsafe class ListMmfBase<T> : ListMmfBaseDebug where T : struct
    {
        /// <summary>
        /// This class only has Version, DataType and Count
        /// </summary>
        private const int HeaderBytesBase = 16;

        /// <summary>
        /// currently used by MemoryMappedFiles
        /// </summary>
        private const int PageSize = 4096;

        private readonly MemoryMappedFileAccess _access;
        private readonly long _parentHeaderBytes;
        private FileStream _fileStream;
        private bool _isDisposed;

        /// <summary>
        /// The size of T (number of bytes)
        /// </summary>
        private readonly int _width;

        public int WidthBits => _width * 8;

        /// <summary>
        /// The number of elements that can be held in the first page of the file
        /// </summary>
        public long CapacityFirstPage => (PageSize - HeaderBytesBase - _parentHeaderBytes) / _width;

        /// <summary>
        /// The number of elements that can be held in pages after the first page of the file
        /// </summary>
        public long CapacityPerPageAfterFirstPage => PageSize / _width;

        /// <summary>
        /// Path is null for a Reader
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// This is the field corresponding to Capacity (in items, not Bytes)
        /// </summary>
        protected long _capacity;

        private MemoryMappedFile _mmf;

        /// <summary>
        /// The long* into the Version location in the view (the first 4 bytes)
        /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
        ///     I don't know why they do that, and I don't seem to need it...
        /// </summary>
        private int* _ptrVersion;

        /// <summary>
        /// The long* into the DataType location in the view (the second 4 bytes)
        /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
        ///     I don't know why they do that, and I don't seem to need it...
        /// </summary>
        private int* _ptrDataType;

        /// <summary>
        /// The long* into the Count location in the view (the 8 bytes following Version and DataType)
        /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
        ///     I don't know why they do that, and I don't seem to need it...
        /// </summary>
        private long* _ptrCount;

        private MemoryMappedViewAccessor _view;

        /// <summary>
        /// This is the beginning of the View, before the parentHeaderBytes and the MyHeaderBytes header for this array.
        /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
        ///     I don't know why they do that, and I don't seem to need it...
        ///  internal instead of protected to avoid CLS-compliant warning
        /// </summary>
        protected byte* _basePointerView;

        /// <summary>
        /// This is the beginning of the Array, after the parentHeaderBytes and the MyHeaderBytes header for this array.
        /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
        ///     I don't know why they do that, and I don't seem to need it...
        ///  internal instead of protected to avoid CLS-compliant warning
        /// </summary>
        private byte* _ptrArray;

        /// <summary>
        /// Reader {Name} or Writer {Name}
        /// </summary>
        private readonly string _accessName;

        protected object SyncRoot { get; }

        public readonly bool IsReadOnly;

        /// <summary>
        /// Open the list in a MemoryMappedFile at path
        /// This ctor is used only by derived classes, to pass along parentHeaderBytes
        /// Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
        /// </summary>
        /// <param name="path">The path to open ReadWrite</param>
        /// <param name="capacityItems">The number of items to initialize the list.
        ///     If 0, will be set to some default amount for a new file. Is ignored for an existing one.</param>
        /// <param name="access">must be Read or ReadWrite</param>
        /// <param name="parentHeaderBytes"></param>
        /// <exception cref="ListMmfException"></exception>
        protected ListMmfBase(string path, long capacityItems, MemoryMappedFileAccess access, long parentHeaderBytes) : base(path)
        {
            if (parentHeaderBytes % 8 != 0)
            {
                // Not sure this is a necessary limitation
                throw new ListMmfException($"{nameof(parentHeaderBytes)} is required to be a multiple of 8 bytes.");
            }
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            if (!Environment.Is64BitProcess)
            {
                throw new ListMmfException("Not supported on 32-bit process. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            }
            _width = Unsafe.SizeOf<T>();
            _access = access;
            IsReadOnly = _access == MemoryMappedFileAccess.Read;
            _accessName = (_access == MemoryMappedFileAccess.Read ? "Reader " : "Writer ") + path;
            SyncRoot = new object();
            Path = path;
            _parentHeaderBytes = parentHeaderBytes;

            // We want to write at least 1 page
            var fi = new FileInfo(path);
            long capacityBytes;
            if (fi.Exists)
            {
                // use the existing file length
                capacityBytes = 0; // 0 means the entire file
            }
            else
            {
                // The file doesn't exist
                if (!Directory.Exists(fi.DirectoryName))
                {
                    if (fi.DirectoryName != null)
                    {
                        UtilsListMmf.MyDirectoryCreateDirectory(fi.DirectoryName);
                    }
                }
                capacityBytes = CapacityItemsToBytes(capacityItems); // rounds up to PageSize, so we don't write off the end
            }
            CreateMmf(capacityBytes);
        }

        private void CreateMmf(long capacityBytes)
        {
            try
            {
                _fileStream = UtilsListMmf.CreateFileStreamFromPath(Path, _access);

                //Logger.ConditionalDebug($"Creating _mmf with capacityBytes={capacityBytes:N0} for {this}");
                // mapName must always be NULL! We can't use it when we are doing reader/writer using FileStream
                _mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, capacityBytes, _access, HandleInheritability.None, true);
            }
            catch (IOException exio)
            {
                Tracker.Deregister(TrackerId);
                if (_access == MemoryMappedFileAccess.ReadWrite)
                {
                    // Assume this exception is because the user checked to see if ReadWrite was available
                    throw new ReadWriteNotAvailableException();
                }
                Logger.Error(exio, exio.Message);
                throw;
            }
            catch (ReadWriteNotAvailableException exrw)
            {
                Logger.ConditionalDebug($"Ignoring {exrw.Message} for {Path}");
            }
            catch (Exception ex)
            {
                Tracker.Deregister(TrackerId);
                Logger.Error(ex, ex.Message);
                throw;
            }
        }

        /// <summary>
        /// The capacity in bytes. Note that for persisted MMFs, the ByteLength can exceed _fileStream.Length, so writes to that region may be going to never-never-land.
        /// internal for unit tests
        /// </summary>
        internal long CapacityBytes => _view != null ? (long)_view.SafeMemoryMappedViewHandle.ByteLength : 0;

        /// <summary>
        /// The number of items in the List.
        /// Same as _size in list.cs
        /// The 8 bytes in the file after Version and DataType
        /// </summary>
        public long Count
        {
            get
            {
                lock (SyncRoot)
                {
                    //Debug.Assert(_ptrCount != null);
                    // if (_ptrCount == null)
                    // {
                    //     return 0;
                    // }
                    var count = UnsafeGetCount();
                    if (IsReadOnly && count > _capacity)
                    {
                        // The writer has increased Count and therefore its Capacity. We must reset _mmf and _view etc. to increase our capacity to the new, larger, file size
                        ResetMmfAndView(0);
                    }
                    return count;
                }
            }
            protected set
            {
                lock (SyncRoot)
                {
                    Debug.Assert(value <= _capacity);
                    // if (value > _capacity)
                    // {
                    //     throw new ListMmfException($"Attempt to set Count={value} which must be <= _capacity={_capacity}");
                    // }
                    UnsafeSetCount(value);
                }
            }
        }

        protected void UnsafeSetCount(long value)
        {
            Unsafe.Write(_ptrCount, value);
        }

        protected long UnsafeGetCount()
        {
            if (_isDisposed)
            {
                return 0;
            }
            return Unsafe.Read<long>(_ptrCount);
        }

        /// <summary>
        /// The version (first 4 bytes of the file)
        /// </summary>
        public int Version
        {
            get
            {
                lock (SyncRoot)
                {
                    var version = Unsafe.Read<int>(_ptrVersion);
                    return version;
                }
            }
            protected set
            {
                lock (SyncRoot)
                {
                    Unsafe.Write(_ptrVersion, value);
                }
            }
        }

        /// <summary>
        /// The DataType (second 4 bytes of the file)
        /// </summary>
        public DataType DataType
        {
            get
            {
                lock (SyncRoot)
                {
                    var dataType = Unsafe.Read<int>(_ptrDataType);
                    return (DataType)dataType;
                }
            }
            protected set
            {
                lock (SyncRoot)
                {
                    Unsafe.Write(_ptrDataType, (int)value);
                }
            }
        }

        /// <summary>
        /// The capacity of this ListMmf (number of items). Setting this can increase or decrease the size of the file.
        /// <inheritdoc cref="IListMmf"/>
        /// </summary>
        public long Capacity
        {
            get
            {
                lock (SyncRoot)
                {
                    return _capacity;
                }
            }
            set
            {
                lock (SyncRoot)
                {
                    if (value == _capacity)
                    {
                        // no change
                        return;
                    }
                    var count = UnsafeGetCount();
                    Debug.Assert(value >= count);
                    // if (value < Count)
                    // {
                    //     throw new ListMmfException($"Capacity cannot be changed to {value:N0} because Count={Count:N0} for {this}");
                    // }
                    if (value < _capacity)
                    {
                        // Trim the extra down to make just enough room for Count
                        ResetCapacity(value);
                    }
                    else
                    {
                        GrowCapacity(value);
                    }
                }
            }
        }

        /// <summary>
        /// This method is called whenever _mmf and View are changed.
        /// Inheritors should first call base.ResetPointers() and then reset their own pointers (if any) from BasePointerByte
        /// </summary>
        /// <returns>the number of header bytes reserved by this class</returns>
        protected virtual int ResetPointers()
        {
            // First set BasePointerByte
            lock (SyncRoot)
            {
                if (_view == null)
                {
                    throw new ListMmfException("Why?");
                }
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
                var sizeSoFar = 0;
                _ptrVersion = (int*)(_basePointerView + sizeSoFar);
                sizeSoFar += sizeof(int);
                _ptrDataType = (int*)(_basePointerView + sizeSoFar);
                sizeSoFar += sizeof(int);
                _ptrCount = (long*)(_basePointerView + sizeSoFar);

                _ptrArray = _basePointerView + _parentHeaderBytes + HeaderBytesBase;
                return HeaderBytesBase;
            }
        }

        /// <summary>
        /// Tries to set the capacity of this list to the size of the list. This method can
        /// be used to minimize a list's memory overhead once it is known that no
        /// new items will be added to the list. To completely clear a list and
        /// release all memory referenced by the list, execute the following
        /// statements:
        ///
        /// list.Clear();
        /// list.TrimExcess();
        ///
        /// BUT this will not succeed if some Reader is open.
        /// </summary>
        public void TrimExcess()
        {
            lock (SyncRoot)
            {
                if (IsReadOnly)
                {
                    return;
                }
                var threshold = (long)(Capacity * 0.9);
                if (Count < threshold)
                {
                    Capacity = Count;
                }
            }
        }

        /// <summary>
        /// Initialize/Reset the _mmf file etc.
        /// </summary>
        protected void ResetView()
        {
            lock (SyncRoot)
            {
                _view?.Dispose();
                _view = _mmf?.CreateViewAccessor(0, 0, _access);
                if (!IsReadOnly && _fileStream.Length != CapacityBytes)
                {
                    // Set the file length up to the view length so we don't write off the end
                    //Logger.ConditionalDebug($"Changing _fileStream.Length from {_fileStream.Length:N0} to {CapacityBytes:N0} for {this}");
                    _fileStream.SetLength(CapacityBytes);
                }
                var totalHeaderBytes = ResetPointers();
                if (totalHeaderBytes != _parentHeaderBytes + HeaderBytesBase)
                {
                    throw new ListMmfException($"{nameof(ResetPointers)} returns {totalHeaderBytes} but expected {_parentHeaderBytes + HeaderBytesBase}");
                }
                _capacity = (CapacityBytes - _parentHeaderBytes - HeaderBytesBase) / _width; // for the header fields just before the beginning of the array
                if (_capacity < Count)
                {
                    throw new ListMmfException($"_capacity={_capacity:N0} cannot be less than Count={Count:N0} for {this}");
                }
            }
        }

        /// <summary>
        /// Grows the capacity of this list to at least the given minCapacityItems.
        /// If the correct capacity is less than minCapacityItems, the
        /// capacity is increased to the maximum of (twice the current capacity) or 1GB,
        ///     or to minCapacityItems if that is larger
        /// </summary>
        /// <param name="minCapacityItems"></param>
        protected void GrowCapacity(long minCapacityItems)
        {
            lock (SyncRoot)
            {
                if (IsReadOnly || minCapacityItems <= _capacity)
                {
                    // nothing to do
                    return;
                }

                // Grow by the smaller of Capacity (doubling file size) or 1 GB (we don't want to double a 500 GB file)
                var extraCapacity = Math.Min(Capacity, 1024 * 1024 * 1024);
                var newCapacityItems = Math.Max(_capacity + extraCapacity, minCapacityItems);
                ResetCapacity(newCapacityItems);
            }
        }

        /// <summary>
        /// Reset Capacity to newCapacityItems.
        /// If reducing Capacity, this will NOT succeed is a Reader is open.
        /// </summary>
        /// <param name="newCapacityItems"></param>
        /// <exception cref="ListMmfException"></exception>
        protected void ResetCapacity(long newCapacityItems)
        {
            lock (SyncRoot)
            {
                if (IsReadOnly)
                {
                    throw new ListMmfException($"{nameof(Capacity)} cannot be set on this Read-Only list.");
                }
                if (newCapacityItems < Count)
                {
                    throw new ListMmfException($"newCapacityItems={newCapacityItems} cannot be less than Count={Count}");
                }

                // Note that this method is called by TrimExcess() to shrink Capacity (but not below Count)
                var capacityBytes = CapacityItemsToBytes(newCapacityItems);
                if (capacityBytes == CapacityBytes)
                {
                    // No change, so no need to reset
                    return;
                }
                //Logger.ConditionalDebug($"Changing capacity from {_capacity:N0} to {newCapacityItems:N0} in {this}");
                ResetMmfAndView(capacityBytes, newCapacityItems);
            }
        }

        /// <summary>
        /// ResetMmfAndView
        /// </summary>
        /// <param name="capacityBytes"></param>
        /// <param name="newCapacityItems">Carried along for debugging</param>
        private void ResetMmfAndView(long capacityBytes, long newCapacityItems = 0)
        {
            if (IsReadOnly && capacityBytes > 0)
            {
                throw new ListMmfException("All ReadOnly files must be opened with 0 to match the current size of the file");
            }
            var oldCapacityBytes = CapacityBytes;
            var oldCapacityItems = Capacity;
            var fi = new FileInfo(Path);
            _view?.Dispose();
            _view = null;
            _mmf?.Dispose();
            _mmf = null;
            if (capacityBytes != 0 && capacityBytes < fi.Length)
            {
                // We want to shorten (Truncate) the file
                try
                {
                    //Logger.ConditionalDebug($"Truncating, changing _fileStream.Length from {_fileStream.Length:N0} to {capacityBytes:N0} " +
                    //    $"and oldCapacityItems={oldCapacityItems:N0} newCapacityItems={newCapacityItems:N0} for {this}");
                    _fileStream.SetLength(capacityBytes);
                }
                catch (Exception)
                {
                    // Ignore -- some reader may have this file open
                    Logger.Warn($"Unable to shrink to {capacityBytes:N0} from {_fileStream.Length:N0} for {this}");
                }
            }
            try
            {
                if (capacityBytes != 0 && capacityBytes < _fileStream.Length)
                {
                    // We can't open a file for ReadWrite with a smaller capacity, except 0
                    capacityBytes = _fileStream.Length;
                }
                _mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, capacityBytes, _access, HandleInheritability.None, true);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.Message);
                throw;
            }
            ResetView();
            //Logger.ConditionalDebug($"Reset Mmf and View in {GetInfo()}");
        }

        /// <summary>
        /// Return the capacity in bytes needed to store capacityItems, rounded up to the PageSize used by MMF
        /// </summary>
        /// <param name="capacityItems"></param>
        /// <returns></returns>
        private long CapacityItemsToBytes(long capacityItems)
        {
            var result = capacityItems * Unsafe.SizeOf<T>()
                         + _parentHeaderBytes
                         + HeaderBytesBase; // for the header fields just before the beginning of the array
            var intoPage = result % PageSize;
            if (intoPage > 0)
            {
                // Round up to the next page
                result += PageSize - intoPage;
            }
            return result;
        }

        /// <summary>
        /// Benchmarking shows the compiler will optimize away this method
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        protected T UnsafeRead(long index)
        {
            lock (SyncRoot)
            {
                return UnsafeReadNoLock(index);
            }
        }

        /// <summary>
        /// Benchmarking shows the compiler will optimize away this method
        /// </summary>
        /// <param name="index"></param>
        /// <param name="callerName"></param>
        /// <returns></returns>
#if DEBUG
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
#endif
        protected T UnsafeReadNoLock(long index, [CallerMemberName] string callerName = "")
        {
            // Avoid going to properties (e.g. Count and Capacity) which lock
            if (IsReadOnly && index >= _capacity)
            {
                // The writer has increased Capacity. We must reset _mmf and _view etc. to increase our capacity to the new, larger, file size
                ResetMmfAndView(0);
            }
            var count = UnsafeGetCount();
            if (index >= count)
            {
                if (callerName != "Add" && callerName != "AddRange")
                {
                    var msg = $"index={index:N0} Count={count:N0} for {this}";
                    Logger.Error(msg);
                    throw new ArgumentOutOfRangeException(msg);
                }
            }
            return ReadUnchecked(index);
        }

        /// <summary>
        /// This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
        ///     e.g. are iterating (e.g. in a for loop)
        /// Benchmarking shows the compiler will optimize away this method
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
#if DEBUG
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
#endif
        public T ReadUnchecked(long index)
        {
            lock (SyncRoot)
            {
                // Avoid going to properties (i.e. Capacity or Count) which lock
                if (IsReadOnly && index >= _capacity)
                {
                    // The writer has increased Capacity. We must reset _mmf and _view etc. to increase our capacity to the new, larger, file size
                    ResetMmfAndView(0);
                }
#if DEBUG

                // Following trick can reduce the range check by one
                if ((ulong)index >= (uint)_capacity)
                {
                    var msg = $"index={index:N0} but _capacity is {_capacity:N0}" + $"\n{Environment.StackTrace}";
                    Logger.Error(msg);
                    LogManager.Flush();
                    throw new ArgumentOutOfRangeException(nameof(index), _capacity, msg);
                }
                // try
                // {
                return Unsafe.Read<T>(_ptrArray + index * _width);
                // }
                // catch (AccessViolationException ex)
                // {
                //     var msg = $"AccessViolationException when index={index:N0} and maximum index is {count - 1:N0}\n{ex.Message} for {this}"
                //               + $"\n{Environment.StackTrace}";
                //     Logger.Error(ex, msg);
                //     LogManager.Flush();
                //     throw;
                // }
                // catch (Exception ex)
                // {
                //     var msg = $"index={index:N0} but maximum index is {count - 1:N0}\n{ex.Message} for {this}" + $"\n{Environment.StackTrace}";
                //     Logger.Error(ex, msg);
                //     LogManager.Flush();
                //     throw;
                // }
#else
                return Unsafe.Read<T>(_ptrArray + index * _width);
#endif
            }
        }

        /// <summary>
        /// Benchmarking shows the compiler will optimize away this method
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
#if DEBUG
        [HandleProcessCorruptedStateExceptions]
        [SecurityCritical]
#endif
        protected void UnsafeWriteNoLock(long index, T item, [CallerMemberName] string callerName = "")
        {
            // try
            // {
            // if (_capacity < Count || index >= _capacity)
            // {
            //     var msg = $"index={index:N0} Capacity={_capacity:N0} for {this}";
            //     Logger.Error(msg);
            //     throw new InvalidOperationException(msg);
            // }
            // if (index >= Count)
            // {
            //     if (callerName != "Add" && callerName != "AddRange")
            //     {
            //         var msg = $"index={index:N0} Count={Count:N0} and callerName={callerName} for {this}";
            //         Logger.Error(msg);
            //         throw new InvalidOperationException(msg);
            //     }
            // }
            Unsafe.Write(_ptrArray + index * _width, item);
            // }
            // catch (Exception ex)
            // {
            //     Logger.Error(ex, $"{ex.Message} index={index:N0} _access={_access} Capacity={Capacity:N0} Count={Count:N0} for {this}");
            //     LogManager.Flush();
            //     Environment.Exit(1);
            // }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_isDisposed)
            {
                lock (SyncRoot)
                {
                    TrimExcess();
                    _isDisposed = true;
                    _mmf?.Dispose();
                    _view?.Dispose();
                    _fileStream?.Dispose();
                    _fileStream = null;
                    base.Dispose(true);

                    //Logger.ConditionalDebug($"Disposed {this}");
                }
            }
        }

        /// <summary>
        /// Accessing Count in the debugger ToString() can crash the debugger. Use GetInfo() for that.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            // Accessing Count in the debugger ToString() can crash the debugger. Use GetInfo() for that.
            var result = _accessName;
#if DEBUG
            result += $" {InstanceId}";
#endif
            return result;
        }

        public string GetInfo()
        {
            var count = _isDisposed || _view == null || _ptrCount == null ? 0 : Count;
            var result = $"count={count:N0}/_capacity={_capacity:N0} {_accessName}";
#if DEBUG
            result += $" {InstanceId}";
#endif
            return result;
        }
    }
}
