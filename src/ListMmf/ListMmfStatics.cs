using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

// ReSharper disable ConvertToUsingDeclaration

namespace BruSoftware.ListMmf
{
    public partial class ListMmf<T>
    {
        /// <summary>
        /// Create a file-backed ListMmf class.
        /// </summary>
        /// <param name="path">The path to a file-backed (persistent) memory mapped file.</param>
        /// <param name="mapName">The system-wide unique name for the memory mapped file, or <c>null</c> if you do not intend to share across processes.</param>
        /// <param name="capacityElements">The total number of elements the file can hold without resizing.</param>
        /// <param name="access">Either Read or ReadWrite.</param>
        /// <param name="headerReserveBytes">The number of bytes reserved at the front of this file for use by others. Must be evenly divisible by 8.</param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        /// <param name="maximumCount">The maximum number of simultaneous lists to open with this name and access</param>
        /// <param name="cancellationToken">allows cancellation when blocking because maximumCount are already open</param>
        /// <param name="timeout">timeout in milliseconds (-1 is Infinite) applicable when blocking because maximumCount are already open</param>
        /// <returns>A ListMmf class, or <c>null</c> if cancelled or timed out</returns>
        public static ListMmf<T> CreateFromFile(string path, string mapName = null, long capacityElements = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserveBytes = 0, bool noLocking = false, int maximumCount = int.MaxValue, CancellationToken cancellationToken = default, int timeout = -1)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            var existed = File.Exists(path);
            var fileStream = CreateFileStreamFromPath(path, access);
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

            // We ALWAYS leave the fileStream open internally so we can re-create the _mmf when we grow the array.
            return CreateFromFile(fileStream, mapName, capacityElements, access, true, headerReserveBytes, noLocking);
        }

        /// <summary>
        /// Create a file-backed ListMmf class using fileStream instead of path
        /// </summary>
        /// <param name="fileStream">The FileStream to a file-backed (persistent) memory mapped file.</param>
        /// <param name="mapName">The system-wide unique name for the memory mapped file, or <c>null</c> if you do not intend to share across processes.</param>
        /// <param name="capacityElements">The total number of elements the file can hold without resizing.</param>
        /// <param name="access">Either Read or ReadWrite.</param>
        /// <param name="leaveOpen">Set <c>true</c> to leave the fileStream open after the ListMmf class is disposed.</param>
        /// <param name="headerReserveBytes">The number of bytes reserved at the front of this file for use by others. Must be evenly divisible by 8.</param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        /// <param name="maximumCount">The maximum number of simultaneous lists to open with this name and access</param>
        /// <param name="cancellationToken">allows cancellation when blocking because maximumCount are already open</param>
        /// <param name="timeout">timeout in milliseconds (-1 is Infinite) applicable when blocking because maximumCount are already open</param>
        /// <returns>A ListMmf class, or <c>null</c> if cancelled or timed out</returns>
        public static ListMmf<T> CreateFromFile(FileStream fileStream, string mapName = null, long capacityElements = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, bool leaveOpen = false,
            long headerReserveBytes = 0, bool noLocking = false, int maximumCount = int.MaxValue, CancellationToken cancellationToken = default, int timeout = -1)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            var semaphore = GetSemaphore(fileStream.Name, access == MemoryMappedFileAccess.Read, maximumCount, cancellationToken, timeout);
            if (semaphore == null)
            {
                return null;
            }
            var capacityBytes = CapacityElementsToBytes(capacityElements, headerReserveBytes);
            if (fileStream.Length > capacityBytes)
            {
                // Don't allow a crash because the user requested fewer elements than the file already supports
                capacityBytes = fileStream.Length;
            }

            // We ALWAYS leave the fileStream open internally so we can re-create the _mmf when we grow the array.
            var mmf = MemoryMappedFile.CreateFromFile(fileStream, mapName, capacityBytes, access, HandleInheritability.None, leaveOpen);
            return new ListMmf<T>(semaphore, headerReserveBytes, noLocking, mmf, access, fileStream, mapName);
        }

        /// <summary>
        /// Create a memory-backed ListMmf class
        /// A memory (not-persisted) ListMmf can NOT be expanded. You can only add elements until Capacity is reached.
        /// </summary>
        /// <param name="mapName">A name to assign to the memory-mapped file.</param>
        /// <param name="capacityElements">The total number of elements the file can hold without resizing.</param>
        /// <param name="access">Either Read or ReadWrite.</param>
        /// <param name="headerReserveBytes">The number of bytes reserved at the front of this file for use by others. Must be evenly divisible by 8.</param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        /// <param name="maximumCount">The maximum number of simultaneous lists to open with this name and access</param>
        /// <param name="cancellationToken">allows cancellation when blocking because maximumCount are already open</param>
        /// <param name="timeout">timeout in milliseconds (-1 is Infinite) applicable when blocking because maximumCount are already open</param>
        /// <returns>A ListMmf class, or <c>null</c> if cancelled or timed out</returns>
        public static ListMmf<T> CreateNew(string mapName, long capacityElements, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserveBytes = 0, bool noLocking = false, int maximumCount = int.MaxValue, CancellationToken cancellationToken = default, int timeout = -1)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            var semaphore = GetSemaphore(mapName, access == MemoryMappedFileAccess.Read, maximumCount, cancellationToken, timeout);
            if (semaphore == null)
            {
                return null;
            }
            var capacityBytes = CapacityElementsToBytes(capacityElements, headerReserveBytes);
            var mmf = MemoryMappedFile.CreateNew(mapName, capacityBytes, access);
            return new ListMmf<T>(semaphore, headerReserveBytes, noLocking, mmf, access, null, mapName);
        }

        /// <summary>
        /// Create a memory-backed ListMmf class
        /// A memory (not-persisted) ListMmf can NOT be expanded. You can only add elements until Capacity is reached.
        /// </summary>
        /// <param name="mapName">A name to assign to the memory-mapped file.</param>
        /// <param name="capacityElements">The total number of elements the file can hold without resizing.</param>
        /// <param name="access">Either Read or ReadWrite.</param>
        /// <param name="headerReserveBytes">The number of bytes reserved at the front of this file for use by others. Must be evenly divisible by 8.</param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        /// <param name="maximumCount">The maximum number of simultaneous lists to open with this name and access</param>
        /// <param name="cancellationToken">allows cancellation when blocking because maximumCount are already open</param>
        /// <param name="timeout">timeout in milliseconds (-1 is Infinite) applicable when blocking because maximumCount are already open</param>
        /// <returns>A ListMmf class, or <c>null</c> if cancelled or timed out</returns>
        public static ListMmf<T> CreateOrOpen(string mapName, long capacityElements, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserveBytes = 0, bool noLocking = false, int maximumCount = int.MaxValue, CancellationToken cancellationToken = default, int timeout = -1)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            var semaphore = GetSemaphore(mapName, access == MemoryMappedFileAccess.Read, maximumCount, cancellationToken, timeout);
            if (semaphore == null)
            {
                return null;
            }
            var capacityBytes = CapacityElementsToBytes(capacityElements, headerReserveBytes);
            var mmf = MemoryMappedFile.CreateOrOpen(mapName, capacityBytes, access);
            return new ListMmf<T>(semaphore, headerReserveBytes, noLocking, mmf, access, null, mapName);
        }

        /// <summary>
        /// Open an existing memory-backed ListMmf class
        /// A memory (not-persisted) ListMmf can NOT be expanded. You can only add elements until Capacity is reached.
        /// </summary>
        /// <param name="mapName">A name to assign to the memory-mapped file.</param>
        /// <param name="access">Either Read or ReadWrite.</param>
        /// <param name="headerReserveBytes">The number of bytes reserved at the front of this file for use by others. Must be evenly divisible by 8.</param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        /// <param name="maximumCount">The maximum number of simultaneous lists to open with this name and access</param>
        /// <param name="cancellationToken">allows cancellation when blocking because maximumCount are already open</param>
        /// <param name="timeout">timeout in milliseconds (-1 is Infinite) applicable when blocking because maximumCount are already open</param>
        /// <returns>A ListMmf class, or <c>null</c> if cancelled or timed out</returns>
        public static ListMmf<T> OpenExisting(string mapName, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserveBytes = 0, bool noLocking = false, int maximumCount = int.MaxValue, CancellationToken cancellationToken = default, int timeout = -1)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            var semaphore = GetSemaphore(mapName, access == MemoryMappedFileAccess.Read, maximumCount, cancellationToken, timeout);
            if (semaphore == null)
            {
                return null;
            }
            var desiredAccessRights = access == MemoryMappedFileAccess.Read ? MemoryMappedFileRights.Read : MemoryMappedFileRights.ReadWrite;
            var mmf = MemoryMappedFile.OpenExisting(mapName, desiredAccessRights);
            return new ListMmf<T>(semaphore, headerReserveBytes, noLocking, mmf, access, null, mapName);
        }

        private static FileStream CreateFileStreamFromPath(string path, MemoryMappedFileAccess access)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ListMmfException("A path is required.");
            }
            var fileMode = access == MemoryMappedFileAccess.ReadWrite ? FileMode.OpenOrCreate : FileMode.Open;
            var fileAccess = access == MemoryMappedFileAccess.ReadWrite ? FileAccess.ReadWrite : FileAccess.Read;
            var fileShare = access == MemoryMappedFileAccess.ReadWrite ? FileShare.Read : FileShare.ReadWrite;
            if (access == MemoryMappedFileAccess.Read && !File.Exists(path))
            {
                throw new ListMmfException($"Attempt to read non-existing file: {path}");
            }
            if (access == MemoryMappedFileAccess.ReadWrite)
            {
                // Create the directory if needed
                var directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
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
            if (!existed)
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Return the capacity in bytes needed to store capacityElements, rounded up to the 4096 page size used by MMF
        /// </summary>
        /// <param name="capacityElements"></param>
        /// <param name="headerReserveBytes"></param>
        /// <returns></returns>
        private static long CapacityElementsToBytes(long capacityElements, long headerReserveBytes)
        {
            var result = capacityElements * Unsafe.SizeOf<T>() + headerReserveBytes + 8; // 8 for the Count field just before the beginning of the array
            var intoPage = result % 4096;
            if (intoPage > 0)
            {
                // Round up to the next page
                result += 4096 - intoPage;
            }
            return result;
        }

        /// <summary>
        /// Return the capacity in elements that will fit within capacityBytes
        /// </summary>
        /// <param name="capacityBytes"></param>
        /// <param name="headerReserveBytes"></param>
        /// <returns></returns>
        private static long CapacityBytesToElements(long capacityBytes, long headerReserveBytes)
        {
            var result = (capacityBytes - headerReserveBytes - 8) / Unsafe.SizeOf<T>(); // 8 for the Count field just before the beginning of the array
            return result;
        }

        /// <summary>
        /// BlockUntilAvailableCancelledOrTimeout on semaphoreUnique until it is signaled (another user of this semaphore disposed/released), timed out or cancelled
        /// Thanks to https://docs.microsoft.com/en-us/dotnet/standard/threading/how-to-listen-for-cancellation-requests-that-have-wait-handles
        /// </summary>
        /// <param name="semaphore"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="timeout">-1 means infinite</param>
        /// <exception cref="OperationCanceledException">if cancelled</exception>
        /// <exception cref="TimeoutException">if timeout</exception>
        public static void BlockUntilAvailableCancelledOrTimedout(Semaphore semaphore, CancellationToken cancellationToken, int timeout = -1)
        {
            var eventThatSignaledIndex = WaitHandle.WaitAny(new[]
            {
                semaphore,
                cancellationToken.WaitHandle
            }, timeout);
            if (eventThatSignaledIndex == 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (eventThatSignaledIndex == WaitHandle.WaitTimeout)
            {
                throw new TimeoutException();
            }
        }

        private static Semaphore GetSemaphore(string pathOrMapName, bool isReadOnly, int maximumCount, CancellationToken cancellationToken, int timeout)
        {
            try
            {
                var semaphoreName = GetSemaphoreName(pathOrMapName, isReadOnly);
                var semaphore = new Semaphore(1, maximumCount, semaphoreName);
                BlockUntilAvailableCancelledOrTimedout(semaphore, cancellationToken, timeout);
                return semaphore;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (TimeoutException)
            {
                return null;
            }
        }

        private static bool IsCompatibleObject(object value)
        {
            // Non-null values are fine.  Only accept nulls if T is a class or Nullable<U>.
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>. 
            return value is T;
        }
    }
}
