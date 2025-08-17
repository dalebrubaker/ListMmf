using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;

namespace ListMmfBenchmarks;

internal unsafe class TestPointerHugeFile
{
    public void WriteRead()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("Requires a 64-bit process (x64 or ARM64).");
        }
        const string TestFilePath = @"C:\_HugeArray\TestWriteRead.btd"; // 9.91 GB of longs
        var count = (long)int.MaxValue * 2;

        var mmf = MemoryMappedFile.CreateFromFile(TestFilePath, FileMode.Create, null, count * 8, MemoryMappedFileAccess.ReadWrite);

        //_mmf = MemoryMappedFile.CreateFromFile(testFilePath, FileMode.Open,null, 0, MemoryMappedFileAccess.Read);
        //_mmva = _mmf.CreateViewAccessor(0, count * 8, MemoryMappedFileAccess.Read);
        //_mmva = _mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        // If I open with 0 size, I get IOException, not enough memory with 32 bit process but no problem 64 bit
        var mmva = mmf.CreateViewAccessor(); // 0 offset, 0 size (all file), ReadWrite
        var size = mmva.SafeMemoryMappedViewHandle.ByteLength;
        var basePointerByte = GetPointer(mmva);
        var basePointerMainInt64 = (long*)basePointerByte;
        for (long i = 0; i < count; i++)
        {
            Unsafe.Write(basePointerMainInt64 + i, i);
        }
        for (long i = 0; i < count; i++)
        {
            var value = Unsafe.Read<long>(basePointerMainInt64 + i);
            Debug.Assert(value == i);
            if (i % 1000000 == 0)
            {
                var checkPointerByte = GetPointer(mmva);
                Debug.Assert(checkPointerByte == basePointerByte);
            }
        }
        var checkPointerByte2 = GetPointer(mmva);
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
}