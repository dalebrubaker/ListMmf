using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using BruSoftware.ListMmf.Interfaces;

namespace BruSoftware.ListMmf
{
    public unsafe class ListMmf<T> : IMmfArray<T> where T : struct
    {
        private MemoryMappedFile _mmf;

        /// <summary>
        /// This is the beginning of the View, before the headerReserveBytes and the 8-byte Length of this array 
        /// </summary>
        protected byte* BasePointerByte;

        // Note from safebuffer.cs about locking
        // This design allows multiple
        // threads to read and write memory simultaneously without locks (as long as
        // you don't write to a region of memory that overlaps with what another
        // thread is accessing).

        /// <summary>
        /// 
        /// </summary>
        /// <param name="headerReserveBytes"></param>
        /// <param name="noLocking"><c>true</c> when your design ensures that reading and writing cannot be happening at the same location in the file, system-wide</param>
        private ListMmf(long headerReserveBytes = 0, bool noLocking = false)
        {
            _mmf = null;

            //_mmf = MemoryMappedFile.CreateFromFile("Test", FileMode.Append, "mapName", 1000);
        }

        /// <summary>
        /// This method is called whenever Mmf and View are changed.
        /// Inheritors should first call base.ResetPointers() and then reset their own pointers (if any) from BasePointerByte
        /// </summary>
        public virtual void ResetPointers()
        {
            BasePointerByte = GetPointer(null);
        }

        public static ListMmf<T> CreateFromFile(string path, FileMode mode = FileMode.Open, string mapName = null, long capacity = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            return new ListMmf<T>(headerReserve);
        }

        public static ListMmf<T> CreateFromFile(FileStream fileStream, string mapName = null, long capacity = 0,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, bool leaveOpen = false,
            long headerReserve = 0, bool noLocking = false)
        {
            return new ListMmf<T>(headerReserve);
        }

        public static ListMmf<T> OpenExisting(string mapName, MemoryMappedFileRights memoryMappedFileRights = MemoryMappedFileRights.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            return new ListMmf<T>(headerReserve);
        }

        public static ListMmf<T> CreateNew(string mapName, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            return new ListMmf<T>(headerReserve);
        }

        public static ListMmf<T> CreateOrOpen(string mapName, long capacity, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite,
            long headerReserve = 0, bool noLocking = false)
        {
            return new ListMmf<T>(headerReserve);
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

        public IReadOnlyList64<T> GetReadOnlyList64(long lowerBound, long count = long.MaxValue)
        {
            throw new NotImplementedException();
        }

        public long Count => Length;

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
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}