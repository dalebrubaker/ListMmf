using System.IO;
using System.IO.MemoryMappedFiles;
using FluentAssertions;
using Xunit;

namespace ListMmfTests
{
    public class MmfTests
    {
        [Fact]
        public void WriteToAllSafeBufferBytes_Memory()
        {
            const string mapName = "TestMapName";
            const int capacity = 1000;
            using var mmf = MemoryMappedFile.CreateNew(mapName, capacity);
            var value = 2;
            using (var mmva1 = mmf.CreateViewAccessor())
            {
                var viewLength = mmva1.SafeMemoryMappedViewHandle.ByteLength;
                viewLength.Should().Be(4096, "The view goes to the end of a page");
                mmva1.Write(4092, value);
            }
            using var mmf2 = MemoryMappedFile.OpenExisting(mapName);
            using var mmva2 = mmf2.CreateViewAccessor();
            var value2 = mmva2.ReadInt32(4092);
            value2.Should().Be(value, "Can read to the end of the buffer.");
        }

        [Fact]
        public void WriteToAllSafeBufferBytes_File()
        {
            var fileName = $"{nameof(WriteToAllSafeBufferBytes_File)}";
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
                        viewLength.Should().Be(4096, "The view goes to the end of a page");
                        mmva.Write(4092, value);
                        fs.Length.Should().Be(capacity, "File doesn't expand, only view size is rounded up.");
                    }
                    using var mmf2 = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
                    using var mmva2 = mmf2.CreateViewAccessor();
                    var value2 = mmva2.ReadInt32(4092);
                    value2.Should().Be(value, "Can read to the end of the buffer.");
                }
                fs.Length.Should().Be(capacity, "Larger view doesn't mean larger file.");
            }
            using (var fs = new FileStream(fileName, FileMode.Open))
            {
                fs.Length.Should().Be(capacity, "Larger view doesn't mean larger file.");
                using var mmf = MemoryMappedFile.CreateFromFile(fs, null, capacity, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
                var fileLength = fs.Length;
                using var mmva = mmf.CreateViewAccessor();
                var viewLength = mmva.SafeMemoryMappedViewHandle.ByteLength;
                viewLength.Should().Be(4096, "The view goes to the end of a page");
                var value2 = mmva.ReadInt32(4092);
                value2.Should().Be(value, "Can read to the end of the buffer, PAST THE END OF THE FILE.");
            }
            File.Delete(fileName);
        }
    }
}
