using System.IO;
using System.IO.MemoryMappedFiles;
using FluentAssertions;
using Xunit;

// ReSharper disable InconsistentNaming

// ReSharper disable RedundantAssignment

namespace ListMmfTests;

public class MmfTests
{
    [Fact]
    public void WriteToAllSafeBufferBytes_File()
    {
        var fileName = $"{nameof(WriteToAllSafeBufferBytes_File)}";
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }
        const int capacity = 1000;
        const int value = 2;
        using (var fs = new FileStream(fileName, FileMode.CreateNew))
        {
            using (var mmf = MemoryMappedFile.CreateFromFile(fs, null, capacity, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
            {
                var fileLength = fs.Length;
                using (var mmva = mmf.CreateViewAccessor())
                {
                    var viewLength = mmva.SafeMemoryMappedViewHandle.ByteLength;
                    // Windows: page-aligned (4096), iOS: exact capacity (1000)
                    (viewLength == 4096 || viewLength == capacity).Should().BeTrue("View should be either page-aligned or exact capacity");

                    // Only test writing to end of buffer if we have page alignment
                    if (viewLength >= 4096)
                    {
                        mmva.Write(4092, value);
                    }
                    else
                    {
                        mmva.Write(capacity - 4, value);
                    }
                    fs.Length.Should().Be(capacity, "File doesn't expand, only view size is rounded up.");
                }
                using (var mmf2 = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
                {
                    using (var mmva2 = mmf2.CreateViewAccessor())
                    {
                        var viewLength2 = mmva2.SafeMemoryMappedViewHandle.ByteLength;
                        int readOffset = viewLength2 >= 4096 ? 4092 : capacity - 4;
                        var value2 = mmva2.ReadInt32(readOffset);
                        value2.Should().Be(value, "Can read to the end of the buffer.");
                    }
                }
            }
            fs.Length.Should().Be(capacity, "Larger view doesn't mean larger file.");
        }
        using (var fs = new FileStream(fileName, FileMode.Open))
        {
            fs.Length.Should().Be(capacity, "Larger view doesn't mean larger file.");
            using (var mmf = MemoryMappedFile.CreateFromFile(fs, null, capacity, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
            {
                var fileLength = fs.Length;
                using (var mmva = mmf.CreateViewAccessor())
                {
                    var viewLength = mmva.SafeMemoryMappedViewHandle.ByteLength;
                    (viewLength == 4096 || viewLength == capacity).Should().BeTrue("View should be either page-aligned or exact capacity");

                    int readOffset = viewLength >= 4096 ? 4092 : capacity - 4;
                    var value2 = mmva.ReadInt32(readOffset);
                    value2.Should().Be(value, "Can read to the end of the buffer.");
                }
            }
        }
        File.Delete(fileName);
    }

    // Removed CreateFromFile_ReadWriteThenReadWrite_ShouldThrow test since it tests named MMFs which aren't supported on iOS
    // and ListMmf never uses named maps anyway (always passes null for mapName)
}