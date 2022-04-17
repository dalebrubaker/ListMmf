using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

// ReSharper disable InconsistentNaming

// ReSharper disable RedundantAssignment

namespace ListMmfTests
{
    public class MmfTests
    {
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
                    using (var mmf2 = MemoryMappedFile.CreateFromFile(fs, null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true))
                    {
                        using (var mmva2 = mmf2.CreateViewAccessor())
                        {
                            var value2 = mmva2.ReadInt32(4092);
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
                        viewLength.Should().Be(4096, "The view goes to the end of a page");
                        var value2 = mmva.ReadInt32(4092);
                        value2.Should().Be(value, "Can read to the end of the buffer, PAST THE END OF THE FILE.");
                    }
                }
            }
            File.Delete(fileName);
        }

        [Fact]
        public void CreateFromFile_ReadWriteThenReadWrite_ShouldThrow()
        {
            var path = nameof(CreateFromFile_ReadWriteThenReadWrite_ShouldThrow);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
#pragma warning disable 8600
            string mapName = null; //"MyMapName";
#pragma warning restore 8600
            using (var fileStreamRW = UtilsListMmf.CreateFileStreamFromPath(path, MemoryMappedFileAccess.ReadWrite))
            {
                using var mmfRW = MemoryMappedFile.CreateFromFile(fileStreamRW, mapName, 4096, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
                mmfRW.Should().NotBeNull();
                using var fileStreamR = UtilsListMmf.CreateFileStreamFromPath(path, MemoryMappedFileAccess.Read);
                using var mmfR = MemoryMappedFile.CreateFromFile(fileStreamR, mapName, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                mmfR.Should().NotBeNull();
            }
            File.Delete(path);
            mapName = "MyMapName";
            using (var fileStreamRW = UtilsListMmf.CreateFileStreamFromPath(path, MemoryMappedFileAccess.ReadWrite))
            {
                using var mmfRW = MemoryMappedFile.CreateFromFile(fileStreamRW, mapName, 4096, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
                mmfRW.Should().NotBeNull();
                using var fileStreamR = UtilsListMmf.CreateFileStreamFromPath(path, MemoryMappedFileAccess.Read);
                Action act = () =>
                {
                    var mmfR = MemoryMappedFile.CreateFromFile(fileStreamR, mapName, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                };
                act.Should().Throw<IOException>("Because mapName is not null");

                //     using var mmfR = MemoryMappedFile.CreateFromFile(fileStreamR, mapName, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
                //     mmfR.Should().NotBeNull();
            }
            File.Delete(path);
        }

        [Fact]
        public void CreateFromFile_ReadWriteThenReadThenReadWrite()
        {
            var path = nameof(CreateFromFile_ReadWriteThenReadThenReadWrite);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            var mapName = "MyMapName";
            const int capacity = 4096;
            const int testOffset = 4092;
            const int testValue = 49;
            using (var mmfRW = MemoryMappedFile.CreateFromFile(path, FileMode.Create, mapName, capacity, MemoryMappedFileAccess.ReadWrite))
            {
                mmfRW.Should().NotBeNull();
                using (var mmvaRW = mmfRW.CreateViewAccessor())
                {
                    var viewLengthRW = mmvaRW.SafeMemoryMappedViewHandle.ByteLength;
                    viewLengthRW.Should().Be(capacity, "The view goes to the end of a page");
                    mmvaRW.Write(testOffset, testValue);
                    using (var mmfR = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read))
                    {
                        mmfR.Should().NotBeNull();

                        // Note we CANNOT use the default accessor on the next line. Need 0, 0 
                        using (var mmvaR = mmfR.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
                        {
                            var viewLengthR = mmvaR.SafeMemoryMappedViewHandle.ByteLength;
                            viewLengthR.Should().Be(capacity, "The view goes to the end of a page");
                            var value = mmvaR.ReadInt16(testOffset);
                            value.Should().Be(testValue);

                            mmvaRW.Dispose();
                            mmfRW.Dispose();
                            var value2 = mmvaR.ReadInt16(testOffset);
                            value2.Should().Be(testValue, "We can still read from the Reader after the Writer is closed");

                            // Now while the reader is still open, try to re-open writer on path
                            Action action = () => MemoryMappedFile.CreateFromFile(path, FileMode.Open, mapName, 0, MemoryMappedFileAccess.Read);
                            action.Should().Throw<IOException>();

                            // using (var mmfRW2 = MemoryMappedFile.CreateFromFile(path, FileMode.Open, mapName, 0, MemoryMappedFileAccess.Read))
                            // {
                            //     mmfRW2.Should().NotBeNull();
                            //     using (var mmvaRW2 = mmfRW2.CreateViewAccessor())
                            //     {
                            //         var value3 = mmvaRW2.ReadInt16(testOffset);
                            //         value3.Should().Be(testValue, "We can still read from the re-opened  Writer");
                            //     }
                            // }
                        }
                    }
                }
            }

            File.Delete(path);
            // using (var fileStreamRW = ListMmfBaseBase.CreateFileStreamFromPath(path, MemoryMappedFileAccess.ReadWrite))
            // {
            //     using var mmfRW = MemoryMappedFile.CreateFromFile(fileStreamRW, mapName, 4096, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, true);
            //     mmfRW.Should().NotBeNull();
            //     using var fileStreamR = ListMmfBaseBase.CreateFileStreamFromPath(path, MemoryMappedFileAccess.Read);
            //     Action act = () =>
            //     {
            //         var mmfR = MemoryMappedFile.CreateFromFile(fileStreamR, mapName, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
            //     };
            //     act.Should().Throw<IOException>("Because mapName is not null");
            //
            //     //     using var mmfR = MemoryMappedFile.CreateFromFile(fileStreamR, mapName, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
            //     //     mmfR.Should().NotBeNull();
            // }
            File.Delete(path);
        }
    }
}