using BruSoftware.ListMmf;
using Xunit;

namespace ListMmfTests
{
    public class CtorTests
    {
        [Fact]
        public void Test1()
        {
            var listMmf = ListMmf<long>.CreateFromFile("TestPath");
            Assert.True(true);
        }
    }
}
