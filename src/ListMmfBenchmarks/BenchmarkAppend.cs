using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace ListMmfBenchmarks
{
    public unsafe class BenchmarkAppend
    {
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _mmva;
        private long* _basePointerInt64;
        private FileStream _fs;

        [GlobalSetup]
        public void GlobalSetup()
        {
            if (!Environment.Is64BitOperatingSystem)
                throw new Exception("Not supported on 32-bit operating system. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            if (!Environment.Is64BitProcess) throw new Exception("Not supported on 32-bit process. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            const string testFilePath = @"D:\_HugeArray\TestApppend.dat"; 
            _fs = new FileStream(testFilePath, FileMode.Create);
            var count = (int)_fs.Length / 8;
            Console.WriteLine($"{count:N0} longs are in {testFilePath}");
            var numElements = 1000;
            CreateMmf(numElements);
        }

        private void CreateMmf(long numElements)
        {
            _mmf = MemoryMappedFile.CreateFromFile(_fs, null, numElements * 8, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);

            //_mmf = MemoryMappedFile.CreateFromFile(testFilePath, FileMode.Open,null, 0, MemoryMappedFileAccess.Read);
            //_mmva = _mmf.CreateViewAccessor(0, count * 8, MemoryMappedFileAccess.Read);
            //_mmva = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            // If I open with 0 size, I get IOException, not enough memory with 32 bit process but no problem 64 bit
            var length = _fs.Length;
            _mmva = _mmf.CreateViewAccessor(); // 0 offset, 0 size (all file), ReadWrite

            var safeBuffer = _mmva.SafeMemoryMappedViewHandle;
            byte* basePointerByte = null;
            RuntimeHelpers.PrepareConstrainedRegions();
            safeBuffer.AcquirePointer(ref basePointerByte);
            basePointerByte += _mmva.PointerOffset; // adjust for the extraMemNeeded
            _basePointerInt64 = (long*)basePointerByte;
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            _mmva.Dispose();
            _mmf.Dispose();
        }




        /// <summary>
        /// Read: 154.9 ms for 10 million vs 157.1 for unsafe pointer
        /// </summary>
        [Benchmark]
        public void Append()
        {
            var length = _fs.Length;
            var index = length / 8 - 1; // this is index of longs, not byte
            Unsafe.Write(_basePointerInt64 + index, index);
            var value = Unsafe.Read<long>(_basePointerInt64 + index);


            // This doesn't work!_fs.SetLength(2000 * 8);
            //var length2 = _fs.Length;
            //var index2 = length2 / 8 - 1; // this is index of longs, not byte
            //Unsafe.Write(_basePointerInt64 + index2, index2);
            //var value2 = Unsafe.Read<long>(_basePointerInt64 + index2);

            CreateMmf(20000000000); // Need to reset Mmf and View to increase file size
            var length2 = _fs.Length;
            var index2 = length2 / 8 - 1; // this is index of longs, not byte
            Unsafe.Write(_basePointerInt64 + index2, index2);
            var value2 = Unsafe.Read<long>(_basePointerInt64 + index2);

        }
    }
}