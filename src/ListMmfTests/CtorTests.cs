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
            const string testMapName = "TestMapName";
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateNew(testMapName, capacityElements))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            }
        }

        [Fact]
        public void CreateFile_SetsCapacity()
        {
            const string testPath = "TestPath";
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateFromFile(testPath, capacityElements: capacityElements))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            }
            File.Delete(testPath);
        }

        [Fact]
        public void CapacitySet_ShouldGrowCount()
        {
            const string testMapName = "TestMapName";
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateNew(testMapName, capacityElements))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Capacity = 511 + 512; // add another page
                listMmf.Capacity.Should().Be(511 + 512);
            }
        }
    }
}
