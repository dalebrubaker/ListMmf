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
            return CreateFromFile(fileStream, mapName, capacityElements, access, false, headerReserve, noLocking);
        }

        public static ListMmf<T> CreateFromFile(FileStream fileStream, string mapName = null, long capacityElements = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, bool leaveOpen = false,
            long headerReserve = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            var sizeOfT = Unsafe.SizeOf<T>();
            var capacity = capacityElements * sizeOfT;
            if (fileStream.Length > capacity)

                // Don't allow a crash because the user requested fewer elements than the file alreaady supports
                capacity = fileStream.Length;
            var mmf = MemoryMappedFile.CreateFromFile(fileStream, mapName, capacity, access, HandleInheritability.None, leaveOpen);
            return new ListMmf<T>(headerReserve, noLocking, mmf, access, fileStream, mapName, leaveOpen);
        }

        public static ListMmf<T> CreateNew(string mapName, long capacityElements, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            var sizeOfT = Unsafe.SizeOf<T>();
            var capacity = capacityElements * sizeOfT;
            var mmf = MemoryMappedFile.CreateNew(mapName, capacity, access);
            return new ListMmf<T>(headerReserve, noLocking, mmf, access, null, mapName);
        }

        public static ListMmf<T> CreateOrOpen(string mapName, long capacityElements, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            var sizeOfT = Unsafe.SizeOf<T>();
            var capacity = capacityElements * sizeOfT;
            var mmf = MemoryMappedFile.CreateOrOpen(mapName, capacity, access);
            return new ListMmf<T>(headerReserve, noLocking, mmf, access, null, mapName);
        }

        public static ListMmf<T> OpenExisting(string mapName, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            if (access != MemoryMappedFileAccess.ReadWrite && access != MemoryMappedFileAccess.Read)
                throw new ArgumentOutOfRangeException(nameof(access), "Only Read and ReadWrite access are allowed.");
            var desiredAccessRights = access == MemoryMappedFileAccess.Read ? MemoryMappedFileRights.Read : MemoryMappedFileRights.ReadWrite;
            var mmf = MemoryMappedFile.OpenExisting(mapName, desiredAccessRights);
            return new ListMmf<T>(headerReserve, noLocking, mmf, access, null, mapName);
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

        private static bool IsCompatibleObject(object value)
        {
            // Non-null values are fine.  Only accept nulls if T is a class or Nullable<U>.
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>. 
            return value is T;
        }
    }
}