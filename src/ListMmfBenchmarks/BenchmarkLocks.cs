using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace ListMmfBenchmarks
{
    public unsafe class BenchmarkLocks
    {
        private FileStream _fs;
        private BinaryReader _br;
        private int[] _testIndexes;
        private MemoryMappedFile _mmf;
        private MemoryMappedViewAccessor _mmva;
        private long* _basePointerInt64;
        private readonly object _lock = new object();

        [GlobalSetup]
        public void GlobalSetup()
        {
            if (!Environment.Is64BitOperatingSystem)
                throw new Exception("Not supported on 32-bit operating system. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            if (!Environment.Is64BitProcess) throw new Exception("Not supported on 32-bit process. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
            const string testFilePath = @"D:\_HugeArray\Timestamps.btd"; // 9.91 GB of longs
            const int numTests = 10000000;
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
            _mmf = MemoryMappedFile.CreateFromFile(_fs, null, _fs.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);

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

            var fileLength = _fs.Length;
            var viewLength = (long)safeBuffer.ByteLength;
            var viewLonger = viewLength - fileLength;
            var capacity = _mmva.Capacity; // same as viewLength
            var isClosed = safeBuffer.IsClosed;
            var isInvalid = safeBuffer.IsInvalid;
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
        }

        /// <summary>
        /// 16.56 ms for 1 million vs 17.05 for unsafe pointer
        /// 154.9 ms for 10 million vs 157.1 for unsafe pointer
        /// </summary>
        [Benchmark]
        public void ReadRandomMemoryMappedUnsafeGeneric()
        {
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                var value = Unsafe.Read<long>(_basePointerInt64 + index);
                if (value < 1)

                    // To avoid optimizing away the read
                    break;
            }
        }

        
        /// <summary>
        /// 565 ms for 10 million 
        /// </summary>
        [Benchmark]
        public void ReadRandomMemoryMappedUnsafeGenericWithLock()
        {
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                lock (_lock)
                {
                    var value = Unsafe.Read<long>(_basePointerInt64 + index);
                    if (value < 1)

                        // To avoid optimizing away the read
                        break;

                }
            }
        }

        /// <summary>
        /// 406 ms for 10 million 
        /// </summary>
        [Benchmark]
        public void ReadRandomMemoryMappedUnsafeGenericWithMonitor()
        {
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                Monitor.Enter(_lock);
                var value = Unsafe.Read<long>(_basePointerInt64 + index);
                if (value < 1)
                {

                    // To avoid optimizing away the read
                    break;
                }
                Monitor.Exit(_lock);
            }
        }

        /// <summary>
        /// 416 ms for 10 million 
        /// </summary>
        [Benchmark]
        public void ReadRandomMemoryMappedUnsafeGenericWithMonitorActions()
        {
            var actionEnter = new Action(() => Monitor.Enter(_lock));
            var actionExit = new Action(() => Monitor.Exit(_lock));
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                actionEnter?.Invoke();
                var value = Unsafe.Read<long>(_basePointerInt64 + index);
                if (value < 1)
                {

                    // To avoid optimizing away the read
                    break;
                }
                actionExit?.Invoke();
            }
        }

        /// <summary>
        /// 174 ms for 10 million 
        /// </summary>
        [Benchmark]
        public void ReadRandomMemoryMappedUnsafeGenericWithNullActions()
        {
            Action actionEnter = null;
            Action actionExit = null;
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                actionEnter?.Invoke();
                var value = Unsafe.Read<long>(_basePointerInt64 + index);
                if (value < 1)
                {

                    // To avoid optimizing away the read
                    break;
                }
                actionExit?.Invoke();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)] // this attribute doesn't seem to make any difference
        private void NoOp()
        {
        }

        /// <summary>
        /// 256 ms for 10 million vs 173 for null actions.
        /// So Release optimization didn't make the NoOp() go away
        /// Adding AggressiveInlining did not help.
        /// </summary>
        [Benchmark]
        public void ReadRandomMemoryMappedUnsafeGenericWithNoOpActions()
        {
            Action actionEnter = new Action(NoOp);
            Action actionExit = new Action(NoOp);
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                actionEnter();
                var value = Unsafe.Read<long>(_basePointerInt64 + index);
                if (value < 1)
                {

                    // To avoid optimizing away the read
                    break;
                }
                actionExit();
            }
        }

        /// <summary>
        /// 8795 ms for 10 million 
        /// </summary>
        //[Benchmark]
        public void ReadRandomMemoryMappedUnsafeGenericWithNamedMutex()
        {
            using (var mut = new Mutex(false, "Test"))
            {
                for (int i = 0; i < _testIndexes.Length; i++)
                {
                    var index = _testIndexes[i];
                    mut.WaitOne();
                    //var value0 = _mmva.ReadInt64(index * 8);
                    //var value1 = *(_basePointerInt64 + index);
                    var value = Unsafe.Read<long>(_basePointerInt64 + index);
                    if (value < 1)

                        // To avoid optimizing away the read
                        break;
                    mut.ReleaseMutex();
                }
            }
        }

        /*

        |                                                Method |         Mean |      Error |     StdDev |
        |------------------------------------------------------ |-------------:|-----------:|-----------:|
        |                   ReadRandomMemoryMappedUnsafeGeneric |     6.356 ns |  0.0512 ns |  0.0454 ns |
        |           ReadRandomMemoryMappedUnsafeGenericWithLock |   108.659 ns |  2.1385 ns |  2.7807 ns |
        |        ReadRandomMemoryMappedUnsafeGenericWithMonitor |    61.280 ns |  0.1394 ns |  0.1164 ns |
        | ReadRandomMemoryMappedUnsafeGenericWithMonitorActions |    98.273 ns |  1.9988 ns |  3.8985 ns |
        |    ReadRandomMemoryMappedUnsafeGenericWithNullActions |     8.473 ns |  0.0252 ns |  0.0235 ns |
        |    ReadRandomMemoryMappedUnsafeGenericWithNoOpActions |    46.969 ns |  0.3339 ns |  0.2960 ns |
        |     ReadRandomMemoryMappedUnsafeGenericWithNamedMutex | 9,665.964 ns | 26.8542 ns | 22.4245 ns |
        */
    }
}