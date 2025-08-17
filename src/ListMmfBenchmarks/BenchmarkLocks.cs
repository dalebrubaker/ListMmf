using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace ListMmfBenchmarks;

//[SimpleJob(RunStrategy.ColdStart, targetCount: 5)]
public unsafe class BenchmarkLocks
{
    private readonly object _lock = new();
    private long* _basePointerInt64;
    private BinaryReader _br;
    private FileStream _fs;
    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _mmva;
    private int[] _testIndexes;

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("Requires a 64-bit process (x64 or ARM64).");
        }
        const string TestFilePath = @"C:\_HugeArray\Timestamps.btd"; // 9.91 GB of longs
        const int NumTests = 10000000;
        _fs = new FileStream(TestFilePath, FileMode.Open);
        _br = new BinaryReader(_fs);
        var count = (int)(_fs.Length / 8);

        //_fs.Dispose();
        Console.WriteLine($"{count:N0} longs are in {TestFilePath}");
        var random = new Random(1);
        _testIndexes = new int[NumTests];
        for (var i = 0; i < NumTests; i++)
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
        //RuntimeHelpers.PrepareConstrainedRegions();
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
    ///     16.56 ms for 1 million vs 17.05 for unsafe pointer
    ///     154.9 ms for 10 million vs 157.1 for unsafe pointer
    /// </summary>
    [Benchmark]
    public long ReadRandomMemoryMappedUnsafeGeneric()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];

            //var value0 = _mmva.ReadInt64(index * 8);
            //var value1 = *(_basePointerInt64 + index);
            value = Unsafe.Read<long>(_basePointerInt64 + index);
        }
        return value;
    }

    /// <summary>
    ///     565 ms for 10 million
    /// </summary>
    [Benchmark]
    public long ReadRandomMemoryMappedUnsafeGenericWithLock()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
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
    ///     406 ms for 10 million
    /// </summary>
    [Benchmark]
    public long ReadRandomMemoryMappedUnsafeGenericWithMonitor()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
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
    ///     416 ms for 10 million
    /// </summary>
    [Benchmark]
    public long ReadRandomMemoryMappedUnsafeGenericWithMonitorActions()
    {
        var value = 0L;
        var actionEnter = new Action(() => Monitor.Enter(_lock));
        var actionExit = new Action(() => Monitor.Exit(_lock));
        for (var i = 0; i < _testIndexes.Length; i++)
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
    ///     174 ms for 10 million
    /// </summary>
    [Benchmark]
    public long ReadRandomMemoryMappedUnsafeGenericWithNullActions()
    {
        var value = 0L;
        Action actionEnter = null;
        Action actionExit = null;
        for (var i = 0; i < _testIndexes.Length; i++)
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
    ///     256 ms for 10 million vs 173 for null actions.
    ///     So Release optimization didn't make the NoOp() go away
    ///     Adding AggressiveInlining did not help.
    /// </summary>
    [Benchmark]
    public long ReadRandomMemoryMappedUnsafeGenericWithNoOpActions()
    {
        var value = 0L;
        var actionEnter = NoOp;
        var actionExit = NoOp;
        for (var i = 0; i < _testIndexes.Length; i++)
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
    ///     8795 ms for 10 million
    /// </summary>
    //[Benchmark]
    public long ReadRandomMemoryMappedUnsafeGenericWithNamedMutex()
    {
        var value = 0L;
        using (var mut = new Mutex(false, "Test"))
        {
            for (var i = 0; i < _testIndexes.Length; i++)
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
  Warm Start
  |         100 million                             Method |     Mean |    Error |   StdDev |
  |------------------------------------------------------ |---------:|---------:|---------:|
|                   ReadRandomMemoryMappedUnsafeGeneric | 3.613 s | 0.0695 s | 0.0800 s |
|           ReadRandomMemoryMappedUnsafeGenericWithLock | 5.393 s | 0.1055 s | 0.1129 s |
|        ReadRandomMemoryMappedUnsafeGenericWithMonitor | 5.308 s | 0.1049 s | 0.1030 s |
| ReadRandomMemoryMappedUnsafeGenericWithMonitorActions | 5.670 s | 0.0787 s | 0.0697 s |
|    ReadRandomMemoryMappedUnsafeGenericWithNullActions | 3.474 s | 0.0678 s | 0.0726 s |
|    ReadRandomMemoryMappedUnsafeGenericWithNoOpActions | 3.689 s | 0.0240 s | 0.0200 s |
  */

    /*
    Warm Start
    |         10 million                             Method |     Mean |    Error |   StdDev |
    |------------------------------------------------------ |---------:|---------:|---------:|
    |                   ReadRandomMemoryMappedUnsafeGeneric | 358.1 ms |  6.76 ms |  6.64 ms |
    |           ReadRandomMemoryMappedUnsafeGenericWithLock | 540.6 ms | 10.26 ms | 10.07 ms |
    |        ReadRandomMemoryMappedUnsafeGenericWithMonitor | 538.6 ms | 10.31 ms |  9.64 ms |
    | ReadRandomMemoryMappedUnsafeGenericWithMonitorActions | 553.9 ms | 10.29 ms |  9.63 ms |
    |    ReadRandomMemoryMappedUnsafeGenericWithNullActions | 353.8 ms |  4.36 ms |  4.08 ms |
    |    ReadRandomMemoryMappedUnsafeGenericWithNoOpActions | 372.2 ms |  6.86 ms |  6.41 ms |

    */
    /*
    Cold Start
    |         10 million                             Method |     Mean |    Error |   StdDev |
    |------------------------------------------------------ |---------:|---------:|---------:|
|                   ReadRandomMemoryMappedUnsafeGeneric | 4.727 s | 37.315 s | 9.691 s | 0.3830 s |
|           ReadRandomMemoryMappedUnsafeGenericWithLock | 1.749 s | 10.401 s | 2.701 s | 0.5376 s |
|        ReadRandomMemoryMappedUnsafeGenericWithMonitor | 1.663 s |  9.694 s | 2.518 s | 0.5404 s |
| ReadRandomMemoryMappedUnsafeGenericWithMonitorActions | 1.768 s | 10.367 s | 2.692 s | 0.5759 s |
|    ReadRandomMemoryMappedUnsafeGenericWithNullActions | 1.645 s | 11.079 s | 2.877 s | 0.3579 s |
|    ReadRandomMemoryMappedUnsafeGenericWithNoOpActions | 1.728 s | 11.541 s | 2.997 s | 0.3893 s |
    */
}