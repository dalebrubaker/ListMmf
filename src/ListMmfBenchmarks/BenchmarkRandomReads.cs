using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace ListMmfBenchmarks;

public unsafe class BenchmarkRandomReads
{
    private long* _basePointerInt64;
    private BinaryReader _br;
    private FileStream _fs;
    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _mmva;
    private int[] _testIndexes;

    [Params(1000000, 10000000)]
    private int NumTests { get; } = 1000000;

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("Requires a 64-bit process (x64 or ARM64).");
        }
        const string testFilePath = @"C:\_HugeArray\Timestamps.btd"; // 9.91 GB of longs
        _fs = new FileStream(testFilePath, FileMode.Open);
        _br = new BinaryReader(_fs);
        var count = (int)(_fs.Length / 8);

        //_fs.Dispose();
        Console.WriteLine($"{count:N0} longs are in {testFilePath}");
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
    ///     10.93 ms
    /// </summary>
    //Benchmark]
    public void Sleep10()
    {
        Thread.Sleep(10);
    }

    /// <summary>
    ///     687 ms for 100000 random accesses, 6.87 for 1 million
    /// </summary>
    [Benchmark]
    public long ReadRandomFileStream()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];
            _fs.Seek(index * 8, SeekOrigin.Begin);
            value = _br.ReadInt64();
        }
        return value;
    }

    /// <summary>
    ///     14.64 ms for 100000 random accesses, 149 ms for 1 million
    /// </summary>
    //[Benchmark]
    public long ReadRandomMemoryMapped()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];
            value = _mmva.ReadInt64(index * 8);
        }
        return value;
    }

    /// <summary>
    ///     16 ms for 1 million, 157.1  ms for 10 million
    /// </summary>
    //[Benchmark]
    public long ReadRandomMemoryMappedUnsafePointer()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];

            //var value0 = _mmva.ReadInt64(index * 8);
            value = *(_basePointerInt64 + index);
        }
        return value;
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
    ///     150 ms for 1 million vs 16 for ReadRandomMemoryMappedUnsafeGeneric
    ///     1554 ms for 10 million vs 162 for ReadRandomMemoryMappedUnsafeGeneric
    /// </summary>
    //[Benchmark]
    public long ReadRandomMemoryMappedUnsafeGenericReAcquirePointer()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];
            byte* pointer = null;
            //RuntimeHelpers.PrepareConstrainedRegions();
            try
            {
                _mmva.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);

                // Use pointer here, with your own bounds checking.  
            }
            finally
            {
                if (pointer != null)
                {
                    _mmva.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
            _basePointerInt64 = (long*)pointer;

            //var value0 = _mmva.ReadInt64(index * 8);
            //var value1 = *(_basePointerInt64 + index);
            value = Unsafe.Read<long>(_basePointerInt64 + index);
        }
        return value;
    }

    /*
    |                                              Method |     Mean |   Error |  StdDev |
    |---------------------------------------------------- |---------:|--------:|--------:|
    |                                ReadRandomFileStream |       NA |      NA |      NA |
    |                              ReadRandomMemoryMapped |       NA |      NA |      NA |
    |                 ReadRandomMemoryMappedUnsafePointer | 367.7 ms | 7.22 ms | 7.41 ms |
    |                 ReadRandomMemoryMappedUnsafeGeneric | 371.5 ms | 6.43 ms | 6.01 ms |
    | ReadRandomMemoryMappedUnsafeGenericReAcquirePointer | 786.2 ms | 9.84 ms | 8.72 ms |
     */
}