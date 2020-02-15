using System.IO;
using BruSoftware.ListMmf;
using Xunit;

namespace ListMmfTests
{
    public class CtorTests
    {
        [Fact]
        public void Test1()
        {
            using (var listMmf = ListMmf<long>.CreateFromFile("TestPath", capacityElements:10))
            {
                Assert.True(true);
                //File.Delete("TestPath");
            }
            }

    }
}
