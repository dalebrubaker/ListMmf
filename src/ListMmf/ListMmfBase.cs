//#define LOGGING

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Security;
using BruSoftware.ListMmf.Exceptions;

namespace BruSoftware.ListMmf;

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

#if LOGGING
    private static readonly Logger s_logger = LogManager.GetCurrentClassLogger();
#endif

    private readonly MemoryMappedFileAccess _access;

    /// <summary>
    /// Reader {Name} or Writer {Name}
    /// </summary>
    private readonly string _accessName;

    private readonly long _parentHeaderBytes;

    /// <summary>
    /// The size of T (number of bytes)
    /// </summary>
    protected readonly int _width;

    protected readonly object SyncRoot = new();

    /// <summary>
    /// This is the beginning of the View, before the parentHeaderBytes and the MyHeaderBytes header for this array.
    /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
    /// I don't know why they do that, and I don't seem to need it...
    /// </summary>
    protected byte* _basePointerView;

    /// <summary>
    /// This is the field corresponding to Capacity (in items, not Bytes)
    /// </summary>
    protected long _capacity;

    private FileStream _fileStream;
    private FileStream _lockFileStream;
    private Func<long> _funcGetCount;

    /// <summary>
    /// Platform-specific file locking action (null for Windows, POSIX implementation for POSIX systems)
    /// </summary>
    private readonly Action<string> _actionLockFile;

    /// <summary>
    /// Platform-specific file unlocking action (null for Windows, POSIX implementation for POSIX systems)
    /// </summary>
    private readonly Action<string> _actionUnlockFile;

    protected bool _isDisposed;

    private MemoryMappedFile _mmf;

    /// <summary>
    /// This is the beginning of the Array, after the parentHeaderBytes and the MyHeaderBytes header for this array.
    /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
    /// I don't know why they do that, and I don't seem to need it...
    /// internal instead of protected to avoid CLS-compliant warning
    /// </summary>
    protected byte* _ptrArray;

    /// <summary>
    /// The long* into the Count location in the view (the 8 bytes following Version and DataType)
    /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
    /// I don't know why they do that, and I don't seem to need it...
    /// </summary>
    private long* _ptrCount;

    /// <summary>
    /// The long* into the DataType location in the view (the second 4 bytes)
    /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
    /// I don't know why they do that, and I don't seem to need it...
    /// </summary>
    private int* _ptrDataType;

    /// <summary>
    /// The long* into the Version location in the view (the first 4 bytes)
    /// This is a "stale" pointer -- we don't get a new one on each read access, as Microsoft does in SafeBuffer.Read(T).
    /// I don't know why they do that, and I don't seem to need it...
    /// </summary>
    private int* _ptrVersion;

    private MemoryMappedViewAccessor _view;

    /// <summary>
    /// Track whether we've acquired a pointer that needs to be released
    /// </summary>
    private bool _pointerAcquired;

    /// <summary>
    /// Flag indicating whether ResetPointers is disallowed to prevent AccessViolationException
    /// </summary>
    private bool _isResetPointersDisallowed;

    /// <summary>
    /// Open the list in a MemoryMappedFile at path
    /// This ctor is used only by derived classes, to pass along parentHeaderBytes
    /// Each inheriting class MUST CALL ResetView() from their constructors in order to properly set their pointers in the header
    /// </summary>
    /// <param name="path">The path to open ReadWrite</param>
    /// <param name="capacityItems">
    /// The number of items to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// </param>
    /// <param name="parentHeaderBytes"></param>
    /// <exception cref="ListMmfException"></exception>
    protected ListMmfBase(string path, long capacityItems, long parentHeaderBytes) : base(path)
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
        if (!Environment.Is64BitProcess)
        {
            throw new ListMmfException("Not supported on 32-bit process. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
        }
        _width = Unsafe.SizeOf<T>();
        _access = MemoryMappedFileAccess.ReadWrite;
        _funcGetCount = GetCountWriter;
        _accessName = "Writer " + path;
        Path = path;
        _parentHeaderBytes = parentHeaderBytes;

        // Assign platform-specific locking behavior
        if (IsPosixPlatform())
        {
            // POSIX systems (macOS, Linux, BSD variants, etc.) use lock file approach
            _actionLockFile = PosixLockFile;
            _actionUnlockFile = PosixUnlockFile;
        }
        else
        {
            // Windows uses native FileShare behavior, no lock files needed
            _actionLockFile = null;
            _actionUnlockFile = null;
        }

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
                    Directory.CreateDirectory(fi.DirectoryName);
                }
            }
            capacityBytes = CapacityItemsToBytes(capacityItems); // rounds up to PageSize, so we don't write off the end
        }
        CreateMmf(capacityBytes);
    }

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
    /// The capacity in bytes. Note that for persisted MMFs, the ByteLength can exceed _fileStream.Length, so writes to that region may be going to
    /// never-never-land.
    /// internal for unit tests
    /// </summary>
    internal long CapacityBytes => _view != null ? (long)_view.SafeMemoryMappedViewHandle.ByteLength : 0;

    // // ReSharper disable once StaticMemberInGenericType
    // public static int NumberOfGetCount;

    /// <summary>
    /// The number of items in the List.
    /// Same as _size in list.cs
    /// The 8 bytes in the file after Version and DataType
    /// </summary>
    public long Count
    {
        get => _funcGetCount.Invoke(); // Returns 0 if disposed
        protected set =>
            // if (value > _capacity)
            // {
            //     throw new ListMmfException($"Attempt to set Count={value} which must be <= _capacity={_capacity}");
            // }
            Unsafe.Write(_ptrCount, value);
    }

    /// <summary>
    /// The version (first 4 bytes of the file)
    /// </summary>
    public int Version
    {
        get
        {
            var version = Unsafe.Read<int>(_ptrVersion);
            return version;
        }
        protected set => Unsafe.Write(_ptrVersion, value);
    }

    /// <summary>
    /// The DataType (second 4 bytes of the file)
    /// </summary>
    public DataType DataType
    {
        get
        {
            var dataType = Unsafe.Read<int>(_ptrDataType);
            return (DataType)dataType;
        }
        protected init => Unsafe.Write(_ptrDataType, (int)value);
    }

    /// <summary>
    /// The capacity of this ListMmf (number of items). Setting this can increase or decrease the size of the file.
    /// <inheritdoc cref="IListMmf" />
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

    /// <summary>
    /// Gets a value indicating whether ResetPointers has been disallowed to prevent AccessViolationException.
    /// Once set to true via DisallowResetPointers(), it cannot be reversed.
    /// </summary>
    public bool IsResetPointersDisallowed
    {
        get
        {
            lock (SyncRoot)
            {
                return _isResetPointersDisallowed;
            }
        }
    }

    /// <summary>
    /// Disallows future calls to ResetPointers() to prevent AccessViolationException.
    /// This should be called when the file capacity is locked and should no longer grow.
    /// This is a one-way operation and cannot be reversed.
    /// </summary>
    public void DisallowResetPointers()
    {
        lock (SyncRoot)
        {
            _isResetPointersDisallowed = true;
        }
    }

    private static long GetCountDisposed()
    {
        return 0;
    }

    /// <summary>
    /// Determines if running on a POSIX-compliant platform (macOS, Linux, BSD variants, etc.)
    /// </summary>
    /// <returns>True for POSIX systems, false for Windows</returns>
    private static bool IsPosixPlatform()
    {
        return Environment.OSVersion.Platform != PlatformID.Win32NT;
    }

    /// <summary>
    /// POSIX-specific file locking implementation using lock files
    /// </summary>
    /// <param name="path">Path to the data file to lock</param>
    private void PosixLockFile(string path)
    {
        var lockPath = path + UtilsListMmf.LockFileExtension;

        // Check if lock file exists and try to clean it up if it's stale
        if (File.Exists(lockPath))
        {
            try
            {
                // Try to open the lock file with write access - if another process has it locked, this will fail
                using (var testStream = new FileStream(lockPath, FileMode.Open, FileAccess.Write, FileShare.None))
                {
                    // If we can open it, it's a stale lock from a crashed process
                    testStream.Close();
                }

                // Delete the stale lock file
                File.Delete(lockPath);
#if LOGGING
                s_logger.Info($"Cleaned up stale lock file: {lockPath}");
#endif
            }
            catch (IOException)
            {
                // Lock file is truly in use by another process
                throw new IOException($"Cannot open '{path}' for writing. The file is already open by another writer.");
            }
            catch (UnauthorizedAccessException)
            {
                // Lock file is truly in use by another process with exclusive access
                throw new IOException($"Cannot open '{path}' for writing. The file is already open by another writer.");
            }
        }

        try
        {
            // Try to create lock file exclusively
            _lockFileStream = new FileStream(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        }
        catch (IOException ex)
        {
            // Lock file was just created by another process after we checked
            throw new IOException($"Cannot open '{path}' for writing. The file is already open by another writer.", ex);
        }
    }

    /// <summary>
    /// POSIX-specific file unlocking implementation - cleanup lock files
    /// </summary>
    /// <param name="path">Path to the data file to unlock</param>
    private void PosixUnlockFile(string path)
    {
        if (_lockFileStream != null)
        {
            _lockFileStream.Dispose();
            _lockFileStream = null;

            var lockPath = path + UtilsListMmf.LockFileExtension;
            try
            {
                File.Delete(lockPath);
            }
            catch
            {
                // Best effort cleanup - file might already be deleted or in use
            }
        }
    }

    private long GetCountWriter()
    {
        var count = Unsafe.Read<long>(_ptrCount);
        return count;
    }

    private void CreateMmf(long capacityBytes)
    {
        try
        {
            // Apply platform-specific locking (POSIX lock files or Windows native behavior)
            _actionLockFile?.Invoke(Path);

            // Create the file stream with standard FileShare behavior
            _fileStream = UtilsListMmf.CreateFileStreamFromPath(Path, _access);

            // mapName must always be NULL! We can't use it when we are doing reader/writer using FileStream
            if (_fileStream.Length == 0 && capacityBytes <= 0)
            {
                _fileStream.SetLength(PageSize); // We want to write at least 1 page
            }

            _mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, capacityBytes, _access, HandleInheritability.None, true);
        }
        catch (IOException)
        {
            Tracker.Deregister(TrackerId);
            // Clean up any resources that were acquired
            _fileStream?.Dispose();
            _actionUnlockFile?.Invoke(Path);
            throw; // Let IOException bubble up - file access conflicts should fail fast
        }
        catch (Exception)
        {
            Tracker.Deregister(TrackerId);
            // Clean up any resources that were acquired
            _fileStream?.Dispose();
            _actionUnlockFile?.Invoke(Path);
            throw;
        }
    }

    /// <summary>
    /// Safely releases the acquired pointer if one exists
    /// </summary>
    private void ReleasePointerIfAcquired()
    {
        if (_pointerAcquired && _basePointerView != null && _view != null)
        {
            try
            {
                _view.SafeMemoryMappedViewHandle.ReleasePointer();
            }
            catch
            {
                // Best effort - view might already be disposed
            }
            _pointerAcquired = false;
            _basePointerView = null;
        }
    }

    /// <summary>
    /// This method is called whenever _mmf and View are changed.
    /// Inheritors should first call base.ResetPointers() and then reset their own pointers (if any) from BasePointerByte
    /// </summary>
    /// <returns>the number of header bytes reserved by this class</returns>
    protected virtual int ResetPointers()
    {
        // Check if ResetPointers has been disallowed
        if (_isResetPointersDisallowed)
        {
            throw new ResetPointersDisallowedException(
                $"ResetPointers is not allowed after DisallowResetPointers() has been called on {Path ?? "this ListMmf"}");
        }

        // Note: Don't release pointer here - it should have been released before the view was disposed
        // Reset the flag since any old pointer is now invalid
        _pointerAcquired = false;
        _basePointerView = null;

        // First set BasePointerByte
        if (_view == null)
        {
            throw new ListMmfException("Why?");
        }
        var safeBuffer = _view.SafeMemoryMappedViewHandle;
        //RuntimeHelpers.PrepareConstrainedRegions();
        _basePointerView = null;

        // Acquire the pointer and track that we need to release it later
        safeBuffer.AcquirePointer(ref _basePointerView);
        _pointerAcquired = true;
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

    /// <summary>
    /// Tries to set the capacity of this list to the size of the list. This method can
    /// be used to minimize a list's memory overhead once it is known that no
    /// new items will be added to the list. To completely clear a list and
    /// release all memory referenced by the list, execute the following
    /// statements:
    /// list.Clear();
    /// list.TrimExcess();
    /// BUT this will not succeed if some Reader is open.
    /// </summary>
    public void TrimExcess()
    {
        // Skip trimming if ResetPointers is disallowed
        if (_isResetPointersDisallowed)
        {
            return;
        }

        var threshold = (long)(Capacity * 0.9);
        if (Count < threshold)
        {
            Capacity = Count;
        }
    }

    /// <summary>
    /// Initialize/Reset the _mmf file etc.
    /// </summary>
    protected void ResetView()
    {
        // Release pointer before disposing the view
        ReleasePointerIfAcquired();
        _view?.Dispose();
        _view = _mmf?.CreateViewAccessor(0, 0, _access);
        if (_fileStream.Length != CapacityBytes)
        {
            // Set the file length up to the view length so we don't write off the end
            _fileStream.SetLength(CapacityBytes);
        }
        var totalHeaderBytes = ResetPointers();
        if (totalHeaderBytes != _parentHeaderBytes + HeaderBytesBase)
        {
            throw new ListMmfException($"{nameof(ResetPointers)} returns {totalHeaderBytes} but expected {_parentHeaderBytes + HeaderBytesBase}");
        }
        _capacity = (CapacityBytes - _parentHeaderBytes - HeaderBytesBase)
                    / _width; // for the header fields just before the beginning of the array
        if (_capacity < Count)
        {
            throw new ListMmfException($"_capacity={_capacity:N0} cannot be less than Count={Count:N0} for {this}");
        }
    }

    /// <summary>
    /// Grows the capacity of this list to at least the given minCapacityItems.
    /// If the correct capacity is less than minCapacityItems, the
    /// capacity is increased to the maximum of (twice the current capacity) or 1GB,
    /// or to minCapacityItems if that is larger
    /// </summary>
    /// <param name="minCapacityItems"></param>
    protected void GrowCapacity(long minCapacityItems)
    {
        // Check if ResetPointers has been disallowed
        if (_isResetPointersDisallowed)
        {
            throw new ResetPointersDisallowedException(
                $"ResetPointers is not allowed after DisallowResetPointers() has been called on {Path ?? "this ListMmf"}");
        }

        if (minCapacityItems <= _capacity)
        {
            // nothing to do
            return;
        }

        // Grow by the smaller of Capacity (doubling file size) or 1 GB (we don't want to double a 500 GB file)
        var extraCapacity = Math.Min(Capacity, 1024 * 1024 * 1024);
        var newCapacityItems = Math.Max(_capacity + extraCapacity, minCapacityItems);
        //var sw = Stopwatch.StartNew();
        ResetCapacity(newCapacityItems);
        // if (this is ListMmfTimeSeriesDateTimeSeconds)
        // {
        //     s_logger.Error($"Grew capacity of {Path} from {_capacity:N0} to {newCapacityItems:N0} items in {sw.ElapsedMilliseconds} ms");
        // }
    }

    /// <summary>
    /// Reset Capacity to newCapacityItems.
    /// If reducing Capacity, this will NOT succeed is a Reader is open.
    /// </summary>
    /// <param name="newCapacityItems"></param>
    /// <exception cref="ListMmfException"></exception>
    protected void ResetCapacity(long newCapacityItems)
    {
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
        ResetMmfAndView(capacityBytes, newCapacityItems);
    }

    /// <summary>
    /// ResetMmfAndView
    /// </summary>
    /// <param name="capacityBytes"></param>
    /// <param name="newCapacityItems">Carried along for debugging</param>
    private void ResetMmfAndView(long capacityBytes, long newCapacityItems = 0)
    {
        var oldCapacityBytes = CapacityBytes;
        var oldCapacityItems = Capacity;
        var fi = new FileInfo(Path);

        // Release pointer before disposing the view
        ReleasePointerIfAcquired();
        _view?.Dispose();
        _view = null;
        _mmf?.Dispose();
        _mmf = null;
        if (capacityBytes != 0 && capacityBytes < fi.Length)
        {
            // We want to shorten (Truncate) the file
            try
            {
                _fileStream.SetLength(capacityBytes);
            }
            catch (Exception)
            {
                // Ignore -- some reader may have this file open
                //s_logger.Warn("Unable to shrink to {CapacityBytes:N0} from {FileLength:N0} for {This}", capacityBytes, _fileStream.Length, this);
            }
        }
        if (capacityBytes != 0 && capacityBytes < _fileStream.Length)
        {
            // We can't open a file for ReadWrite with a smaller capacity, except 0
            capacityBytes = _fileStream.Length;
        }
        _mmf = MemoryMappedFile.CreateFromFile(_fileStream, null, capacityBytes, _access, HandleInheritability.None, true);
        ResetView();
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected T UnsafeRead(long index)
    {
        return Unsafe.Read<T>(_ptrArray + index * _width);
    }

    /// <summary>
    /// This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
    /// e.g. are iterating (e.g. in a for loop)
    /// Benchmarking shows the compiler will optimize away this method
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
#if DEBUG
    [SecurityCritical]
#endif
    public T ReadUnchecked(long index)
    {
        // Avoid going to properties (i.e. Capacity or Count) which lock
        return Unsafe.Read<T>(_ptrArray + index * _width);
    }

    /// <summary>
    /// Returns a ReadOnlySpan&lt;T&gt; representing a range of elements from the ListMmf.
    /// Provides zero-copy access to the underlying memory-mapped data.
    /// The span is only valid while the ListMmf remains unchanged and should be used within method scope.
    /// </summary>
    /// <param name="start">The starting index (inclusive)</param>
    /// <param name="length">The number of elements to include in the span</param>
    /// <returns>A ReadOnlySpan&lt;T&gt; representing the requested range</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start or length is invalid</exception>
    /// <exception cref="ListMmfOnlyInt32SupportedException">If length exceeds int.MaxValue</exception>
    public ReadOnlySpan<T> GetRange(long start, int length)
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

        // Create span from existing pointer arithmetic
        return new ReadOnlySpan<T>(_ptrArray + start * _width, length);
    }

    /// <summary>
    /// Returns a ReadOnlySpan&lt;T&gt; representing elements from the start index to the end of the list.
    /// Provides zero-copy access to the underlying memory-mapped data.
    /// The span is only valid while the ListMmf remains unchanged and should be used within method scope.
    /// </summary>
    /// <param name="start">The starting index (inclusive)</param>
    /// <returns>A ReadOnlySpan&lt;T&gt; representing elements from start to the end</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start is invalid</exception>
    /// <exception cref="ListMmfOnlyInt32SupportedException">If the resulting length exceeds int.MaxValue</exception>
    public ReadOnlySpan<T> GetRange(long start)
    {
        var count = Count;
        var length = count - start;
        if (length > int.MaxValue)
        {
            throw new ListMmfOnlyInt32SupportedException(length);
        }

        return GetRange(start, (int)length);
    }

    /// <summary>
    /// Benchmarking shows the compiler will optimize away this method
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
#if DEBUG
    [SecurityCritical]
#endif
    protected void UnsafeWrite(long index, T item, [CallerMemberName] string callerName = "")
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
        //     Environment.Exit(1);
        // }
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void Truncate(long newCount)
    {
        // if (this is ListMmfTimeSeriesDateTimeSeconds && Path.Contains(@"\1T\"))
        // {
        // }
        var count = Count;
        if (newCount >= count)
        {
            // nothing to do
            return;
        }
        if (newCount < 0)
        {
            throw new ArgumentException("Truncate new length cannot be negative");
        }
        if (newCount > _capacity)
        {
            throw new ListMmfException($"Truncate new length {newCount} cannot be greater than Capacity {_capacity}");
        }

        // Change Count first so readers won't use a wrong value
        Count = newCount;
        ResetCapacity(newCount);
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void TruncateBeginning(long newCount, IProgress<long> progress = null)
    {
        var count = Count;
        if (newCount > count)
        {
            throw new ListMmfException($"TruncateBeginning {newCount} must not be greater than Count={count}");
        }

        if (newCount == count)
        {
            return; // No-op optimization
        }

        var beginIndex = count - newCount;
        var chunkSize = Math.Max(1, newCount / 100); // 1% chunks for progress reporting
        var elementsProcessed = 0L;

        var sourcePtr = _ptrArray + beginIndex * _width;
        var destPtr = _ptrArray;

        // Process in chunks for progress reporting
        while (elementsProcessed < newCount)
        {
            var remainingElements = newCount - elementsProcessed;
            var currentChunkSize = Math.Min(chunkSize, remainingElements);
            var bytesToCopy = currentChunkSize * _width;

            // Use bulk memory copy for this chunk
            Buffer.MemoryCopy(
                sourcePtr + elementsProcessed * _width,
                destPtr + elementsProcessed * _width,
                bytesToCopy,
                bytesToCopy);

            elementsProcessed += currentChunkSize;

            // Report progress
            progress?.Report(elementsProcessed);
        }

        // Change Count first so readers won't use a wrong value
        Count = newCount;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_isDisposed)
        {
            // Only trim if ResetPointers is allowed
            if (!_isResetPointersDisallowed)
            {
                TrimExcess();
            }
            _funcGetCount = GetCountDisposed;
            _isDisposed = true;

            // Release the pointer before disposing the view
            ReleasePointerIfAcquired();

            _view?.Dispose(); // must be disposed before _mmf
            _view = null;
            _mmf?.Dispose();
            _mmf = null;
            _fileStream?.Dispose();
            _fileStream = null;

            // Release platform-specific locks if we were writing
            if (_access == MemoryMappedFileAccess.ReadWrite)
            {
                _actionUnlockFile?.Invoke(Path);
            }

            base.Dispose(true);
            // GC.Collect();
            // GC.WaitForPendingFinalizers();
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