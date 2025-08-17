using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace ListMmfBenchmarks;

public unsafe class BenchmarkRandomWrites
{
    private long* _basePointerInt64;
    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _mmva;
    private int[] _testIndexes;

    [Params(10000000)]
    private int NumTests { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("Requires a 64-bit process (x64 or ARM64).");
        }
        const string testFilePath = @"C:\_HugeArray\Timestamps.btd"; // 9.91 GB of longs
        NumTests = 10000000;
        var fs = new FileStream(testFilePath, FileMode.Open);
        var count = (int)(fs.Length / 8);

        //_fs.Dispose();
        Console.WriteLine($"{count:N0} longs are in {testFilePath}");
        var random = new Random(1);
        _testIndexes = new int[NumTests];
        for (var i = 0; i < _testIndexes.Length; i++)
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
        //RuntimeHelpers.PrepareConstrainedRegions();
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
    ///     Read: 154.9 ms for 10 million
    ///     Read then Write: 187.9 ms for 10 million
    /// </summary>
    [Benchmark]
    public long ReadWriteRandomMemoryMappedUnsafeGeneric()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];
            value = Unsafe.Read<long>(_basePointerInt64 + index);
            Unsafe.Write(_basePointerInt64 + index, value);

            //var valueCheck = Unsafe.Read<long>(_basePointerInt64 + index);
        }
        return value;
    }

    /*
    |                                   Method |     Mean |   Error |  StdDev |
    |----------------------------------------- |---------:|--------:|--------:|
    | ReadWriteRandomMemoryMappedUnsafeGeneric | 394.3 ms | 5.35 ms | 5.00 ms |
     */
}