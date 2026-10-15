//#define LOGGING

using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading;
using BruSoftware.ListMmf.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BruSoftware.ListMmf;

/// <summary>
/// This is the class from which inheritors should derive, NOT ListMmf which exposes more public methods than necessary
/// Supports both ReadWrite (default) and Read access modes.
/// - ReadWrite mode: Acquires exclusive lock, no concurrent writers allowed
/// - Read mode: No locks acquired, multiple readers allowed, FileShare.ReadWrite
/// Within one instance: one writer thread, plus any number of threads calling Count, the indexer, ReadUnchecked,
/// Version or DataType at the same time, including while the writer grows/remaps the file or disposes the list.
/// Those reads are lock-free: see <see cref="MmfReaders"/> for how a remap or Dispose waits for them.
/// Spans from AsSpan are not covered: don't use one after the list has been remapped or disposed.
/// </summary>
/// <typeparam name="T"></typeparam>
public unsafe class ListMmfBase<T> : ListMmfBaseDebug where T : struct
{
    /// <summary>
    /// This class only has Version, DataType and Count
    /// </summary>
    private const int HeaderBytesBase = 16;

    private const int VersionOffset = 0;
    private const int DataTypeOffset = 4;
    private const int CountOffset = 8;

    /// <summary>
    /// currently used by MemoryMappedFiles
    /// </summary>
    private const int PageSize = 4096;

    // Logging removed: previously used NLog under conditional compilation.

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

    private FileStream? _fileStream;
    private readonly ExclusiveFileLock? _exclusiveFileLock;

    /// <summary>
    /// The pointers readers use. null before the first ResetView, after Dispose, or after a failed remap.
    /// <see cref="MmfMapping.Remapping"/> while the writer is remapping.
    /// Only replaced under SyncRoot, and the old view is unmapped only after MmfReaders.WaitForReaders.
    /// </summary>
    private MmfMapping? _mapping;

    /// <summary>
    /// The managed thread id doing the current remap, or 0. Lets a read on that thread fail instead of waiting on itself.
    /// </summary>
    private int _remappingThreadId;

    /// <summary>
    /// True if opened in read-only mode (no locks, FileShare.ReadWrite)
    /// </summary>
    public bool IsReadOnly { get; }

    protected bool _isDisposed;

    private MemoryMappedFile? _mmf;

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

    private MemoryMappedViewAccessor? _view;

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
    /// <param name="path">The path to open ReadWrite or Read</param>
    /// <param name="capacityItems">
    /// The number of items to initialize the list.
    /// If 0, will be set to some default amount for a new file. Is ignored for an existing one.
    /// Ignored in read-only mode.
    /// </param>
    /// <param name="parentHeaderBytes"></param>
    /// <param name="logger"></param>
    /// <param name="isReadOnly">If true, opens in read-only mode with no locks and FileShare.ReadWrite</param>
    /// <exception cref="ListMmfException"></exception>
    private readonly ILogger _logger;

    protected ListMmfBase(string path, long capacityItems, long parentHeaderBytes, ILogger? logger = null, bool isReadOnly = false,
        TimeSpan? writeLockTimeout = null, TimeSpan? writeLockPollInterval = null) : base(path)
    {
        _logger = logger ?? NullLogger.Instance;
        IsReadOnly = isReadOnly;

        if (parentHeaderBytes % 8 != 0)
            // Not sure this is a necessary limitation
            throw new ListMmfException($"{nameof(parentHeaderBytes)} is required to be a multiple of 8 bytes.");
        if (path == null) throw new ArgumentNullException(nameof(path));
        if (!Environment.Is64BitProcess)
            throw new PlatformNotSupportedException("Requires a 64-bit process (x64 or ARM64).");
        _width = Unsafe.SizeOf<T>();
        _accessName = (isReadOnly ? "Reader " : "Writer ") + path;
        Path = path;
        _parentHeaderBytes = parentHeaderBytes;

        // We want to write at least 1 page
        var fi = new FileInfo(path);
        long capacityBytes;

        if (isReadOnly)
        {
            // Read-only mode: file must already exist
            if (!fi.Exists)
                throw new FileNotFoundException($"Cannot open '{path}' in read-only mode: file does not exist.", path);

            // Open file in read-only mode with shared access (no lock)
            _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            capacityBytes = 0; // 0 means the entire file
            _exclusiveFileLock = null; // No lock for read-only
        }
        else
        {
            // Write mode: acquire exclusive lock
            if (fi.Exists)
            {
                // use the existing file length
                capacityBytes = 0; // 0 means the entire file
            }
            else
            {
                // The file doesn't exist
                if (!Directory.Exists(fi.DirectoryName))
                    if (fi.DirectoryName != null)
                        Directory.CreateDirectory(fi.DirectoryName);

                capacityBytes = CapacityItemsToBytes(capacityItems); // rounds up to PageSize, so we don't write off the end
            }

            // Acquire lock before creating MMF (can't await in unsafe context)
            try
            {
                _exclusiveFileLock = ExclusiveFileLock
                    .AcquireAsync(Path, writeLockTimeout, writeLockPollInterval, alsoLockDataFile: true)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (TimeoutException ex)
            {
                throw new IOException($"Cannot open '{Path}' for writing. The file is already open by another writer.", ex);
            }
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
        get
        {
            var slot = MmfReaders.Current;
            try
            {
                var mapping = EnterRead(slot);
                return mapping == null ? 0 : Volatile.Read(ref *mapping.PtrCount); // Returns 0 if disposed
            }
            finally
            {
                ExitRead(slot);
            }
        }
        protected set
        {
            if (IsReadOnly)
                throw new NotSupportedException("Cannot set Count in read-only mode");
            // if (value > _capacity)
            // {
            //     throw new ListMmfException($"Attempt to set Count={value} which must be <= _capacity={_capacity}");
            // }
            var slot = MmfReaders.Current;
            var isWritten = false;
            try
            {
                var mapping = EnterRead(slot);
                if (mapping != null)
                {
                    // Release: a reader that sees the new Count also sees the items written before it
                    Volatile.Write(ref *mapping.PtrCount, value);
                    isWritten = true;
                }
            }
            finally
            {
                ExitRead(slot);
            }
            if (!isWritten)
                throw new ObjectDisposedException(nameof(ListMmfBase<T>));
        }
    }

    /// <summary>
    /// The version (first 4 bytes of the file)
    /// </summary>
    public int Version
    {
        get
        {
            if (!TryReadHeader<int>(VersionOffset, out var version))
                throw new ObjectDisposedException(nameof(ListMmfBase<T>));
            return version;
        }
        protected set
        {
            if (!TryWriteHeader(VersionOffset, value))
                throw new ObjectDisposedException(nameof(ListMmfBase<T>));
        }
    }

    /// <summary>
    /// The DataType (second 4 bytes of the file)
    /// </summary>
    public DataType DataType
    {
        get
        {
            if (!TryReadHeader<int>(DataTypeOffset, out var dataType))
                throw new ObjectDisposedException(nameof(ListMmfBase<T>));
            return (DataType)dataType;
        }
        protected init
        {
            if (!TryWriteHeader(DataTypeOffset, (int)value))
                throw new ObjectDisposedException(nameof(ListMmfBase<T>));
        }
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
                // no change
                return;
            if (value < _capacity)
                // Trim the extra down to make just enough room for Count
                ResetCapacity(value);
            else
                GrowCapacity(value);
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

    /// <summary>
    /// Starts a read section on this list and returns the mapping to read through, or null if disposed.
    /// The caller MUST call ExitRead(slot) in a finally, and must not call anything that can block or
    /// touch another list before then.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MmfMapping? EnterRead(MmfReaders.Slot slot)
    {
        // Volatile.Write then Volatile.Read keeps the JIT from reordering the two; the CPU's store-load
        // reordering is covered by Interlocked.MemoryBarrierProcessWide in MmfReaders.WaitForReaders.
        Volatile.Write(ref slot.Owner, this);
        var mapping = Volatile.Read(ref _mapping);
        if (mapping is { IsRemapping: true })
        {
            mapping = WaitForRemap(slot);
        }
        return mapping;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExitRead(MmfReaders.Slot slot)
    {
        // Release: every read through the mapping completes before the writer can see the slot clear
        Volatile.Write(ref slot.Owner, null);
    }

    /// <summary>
    /// Leaves the read section while the writer remaps (so the writer isn't waiting on us), then re-enters
    /// once a mapping (or null) has been published.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private MmfMapping? WaitForRemap(MmfReaders.Slot slot)
    {
        if (Volatile.Read(ref _remappingThreadId) == Environment.CurrentManagedThreadId)
        {
            throw new InvalidOperationException($"Read from {Path} during its own remap on the same thread");
        }
        var spinner = new SpinWait();
        while (true)
        {
            ExitRead(slot);
            spinner.SpinOnce();
            Volatile.Write(ref slot.Owner, this);
            var mapping = Volatile.Read(ref _mapping);
            if (mapping is not { IsRemapping: true })
            {
                return mapping;
            }
        }
    }

    /// <summary>
    /// Reads a header field at byteOffset from the start of the view. Returns false if disposed.
    /// </summary>
    protected bool TryReadHeader<TValue>(int byteOffset, out TValue value) where TValue : unmanaged
    {
        var slot = MmfReaders.Current;
        try
        {
            var mapping = EnterRead(slot);
            if (mapping == null)
            {
                value = default;
                return false;
            }
            value = Unsafe.Read<TValue>(mapping.BasePointer + byteOffset);
            return true;
        }
        finally
        {
            ExitRead(slot);
        }
    }

    /// <summary>
    /// Writes a header field at byteOffset from the start of the view. Returns false if disposed.
    /// </summary>
    protected bool TryWriteHeader<TValue>(int byteOffset, TValue value) where TValue : unmanaged
    {
        var slot = MmfReaders.Current;
        try
        {
            var mapping = EnterRead(slot);
            if (mapping == null)
            {
                return false;
            }
            Unsafe.Write(mapping.BasePointer + byteOffset, value);
            return true;
        }
        finally
        {
            ExitRead(slot);
        }
    }

    /// <summary>
    /// Publishes replacement (a new mapping, Remapping, or null) and waits until no reader can still be
    /// reading through the view being replaced. Call under SyncRoot.
    /// </summary>
    private void PublishAndWaitForReaders(MmfMapping? replacement)
    {
        Volatile.Write(ref _mapping, replacement);
        MmfReaders.WaitForReaders(this);
    }

    /// <summary>
    /// Readers see Remapping (and wait) until FinishRemap or FailRemap. Call under SyncRoot.
    /// </summary>
    private void BeginRemap()
    {
        Volatile.Write(ref _remappingThreadId, Environment.CurrentManagedThreadId);
        PublishAndWaitForReaders(MmfMapping.Remapping);
    }

    /// <summary>
    /// A remap threw part way. Readers stop waiting and see the list as disposed (Count 0), matching the
    /// null pointers this state used to leave behind.
    /// </summary>
    private void FailRemap(Exception ex)
    {
        _logger.LogError(ex, "Remap failed for {Path}; reads will see it as disposed", Path);
        Volatile.Write(ref _mapping, null);
        Volatile.Write(ref _remappingThreadId, 0);
    }

    /// <summary>
    /// Unmaps the current view. Only call once readers can no longer reach it (after PublishAndWaitForReaders).
    /// </summary>
    private void UnmapView()
    {
        ClearPointers();
        ReleasePointerIfAcquired();
        _view?.Dispose();
        _view = null;
    }

    private void CreateMmf(long capacityBytes)
    {
        try
        {
            if (!IsReadOnly)
            {
                _fileStream = _exclusiveFileLock!.DataFileStream;

                if (_fileStream.Length == 0 && capacityBytes <= 0)
                    _fileStream.SetLength(PageSize); // We want to write at least 1 page
            }

            var access = IsReadOnly ? MemoryMappedFileAccess.Read : MemoryMappedFileAccess.ReadWrite;
            _mmf = MemoryMappedFile.CreateFromFile(_fileStream!, null, capacityBytes, access,
                HandleInheritability.None, true);
        }
        catch (IOException)
        {
            Tracker.Deregister(TrackerId);
            // Clean up any resources that were acquired
            _fileStream?.Dispose();
            _exclusiveFileLock?.Dispose();
            throw; // Let IOException bubble up - file access conflicts should fail fast
        }
        catch (Exception)
        {
            Tracker.Deregister(TrackerId);
            // Clean up any resources that were acquired
            _fileStream?.Dispose();
            _exclusiveFileLock?.Dispose();
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
            catch (Exception ex)
            {
                // Best effort - view might already be disposed
                _logger.LogWarning(ex, "ReleasePointer failed for {Path}", Path);
            }

            _pointerAcquired = false;
            _basePointerView = null;
        }
    }

    private void ClearPointers()
    {
        _ptrCount = null;
        _ptrArray = null;
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
            throw new ResetPointersDisallowedException(
                $"ResetPointers is not allowed after DisallowResetPointers() has been called on {Path ?? "this ListMmf"}");

        // Note: Don't release pointer here - it should have been released before the view was disposed
        // Reset the flag since any old pointer is now invalid
        _pointerAcquired = false;
        _basePointerView = null;

        // First set BasePointerByte
        if (_view == null) throw new ListMmfException("Why?");
        var safeBuffer = _view.SafeMemoryMappedViewHandle;
        //RuntimeHelpers.PrepareConstrainedRegions();
        _basePointerView = null;

        // Acquire the pointer and track that we need to release it later
        safeBuffer.AcquirePointer(ref _basePointerView);
        _pointerAcquired = true;
        _basePointerView += _view.PointerOffset;
        _ptrCount = (long*)(_basePointerView + CountOffset);

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
        // Skip trimming if ResetPointers is disallowed or in read-only mode
        if (_isResetPointersDisallowed || IsReadOnly) return;

        var threshold = (long)(Capacity * 0.9);
        if (Count < threshold) Capacity = Count;
    }

    /// <summary>
    /// Initialize/Reset the _mmf file etc.
    /// </summary>
    protected void ResetView()
    {
        lock (SyncRoot)
        {
            BeginRemap();
            try
            {
                UnmapView();
                MapView();
            }
            catch (Exception ex)
            {
                FailRemap(ex);
                throw;
            }
        }
        ThrowIfCapacityBelowCount();
    }

    /// <summary>
    /// Maps a new view of _mmf, resets the pointers and publishes them to readers. Call under SyncRoot after BeginRemap.
    /// </summary>
    private void MapView()
    {
        var access = IsReadOnly ? MemoryMappedFileAccess.Read : MemoryMappedFileAccess.ReadWrite;
        _view = _mmf?.CreateViewAccessor(0, 0, access);
        if (!IsReadOnly && _fileStream!.Length != CapacityBytes)
            // Set the file length up to the view length so we don't write off the end
            _fileStream.SetLength(CapacityBytes);
        var totalHeaderBytes = ResetPointers();
        if (totalHeaderBytes != _parentHeaderBytes + HeaderBytesBase)
            throw new ListMmfException(
                $"{nameof(ResetPointers)} returns {totalHeaderBytes} but expected {_parentHeaderBytes + HeaderBytesBase}");
        _capacity = (CapacityBytes - _parentHeaderBytes - HeaderBytesBase)
                    / _width; // for the header fields just before the beginning of the array
        Volatile.Write(ref _mapping, new MmfMapping(_basePointerView, _ptrCount, _ptrArray));
        Volatile.Write(ref _remappingThreadId, 0);
    }

    private void ThrowIfCapacityBelowCount()
    {
        var count = Count;
        if (_capacity < count)
            throw new ListMmfException($"_capacity={_capacity:N0} cannot be less than Count={count:N0} for {this}");
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
        if (IsReadOnly)
            throw new NotSupportedException("Cannot grow capacity in read-only mode");

        // Check if ResetPointers has been disallowed
        if (_isResetPointersDisallowed)
            throw new ResetPointersDisallowedException(
                $"ResetPointers is not allowed after DisallowResetPointers() has been called on {Path ?? "this ListMmf"}");

        if (minCapacityItems <= _capacity)
            // nothing to do
            return;

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
        if (IsReadOnly)
            throw new NotSupportedException("Cannot reset capacity in read-only mode");

        if (newCapacityItems < Count)
            throw new ListMmfException($"newCapacityItems={newCapacityItems} cannot be less than Count={Count}");

        // Note that this method is called by TrimExcess() to shrink Capacity (but not below Count)
        var capacityBytes = CapacityItemsToBytes(newCapacityItems);
        if (capacityBytes == CapacityBytes)
            // No change, so no need to reset
            return;
        ResetMmfAndView(capacityBytes, newCapacityItems);
    }

    /// <summary>
    /// ResetMmfAndView
    /// </summary>
    /// <param name="capacityBytes"></param>
    /// <param name="newCapacityItems">Carried along for debugging</param>
    private void ResetMmfAndView(long capacityBytes, long newCapacityItems = 0)
    {
        if (IsReadOnly)
            throw new NotSupportedException("Cannot reset MMF in read-only mode");

        lock (SyncRoot)
        {
            var oldCapacityBytes = CapacityBytes;
            var oldCapacityItems = Capacity;
            var fi = new FileInfo(Path);

            BeginRemap();
            try
            {
                UnmapView();
                _mmf?.Dispose();
                _mmf = null;
                if (capacityBytes != 0 && capacityBytes < fi.Length)
                    // We want to shorten (Truncate) the file
                    try
                    {
                        _fileStream!.SetLength(capacityBytes);
                    }
                    catch (Exception ex)
                    {
                        // Ignore -- some reader may have this file open
                        _logger.LogWarning(ex, "Unable to shrink {Path} to {CapacityBytes:N0} bytes from {FileLength:N0}",
                            Path, capacityBytes, _fileStream!.Length);
                    }

                if (capacityBytes != 0 && capacityBytes < _fileStream!.Length)
                    // We can't open a file for ReadWrite with a smaller capacity, except 0
                    capacityBytes = _fileStream.Length;
                _mmf = MemoryMappedFile.CreateFromFile(_fileStream!, null, capacityBytes, MemoryMappedFileAccess.ReadWrite,
                    HandleInheritability.None, true);
                MapView();
            }
            catch (Exception ex)
            {
                FailRemap(ex);
                throw;
            }
        }
        ThrowIfCapacityBelowCount();
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
            // Round up to the next page
            result += PageSize - intoPage;
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
        return ReadItem(index);
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
        return ReadItem(index);
    }

    private T ReadItem(long index)
    {
        var slot = MmfReaders.Current;
        try
        {
            var mapping = EnterRead(slot);
            if (mapping != null)
            {
                return Unsafe.Read<T>(mapping.PtrArray + index * _width);
            }
        }
        finally
        {
            ExitRead(slot);
        }
        throw new ObjectDisposedException(nameof(ListMmfBase<T>));
    }

    /// <summary>
    /// Reads Count and, if 0 &lt;= index &lt; Count, the item at index, both from the same mapping.
    /// Returns false (with the Count it saw, 0 if disposed) when index is out of range.
    /// </summary>
    protected bool TryReadInBounds(long index, out T value, out long count)
    {
        var slot = MmfReaders.Current;
        try
        {
            var mapping = EnterRead(slot);
            count = mapping == null ? 0 : Volatile.Read(ref *mapping.PtrCount);
            if ((ulong)index >= (ulong)count)
            {
                value = default;
                return false;
            }
            value = Unsafe.Read<T>(mapping!.PtrArray + index * _width);
            return true;
        }
        finally
        {
            ExitRead(slot);
        }
    }

    /// <summary>
    /// Returns a ReadOnlySpan&lt;T&gt; representing a range of elements from the ListMmf.
    /// Provides zero-copy access to the underlying memory-mapped data.
    /// The span is only valid while the ListMmf remains unchanged and should be used within method scope.
    /// It points into the current mapping: using it after the list grows/remaps or is disposed reads unmapped memory.
    /// </summary>
    /// <param name="start">The starting index (inclusive)</param>
    /// <param name="length">The number of elements to include in the span</param>
    /// <returns>A ReadOnlySpan&lt;T&gt; representing the requested range</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start or length is invalid</exception>
    /// <exception cref="ListMmfOnlyInt32SupportedException">If length exceeds int.MaxValue</exception>
    public ReadOnlySpan<T> AsSpan(long start, int length)
    {
        // Take Count and the array pointer from the same mapping
        long count;
        byte* ptrArray;
        var slot = MmfReaders.Current;
        try
        {
            var mapping = EnterRead(slot);
            count = mapping == null ? 0 : Volatile.Read(ref *mapping.PtrCount);
            ptrArray = mapping == null ? null : mapping.PtrArray;
        }
        finally
        {
            ExitRead(slot);
        }

        // Bounds validation
        if (start < 0 || start > count)
            throw new ArgumentOutOfRangeException(nameof(start), $"start={start} must be >= 0 and <= Count={count}");
        if (length < 0) throw new ArgumentOutOfRangeException(nameof(length), "length must be >= 0");
        if (start + length > count)
            throw new ArgumentOutOfRangeException(nameof(length),
                $"start + length ({start + length}) exceeds Count={count}");

        // Create span from existing pointer arithmetic
        return new ReadOnlySpan<T>(ptrArray + start * _width, length);
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
    public ReadOnlySpan<T> AsSpan(long start)
    {
        var count = Count;
        var length = count - start;
        if (length > int.MaxValue) throw new ListMmfOnlyInt32SupportedException(length);

        return AsSpan(start, (int)length);
    }

    /// <summary>
    /// Legacy alias retained for backward compatibility. Prefer <see cref="AsSpan(long,int)"/>.
    /// </summary>
    /// <param name="start">The starting index (inclusive)</param>
    /// <param name="length">The number of elements to include in the span</param>
    /// <returns>A ReadOnlySpan&lt;T&gt; representing the requested range</returns>
    public ReadOnlySpan<T> GetRange(long start, int length)
    {
        return AsSpan(start, length);
    }

    /// <summary>
    /// Legacy alias retained for backward compatibility. Prefer <see cref="AsSpan(long)"/>.
    /// </summary>
    /// <param name="start">The starting index (inclusive)</param>
    /// <returns>A ReadOnlySpan&lt;T&gt; representing elements from start to the end</returns>
    public ReadOnlySpan<T> GetRange(long start)
    {
        return AsSpan(start);
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
        if (IsReadOnly)
            throw new NotSupportedException("Cannot truncate in read-only mode");

        // if (this is ListMmfTimeSeriesDateTimeSeconds && Path.Contains(@"\1T\"))
        // {
        // }
        var count = Count;
        if (newCount >= count)
            // nothing to do
            return;
        if (newCount < 0) throw new ArgumentException("Truncate new length cannot be negative");
        if (newCount > _capacity)
            throw new ListMmfException($"Truncate new length {newCount} cannot be greater than Capacity {_capacity}");

        // Change Count first so readers won't use a wrong value
        // Capacity and the mapping are left alone: remapping would invalidate pointers/spans held by readers
        // (and throws after DisallowResetPointers). Use TrimExcess() or Capacity to reclaim space explicitly.
        Count = newCount;
    }

    /// <inheritdoc cref="IListMmf{T}" />
    public void TruncateBeginning(long newCount, IProgress<long>? progress = null)
    {
        if (IsReadOnly)
            throw new NotSupportedException("Cannot truncate beginning in read-only mode");

        var count = Count;
        if (newCount > count)
            throw new ListMmfException($"TruncateBeginning {newCount} must not be greater than Count={count}");

        if (newCount == count) return; // No-op optimization

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
        lock (SyncRoot)
        {
            if (disposing && !_isDisposed)
            {
                // Only trim if ResetPointers is allowed
                if (!_isResetPointersDisallowed) TrimExcess();
                _isDisposed = true;

                // From here Count returns 0 and reads throw ObjectDisposedException
                PublishAndWaitForReaders(null);
                UnmapView(); // the view must be disposed before _mmf
                _mmf?.Dispose();
                _mmf = null;
                _fileStream?.Dispose();
                _fileStream = null;

                _exclusiveFileLock?.Dispose();

                base.Dispose(true);
                // GC.Collect();
                // GC.WaitForPendingFinalizers();
            }
        }
    }

    /// <summary>
    /// Accessing Count in the debugger ToString() can crash the debugger. Use GetInfo() for that.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        var result = _accessName;
#if DEBUG
        result += $" {InstanceId} {Count:N0}";
#endif
        return result;
    }

    public string GetInfo()
    {
        var count = Count;
        var result = $"count={count:N0}/_capacity={_capacity:N0} {_accessName}";
#if DEBUG
        result += $" {InstanceId}";
#endif
        return result;
    }
}
