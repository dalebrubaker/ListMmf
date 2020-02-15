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
            using (var listMmf = ListMmf<long>.CreateNew(testMapName, capacityElements: capacityElements))
            {
                listMmf.Capacity.Should().Be(512, "Capacity is rounded up to the 4096 page size used in a view");
            }
        }

        [Fact]
        public void CreateFile_SetsCapacity()
        {
            const string testPath = "TestPath";
            const int capacityElements = 10;
            using (var listMmf = ListMmf<long>.CreateFromFile(testPath, capacityElements: capacityElements))
            {
                listMmf.Capacity.Should().Be(512, "Capacity is rounded up to the 4096 page size used in a view");
            }
            File.Delete(testPath);
        }


    }
}