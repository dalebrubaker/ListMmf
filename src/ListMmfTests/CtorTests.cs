using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;
// ReSharper disable ConvertToUsingDeclaration

namespace ListMmfTests
{
    public class CtorTests
    {
        [Fact]
        public void CreateNew_SetsCapacity()
        {
            var mapName = $"{nameof(CreateNew_SetsCapacity)}";
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateNew(mapName, capacityElements))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            }
        }

        [Fact]
        public void CreateFile_SetsCapacity()
        {
            var fileName = $"{nameof(CreateFile_SetsCapacity)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateFromFile(fileName, capacityElements: capacityElements))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            }
            File.Delete(fileName);
        }

        [Fact]
        public void CapacitySet_ShouldGrowCapacityFile()
        {
            var fileName = $"{nameof(CapacitySet_ShouldGrowCapacityFile)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateFromFile(fileName, capacityElements: capacityElements))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Capacity = 511 + 512; // add another page
                listMmf.Capacity.Should().Be(511 + 512);
            }
            File.Delete(fileName);
        }

        [Fact]
        public void Add_ShouldGrowCount()
        {
            var mapName = $"{nameof(Add_ShouldGrowCount)}";
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateNew(mapName, capacityElements))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Count.Should().Be(0);
                const int testValue = 1;
                listMmf.Add(testValue);
                listMmf.Count.Should().Be(1);
                var read = listMmf[0];
                read.Should().Be(testValue);
            }
        }

        [Fact]
        public void This_Test()
        {
            var mapName = $"{nameof(This_Test)}";
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateNew(mapName, capacityElements))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Count.Should().Be(0);
                for (int i = 0; i < 3; i++)
                {
                    listMmf.Add(i);
                    listMmf[i].Should().Be(i);
                }
                listMmf.Count.Should().Be(3);
                listMmf[2] = 89;
                listMmf[2].Should().Be(89);
            }
        }

        [Fact]
        public void TrimExcess_ShouldTrimFile()
        {
            var fileName = $"{nameof(TrimExcess_ShouldTrimFile)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            const int capacityElements = 511 + 512;
            using (var listMmf = ListMmf<long>.CreateFromFile(fileName, capacityElements: capacityElements))
            {
                listMmf.Capacity.Should().Be(511 + 512, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Count.Should().Be(0);
                for (int i = 0; i < listMmf.Capacity; i++)
                {
                    listMmf.Add(i);
                }
                var capacity = listMmf.Capacity;
                listMmf.Count.Should().Be(capacity);
                listMmf.Capacity += 512;
                listMmf.Count.Should().Be(capacity, "Increased capacity should not have changed Count");
                listMmf.Capacity.Should().Be(capacity + 512);
                listMmf.TrimExcess();
                listMmf.Capacity.Should().Be(capacity, "Should have removed capacity beyond Count");
            }
            File.Delete(fileName);
        }

        [Fact]
        public void Dispose_ShouldTrimFile()
        {
            var fileName = $"{nameof(Dispose_ShouldTrimFile)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            const int capacityElements = 511 + 512;
            long capacityBytesBeforeAddingEmptyCapacity = 0;
            using (var listMmf = ListMmf<long>.CreateFromFile(fileName, capacityElements: capacityElements))
            {
                listMmf.Capacity.Should().Be(511 + 512, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Count.Should().Be(0);
                for (int i = 0; i < listMmf.Capacity; i++)
                {
                    listMmf.Add(i);
                }
                var capacity = listMmf.Capacity;
                listMmf.Count.Should().Be(capacity);
                capacityBytesBeforeAddingEmptyCapacity = listMmf.CapacityBytes;
                listMmf.Capacity += 512;
                listMmf.Count.Should().Be(capacity, "Increased capacity should not have changed Count");
                listMmf.Capacity.Should().Be(capacity + 512);
            }
            var fileInfo = new FileInfo(fileName);
            fileInfo.Length.Should().Be(capacityBytesBeforeAddingEmptyCapacity);
            File.Delete(fileName);
        }

        [Fact]
        public void IsAnyoneReadingWriting_Tests()
        {
            var mapName = $"{nameof(IsAnyoneReadingWriting_Tests)}";
            var isAnyoneReading = ListMmfBase.IsAnyoneReading(mapName);
            isAnyoneReading.Should().BeFalse("No reader yet.");
            var isAnyoneWriting = ListMmfBase.IsAnyoneWriting(mapName);
            isAnyoneWriting.Should().BeFalse("No writer yet.");

            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateNew(mapName, capacityElements))
            {
                isAnyoneReading = ListMmfBase.IsAnyoneReading(mapName);
                isAnyoneReading.Should().BeFalse("This is a writer, not a reader");
                isAnyoneWriting = ListMmfBase.IsAnyoneWriting(mapName);
                isAnyoneWriting.Should().BeTrue("This is a writer.");
            }
            isAnyoneReading = ListMmfBase.IsAnyoneReading(mapName);
            isAnyoneReading.Should().BeFalse("No reader yet.");
            isAnyoneWriting = ListMmfBase.IsAnyoneWriting(mapName);
            isAnyoneWriting.Should().BeFalse("Writer finished.");
        }
    }
}
