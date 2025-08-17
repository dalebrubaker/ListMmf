using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace ListMmfBenchmarks;

public unsafe class DebugAppend
{
    private long* _basePointerInt64;
    private FileStream _fs;
    private MemoryMappedFile _mmf;
    private MemoryMappedViewAccessor _mmva;
    private string _testFilePath;

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("Requires a 64-bit process (x64 or ARM64).");
        }
        _testFilePath = @"C:\_HugeArray\TestApppend.dat";
        CreateMmf(1000);
    }

    private void CreateMmf(long numItems)
    {
        _mmva?.Dispose();
        _mmf?.Dispose();
        _fs?.Dispose();
        _fs = new FileStream(_testFilePath, FileMode.Create);
        _mmf = MemoryMappedFile.CreateFromFile(_fs, null, numItems * 8, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);

        //_mmf = MemoryMappedFile.CreateFromFile(testFilePath, FileMode.Open,null, 0, MemoryMappedFileAccess.Read);
        //_mmva = _mmf.CreateViewAccessor(0, count * 8, MemoryMappedFileAccess.Read);
        //_mmva = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        // If I open with 0 size, I get IOException, not enough memory with 32 bit process but no problem 64 bit
        var length = _fs.Length;
        _mmva = _mmf.CreateViewAccessor(); // 0 offset, 0 size (all file), ReadWrite
        _basePointerInt64 = (long*)GetPointer(_mmva);
    }

    private byte* GetPointer(MemoryMappedViewAccessor mmva)
    {
        var safeBuffer = mmva.SafeMemoryMappedViewHandle;
        //RuntimeHelpers.PrepareConstrainedRegions();
        byte* pointer = null;
        try
        {
            safeBuffer.AcquirePointer(ref pointer);
        }
        finally
        {
            if (pointer != null)
            {
                safeBuffer.ReleasePointer();
            }
        }
        pointer += mmva.PointerOffset;
        return pointer;
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _mmva.Dispose();
        _mmf.Dispose();
        _fs.Dispose();
    }

    /// <summary>
    ///     Read: 154.9 ms for 10 million vs 157.1 for unsafe pointer
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

        CreateMmf(index + 2); // Need to reset Mmf and View to increase file size
        var length2 = _fs.Length;
        var index2 = length2 / 8 - 1; // this is index of longs, not byte
        Unsafe.Write(_basePointerInt64 + index2, index2);
        var value2 = Unsafe.Read<long>(_basePointerInt64 + index2);
    }
}