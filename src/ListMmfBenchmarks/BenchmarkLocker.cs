using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BruSoftware.ListMmf;

namespace ListMmfBenchmarks
{
    public unsafe class BenchmarkLocker
    {
        private FileStream _fs;
        private BinaryReader _br;
        private int[] _testIndexes;
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _mmva;
        private long* _basePointerInt64;
        private Locker _lockerNoLock;
        private readonly object _lock = new object();
        private Locker _lockerLock;
        private Mutex _mutex;
        private Locker _lockerMutex;
        private Locker _lockerSemaphore;
        private Semaphore _semaphore;

        [GlobalSetup]
        public void GlobalSetup()
        {
            if (!Environment.Is64BitOperatingSystem)
                throw new Exception("Not supported on 32-bit operating system. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            if (!Environment.Is64BitProcess) throw new Exception("Not supported on 32-bit process. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            _lockerNoLock = new Locker();
            _lockerLock = new Locker(_lock);
            _mutex = new Mutex(false, "Test");
            _lockerMutex = new Locker(() => _mutex.WaitOne(), () => _mutex.ReleaseMutex());
            var systemWideSemaphoreName = "TestSystemWideSemaphoreName";
            _semaphore = new Semaphore(1, 1, systemWideSemaphoreName, out var createdNew);
            //_semaphore.WaitOne(1000);
            //var countSemaphore = _semaphore.Release(1);
            if (!createdNew)
            {
                _semaphore.Release();
                throw new ArgumentException($"{systemWideSemaphoreName} semaphore already exists", nameof(systemWideSemaphoreName));
            }
            _lockerSemaphore = new Locker(_semaphore);
            const string testFilePath = @"D:\_HugeArray\Timestamps.btd"; // 11.0 GB of longs
            const int numTests = 1000000;
            _fs = new FileStream(testFilePath, FileMode.Open);
            _br = new BinaryReader(_fs);
            var count = (int)(_fs.Length / 8);

            //_fs.Dispose();
            Console.WriteLine($"{count:N0} longs are in {testFilePath}");
            var random = new Random(1);
            _testIndexes = new int[numTests];
            for (int i = 0; i < numTests; i++)
            {
                var index = random.Next(0, count);
                _testIndexes[i] = index;
            }
            _mmf = MemoryMappedFile.CreateFromFile(_fs, null, _fs.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);

            //_mmf = MemoryMappedFile.CreateFromFile(testFilePath, FileMode.Open,null, 0, MemoryMappedFileAccess.Read);
            //_mmva = _mmf.CreateViewAccessor(0, count * 8, MemoryMappedFileAccess.Read);
            // If I open with 0 size, I get IOException, not enough memory with 32 bit process but no problem 64 bit
            //_mmva = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
            _mmva = _mmf.CreateViewAccessor(); // 0 offset, 0 size (all file), ReadWrite

            // Read vs ReadWrite has NO impact on timings

            var safeBuffer = _mmva.SafeMemoryMappedViewHandle;
            byte* basePointerByte = null;
            RuntimeHelpers.PrepareConstrainedRegions();
            safeBuffer.AcquirePointer(ref basePointerByte);
            basePointerByte += _mmva.PointerOffset; // adjust for the extraMemNeeded
            _basePointerInt64 = (long*)basePointerByte;

            //var fileLength = _fs.Length;
            //var viewLength = (long)safeBuffer.ByteLength;
            //var viewLonger = viewLength - fileLength;
            //var capacity = _mmva.Capacity; // same as viewLength
            //var isClosed = safeBuffer.IsClosed;
            //var isInvalid = safeBuffer.IsInvalid;
        }

        [GlobalCleanup]
        public void GlobalCleanup()
        {
            var fileLength = _fs.Length;
            var safeBuffer = _mmva.SafeMemoryMappedViewHandle;
            _fs.Dispose();
            _mmva.Dispose();
            _mmf.Dispose();
            var viewLength = (long)safeBuffer.ByteLength;
            var viewLonger = viewLength - fileLength;
            var isClosed = safeBuffer.IsClosed;
            var isInvalid = safeBuffer.IsInvalid;
            _mutex?.Dispose();
            _semaphore?.Dispose();
        }

        /// <summary>
        /// 373 ms for 10 million
        /// Another day, 3.5 ms for 100,000, 37 ms for 1 million,
        /// </summary>
        [Benchmark]
        public long ReadRandomMemoryMappedUnsafeGeneric()
        {
            var value = 0L;
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                value = Unsafe.Read<long>(_basePointerInt64 + index);
            }
            return value;
        }

        [Benchmark]
        public long ReadRandomMemoryMappedUnsafeGenericLockerNull()
        {
            var value = 0L;
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                using (_lockerNoLock.Lock())
                {
                    //var value0 = _mmva.ReadInt64(index * 8);
                    //var value1 = *(_basePointerInt64 + index);
                    value = Unsafe.Read<long>(_basePointerInt64 + index);
                }
            }
            return value;
        }

        [Benchmark]
        public long ReadRandomMemoryMappedUnsafeGenericLockerLock()
        {
            var value = 0L;
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                using (_lockerLock.Lock())
                {
                    //var value0 = _mmva.ReadInt64(index * 8);
                    //var value1 = *(_basePointerInt64 + index);
                    value = Unsafe.Read<long>(_basePointerInt64 + index);
                }
            }
            return value;
        }

        [Benchmark]
        public long ReadRandomMemoryMappedUnsafeGenericLockerMutex()
        {
            var value = 0L;
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                using (_lockerMutex.Lock())
                {
                    //var value0 = _mmva.ReadInt64(index * 8);
                    //var value1 = *(_basePointerInt64 + index);
                    value = Unsafe.Read<long>(_basePointerInt64 + index);
                }
            }
            return value;
        }

        [Benchmark]
        public long ReadRandomMemoryMappedUnsafeGenericLockerSemaphore()
        {
            var value = 0L;
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                using (_lockerSemaphore.Lock())
                {
                    //var value0 = _mmva.ReadInt64(index * 8);
                    //var value1 = *(_basePointerInt64 + index);
                    value = Unsafe.Read<long>(_basePointerInt64 + index);
                }
            }
            return value;
        }

        /*
        |     1 million                                        Method |      Mean |    Error |   StdDev |
        |--------------------------------------------------- |----------:|---------:|---------:|
        |                ReadRandomMemoryMappedUnsafeGeneric |  37.48 ms | 0.742 ms | 1.597 ms |
        |      ReadRandomMemoryMappedUnsafeGenericLockerNull |  48.43 ms | 0.963 ms | 1.762 ms |
        |      ReadRandomMemoryMappedUnsafeGenericLockerLock |  78.80 ms | 1.558 ms | 2.645 ms |
        |     ReadRandomMemoryMappedUnsafeGenericLockerMutex | 838.21 ms | 4.931 ms | 4.371 ms |
        | ReadRandomMemoryMappedUnsafeGenericLockerSemaphore | 823.62 ms | 6.288 ms | 5.251 ms |
         */
    }
}
