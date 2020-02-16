using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace BruSoftware.ListMmf
{
    public partial class ListMmf<T>
    {
        public static ListMmf<T> CreateFromFile(string path, string mapName = null, long capacityElements = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserveBytes = 0, bool noLocking = false)
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

        public static ListMmf<T> CreateFromFile(FileStream fileStream, string mapName = null, long capacityElements = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, bool leaveOpen = false,
            long headerReserveBytes = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            var capacityBytes = CapacityElementsToBytes(capacityElements, headerReserveBytes);
            if (fileStream.Length > capacityBytes)
            {
                // Don't allow a crash because the user requested fewer elements than the file already supports
                capacityBytes = fileStream.Length;
            }

            // We ALWAYS leave the fileStream open internally so we can re-create the _mmf when we grow the array.
            var mmf = MemoryMappedFile.CreateFromFile(fileStream, mapName, capacityBytes, access, HandleInheritability.None, true);
            return new ListMmf<T>(headerReserveBytes, noLocking, mmf, access, fileStream, mapName);
        }

        /// <summary>
        /// A memory (not-persisted) ListMmf can NOT be expanded. You can only add elements until Capacity is reached.
        /// </summary>
        /// <param name="mapName"></param>
        /// <param name="capacityElements"></param>
        /// <param name="access"></param>
        /// <param name="headerReserveBytes"></param>
        /// <param name="noLocking"></param>
        /// <returns></returns>
        public static ListMmf<T> CreateNew(string mapName, long capacityElements, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserveBytes = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            var capacityBytes = CapacityElementsToBytes(capacityElements, headerReserveBytes);
            var mmf = MemoryMappedFile.CreateNew(mapName, capacityBytes, access);
            return new ListMmf<T>(headerReserveBytes, noLocking, mmf, access, null, mapName);
        }

        /// <summary>
        /// A memory (not-persisted) ListMmf can NOT be expanded. You can only add elements until Capacity is reached.
        /// </summary>
        /// <param name="mapName"></param>
        /// <param name="capacityElements"></param>
        /// <param name="access"></param>
        /// <param name="headerReserveBytes"></param>
        /// <param name="noLocking"></param>
        /// <returns></returns>
        public static ListMmf<T> CreateOrOpen(string mapName, long capacityElements, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserveBytes = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            var capacityBytes = CapacityElementsToBytes(capacityElements, headerReserveBytes);
            var mmf = MemoryMappedFile.CreateOrOpen(mapName, capacityBytes, access);
            return new ListMmf<T>(headerReserveBytes, noLocking, mmf, access, null, mapName);
        }
        
        /// <summary>
        /// A memory (not-persisted) ListMmf can NOT be expanded. You can only add elements until Capacity is reached.
        /// </summary>
        /// <param name="mapName"></param>
        /// <param name="access"></param>
        /// <param name="headerReserve"></param>
        /// <param name="noLocking"></param>
        /// <returns></returns>
        public static ListMmf<T> OpenExisting(string mapName, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
            {
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            }
            var desiredAccessRights = access == MemoryMappedFileAccess.Read ? MemoryMappedFileRights.Read : MemoryMappedFileRights.ReadWrite;
            var mmf = MemoryMappedFile.OpenExisting(mapName, desiredAccessRights);
            return new ListMmf<T>(headerReserve, noLocking, mmf, access, null, mapName);
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

        private static bool IsCompatibleObject(object value)
        {
            // Non-null values are fine.  Only accept nulls if T is a class or Nullable<U>.
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>. 
            return value is T;
        }
    }
}
