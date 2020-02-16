using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

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
        public void CapacitySet_ShouldGrowCapacity()
        {
            var mapName = $"{nameof(CapacitySet_ShouldGrowCapacity)}";
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateNew(mapName, capacityElements))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Capacity = 511 + 512; // add another page
                listMmf.Capacity.Should().Be(511 + 512);
            }
        }
    }
}
