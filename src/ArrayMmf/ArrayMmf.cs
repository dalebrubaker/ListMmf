using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using BruSoftware.ArrayMmf.Interfaces;

namespace BruSoftware.ArrayMmf
{
    public class ArrayMmf<T> : IMmfArray<T> where T:struct
    {
        protected MemoryMappedFile Mmf;


        // Note from safebuffer.cs about locking
        // This design allows multiple
        // threads to read and write memory simultaneously without locks (as long as
        // you don't write to a region of memory that overlaps with what another
        // thread is accessing).
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerReserve"></param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        private ArrayMmf(long headerReserve = 0, bool noLocking = false)
        {
            //Mmf = MemoryMappedFile.CreateFromFile("Test", FileMode.Append, "mapName", 1000);
        }

        public static ArrayMmf<T> CreateFromFile(string path, FileMode mode = FileMode.Open, string mapName = null, long capacity = 0, 
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, 
            long headerReserve = 0, bool noLocking = false)
        {
            return new ArrayMmf<T>(headerReserve);
        }

        public static ArrayMmf<T> CreateFromFile(FileStream fileStream, string mapName = null, long capacity = 0, 
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, bool leaveOpen = false,
            long headerReserve = 0, bool noLocking = false)
        {
            return new ArrayMmf<T>(headerReserve);
        }

        public static ArrayMmf<T> OpenExisting(string mapName, MemoryMappedFileRights memoryMappedFileRights = MemoryMappedFileRights.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            return new ArrayMmf<T>(headerReserve);
        }

        public static ArrayMmf<T> CreateNew(string mapName, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, 
            long headerReserve = 0, bool noLocking = false)
        {
            return new ArrayMmf<T>(headerReserve);
        }

        public static ArrayMmf<T> CreateOrOpen(string mapName, long capacity, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, 
            long headerReserve = 0, bool noLocking = false)
        {
            return new ArrayMmf<T>(headerReserve);
        }

        public long Length { get; }
        public bool IsReadOnly { get; }
        public void Clear()
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(T item)
        {
            throw new NotImplementedException();
        }

        public void AddRange(IEnumerable<T> collection)
        {
            throw new NotImplementedException();
        }

        public void AddRange(IReadOnlyList64<T> list)
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(T[] array, long arrayIndex)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(int index, T[] array, int arrayIndex, long count)
        {
            throw new NotImplementedException();
        }

        public long IndexOf(T item)
        {
            throw new NotImplementedException();
        }

        public T this[long index]
        {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }

        public IReadOnlyList64<T> GetReadOnlyList64(long lowerBound, long count = Int64.MaxValue)
        {
            throw new NotImplementedException();
        }

        public long Count => Length;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
