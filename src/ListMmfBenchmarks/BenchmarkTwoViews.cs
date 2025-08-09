using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace ListMmfBenchmarks;

public unsafe class BenchmarkTwoViews
{
    private readonly object _lock = new();
    private long* _basePointerHeaderInt64;
    private long* _basePointerMainInt64;
    private BinaryReader _br;
    private FileStream _fs;
    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _mmvaHeader;
    private MemoryMappedViewAccessor _mmvaMain;
    private int[] _testIndexes;

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new Exception("Not supported on 32-bit process. Must be 64-bit for atomic operations on structures of size <= 8 bytes.");
        }
        const string testFilePath = @"C:\_HugeArray\Timestamps.btd"; // 9.91 GB of longs
        const int numTests = 10000000;
        _fs = new FileStream(testFilePath, FileMode.Open);
        _br = new BinaryReader(_fs);
        var count = (int)(_fs.Length / 8);

        //_fs.Dispose();
        Console.WriteLine($"{count:N0} longs are in {testFilePath}");
        var random = new Random(1);
        _testIndexes = new int[numTests];
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = random.Next(0, count);
            _testIndexes[i] = index;
        }
        _mmf = MemoryMappedFile.CreateFromFile(_fs, null, _fs.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);

        //_mmf = MemoryMappedFile.CreateFromFile(testFilePath, FileMode.Open,null, 0, MemoryMappedFileAccess.Read);
        //_mmva = _mmf.CreateViewAccessor(0, count * 8, MemoryMappedFileAccess.Read);
        //_mmva = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        // If I open with 0 size, I get IOException, not enough memory with 32 bit process but no problem 64 bit
        _mmvaMain = _mmf.CreateViewAccessor(); // 0 offset, 0 size (all file), ReadWrite
        _mmvaHeader = _mmf.CreateViewAccessor(0, 8, MemoryMappedFileAccess.ReadWrite);

        var safeBuffer = _mmvaMain.SafeMemoryMappedViewHandle;
        byte* basePointerByte = null;
        //RuntimeHelpers.PrepareConstrainedRegions();
        safeBuffer.AcquirePointer(ref basePointerByte);
        basePointerByte += _mmvaMain.PointerOffset; // adjust for the extraMemNeeded
        _basePointerMainInt64 = (long*)basePointerByte;
        _basePointerHeaderInt64 = (long*)basePointerByte;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _fs.Dispose();
        _mmvaMain.Dispose();
        _mmvaHeader.Dispose();
        _mmf.Dispose();
    }

    /// <summary>
    ///     150.9 ms for 10 million. No difference to have a second small view for a header
    /// </summary>
    [Benchmark]
    public (long value, long length) ReadRandomMemoryMappedUnsafeGenericOneView()
    {
        long value = 0, length = 0;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];
            value = Unsafe.Read<long>(_basePointerMainInt64 + index);
            length = Unsafe.Read<long>(_basePointerMainInt64);
        }
        return (value, length);
    }

    /// <summary>
    ///     151.3 ms for 10 million. No difference to have a second small view for a header
    /// </summary>
    [Benchmark]
    public (long value, long length) ReadRandomMemoryMappedUnsafeGenericTwoViews()
    {
        long value = 0, length = 0;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];
            value = Unsafe.Read<long>(_basePointerMainInt64 + index);
            length = Unsafe.Read<long>(_basePointerHeaderInt64);
        }
        return (value, length);
    }

    /*
    |                                      Method |     Mean |   Error |  StdDev |
    |-------------------------------------------- |---------:|--------:|--------:|
    |  ReadRandomMemoryMappedUnsafeGenericOneView | 360.9 ms | 3.99 ms | 3.73 ms |
    | ReadRandomMemoryMappedUnsafeGenericTwoViews | 362.0 ms | 3.44 ms | 3.22 ms |
     */
}