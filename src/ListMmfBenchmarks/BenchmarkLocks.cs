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

        
        /// <summary>
        /// 565 ms for 10 million 
        /// </summary>
        [Benchmark]
        public long ReadRandomMemoryMappedUnsafeGenericWithLock()
        {
            var value = 0L;
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                lock (_lock)
                {
                    value = Unsafe.Read<long>(_basePointerInt64 + index);
                }
            }
            return value;
        }

        /// <summary>
        /// 406 ms for 10 million 
        /// </summary>
        [Benchmark]
        public long ReadRandomMemoryMappedUnsafeGenericWithMonitor()
        {
            var value = 0L;
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                Monitor.Enter(_lock);
                value = Unsafe.Read<long>(_basePointerInt64 + index);
                Monitor.Exit(_lock);
            }
            return value;
        }

        /// <summary>
        /// 416 ms for 10 million 
        /// </summary>
        [Benchmark]
        public long ReadRandomMemoryMappedUnsafeGenericWithMonitorActions()
        {
            var value = 0L;
            var actionEnter = new Action(() => Monitor.Enter(_lock));
            var actionExit = new Action(() => Monitor.Exit(_lock));
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                actionEnter?.Invoke();
                value = Unsafe.Read<long>(_basePointerInt64 + index);
                actionExit?.Invoke();
            }
            return value;
        }

        /// <summary>
        /// 174 ms for 10 million 
        /// </summary>
        [Benchmark]
        public long ReadRandomMemoryMappedUnsafeGenericWithNullActions()
        {
            var value = 0L;
            Action actionEnter = null;
            Action actionExit = null;
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                actionEnter?.Invoke();
                value = Unsafe.Read<long>(_basePointerInt64 + index);
                actionExit?.Invoke();
            }
            return value;
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
        public long ReadRandomMemoryMappedUnsafeGenericWithNoOpActions()
        {
            var value = 0L;
            Action actionEnter = new Action(NoOp);
            Action actionExit = new Action(NoOp);
            for (int i = 0; i < _testIndexes.Length; i++)
            {
                var index = _testIndexes[i];

                //var value0 = _mmva.ReadInt64(index * 8);
                //var value1 = *(_basePointerInt64 + index);
                actionEnter();
                value = Unsafe.Read<long>(_basePointerInt64 + index);
                actionExit();
            }
            return value;
        }

        /// <summary>
        /// 8795 ms for 10 million 
        /// </summary>
        //[Benchmark]
        public long ReadRandomMemoryMappedUnsafeGenericWithNamedMutex()
        {
            var value = 0L;
            using (var mut = new Mutex(false, "Test"))
            {
                for (int i = 0; i < _testIndexes.Length; i++)
                {
                    var index = _testIndexes[i];
                    mut.WaitOne();
                    //var value0 = _mmva.ReadInt64(index * 8);
                    //var value1 = *(_basePointerInt64 + index);
                    value = Unsafe.Read<long>(_basePointerInt64 + index);
                    mut.ReleaseMutex();
                }
            }
            return value;
        }

        /*

        |                                                Method |     Mean |    Error |   StdDev |
        |------------------------------------------------------ |---------:|---------:|---------:|
        |                   ReadRandomMemoryMappedUnsafeGeneric | 358.1 ms |  6.76 ms |  6.64 ms |
        |           ReadRandomMemoryMappedUnsafeGenericWithLock | 540.6 ms | 10.26 ms | 10.07 ms |
        |        ReadRandomMemoryMappedUnsafeGenericWithMonitor | 538.6 ms | 10.31 ms |  9.64 ms |
        | ReadRandomMemoryMappedUnsafeGenericWithMonitorActions | 553.9 ms | 10.29 ms |  9.63 ms |
        |    ReadRandomMemoryMappedUnsafeGenericWithNullActions | 353.8 ms |  4.36 ms |  4.08 ms |
        |    ReadRandomMemoryMappedUnsafeGenericWithNoOpActions | 372.2 ms |  6.86 ms |  6.41 ms |

        */
    }
}
