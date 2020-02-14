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
            var listMmf = ListMmf<long>.CreateFromFile("TestPath", FileMode.OpenOrCreate, null, 10);
            Assert.True(true);

            //File.Delete("TestPath");
        }
    }
}
