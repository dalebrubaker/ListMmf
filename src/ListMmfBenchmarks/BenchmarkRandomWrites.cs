using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace ListMmfBenchmarks
{
    public unsafe class BenchmarkRandomWrites
    {
        private int[] _testIndexes;
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _mmva;
        private long* _basePointerInt64;

        [GlobalSetup]
        public void GlobalSetup()
        {
            if (!Environment.Is64BitOperatingSystem)
                throw new Exception("Not supported on 32-bit operating system. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            if (!Environment.Is64BitProcess) throw new Exception("Not supported on 32-bit process. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            const string testFilePath = @"D:\_HugeArray\Timestamps.btd"; // 9.91 GB of longs
            const int numTests = 10000000;
            var fs = new FileStream(testFilePath, FileMode.Open);
            var count = (int)(fs.Length / 8);

            //_fs.Dispose();
            Console.WriteLine($"{count:N0} longs are in {testFilePath}");
            var random = new Random(1);
            _testIndexes = new int[numTests];
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = random.Next(0, count);
                _testIndexes[i] = index;
            }
            _mmf = MemoryMappedFile.CreateFromFile(fs, null, fs.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);

            //_mmf = MemoryMappedFile.CreateFromFile(testFilePath, FileMode.Open,null, 0, MemoryMappedFileAccess.Read);
            //_mmva = _mmf.CreateViewAccessor(0, count * 8, MemoryMappedFileAccess.Read);
            //_mmva = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            // If I open with 0 size, I get IOException, not enough memory with 32 bit process but no problem 64 bit
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
        /// Read: 154.9 ms for 10 million
        /// Read then Write: 187.9 ms for 10 million
        /// </summary>
        [Benchmark]
        public void ReadWriteRandomMemoryMappedUnsafeGeneric()
        {
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];
                var value = Unsafe.Read<long>(_basePointerInt64 + index);
                Unsafe.Write(_basePointerInt64 + index, value);
                //var valueCheck = Unsafe.Read<long>(_basePointerInt64 + index);
                if (value < 1)

                    // To avoid optimizing away the read
                    break;
            }
        }


    }
}