using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using BruSoftware.ListMmf;

namespace ListMmfTests
{
    /// <summary>
    /// This class make a file-backed Mmf in the current directory with a Guid string for path and mapName.
    /// The file is Deleted upon Dispose()
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class TestListMmf<T> : ListMmf<T> where T : struct
    {
        protected TestListMmf(Semaphore semaphore, long headerReserveBytes, bool noLocking, MemoryMappedFile mmf, MemoryMappedFileAccess access, FileStream fileStream,
            string mapName, bool leaveOpen = false)
            : base(semaphore, headerReserveBytes, noLocking, mmf, access, fileStream, mapName, leaveOpen)
        {
        }

        public static TestListMmf<T> CreateTestFile(long capacityItems = 0)
        {
            var guid = Guid.NewGuid();
            var path = guid.ToString();
            string mapName = null;
            var access = MemoryMappedFileAccess.ReadWrite;
            var headerReserveBytes = 0;
            var noLocking = false;
            var maximumCount = 1;
            var cancellationToken = default(CancellationToken);
            var timeout = -1;
            var fileStream = CreateFileStreamFromPath(path, MemoryMappedFileAccess.ReadWrite);
            var leaveOpen = false;

            var semaphore = GetSemaphore(fileStream.Name, access == MemoryMappedFileAccess.Read, maximumCount, cancellationToken, timeout);
            if (semaphore == null)
            {
                return null;
            }
            var capacityBytes = CapacityItemsToBytes(capacityItems, headerReserveBytes);
            if (fileStream.Length > capacityBytes)
            {
                // Don't allow a crash because the user requested fewer items than the file already supports
                capacityBytes = fileStream.Length;
            }

            // We ALWAYS leave the fileStream open internally so we can re-create the _mmf when we grow the array.
            var mmf = MemoryMappedFile.CreateFromFile(fileStream, mapName, capacityBytes, access, HandleInheritability.None, true);
            return new TestListMmf<T>(semaphore, headerReserveBytes, noLocking, mmf, access, fileStream, mapName, leaveOpen);
        }

        public static TestListMmf<T> CreateTestFile(IEnumerable<T> collection)
        {
            var result = CreateTestFile();
            result.AddRange(collection);
            return result;
        }

        protected override void ResetPointers()
        {
            base.ResetPointers();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var name = Name;
                base.Dispose(true);
                try
                {
                    File.Delete(name);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                GC.SuppressFinalize(this);
            }
        }
    }
}
