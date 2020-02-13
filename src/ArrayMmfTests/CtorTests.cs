using System;
using BruSoftware.ArrayMmf;
using Xunit;

namespace ArrayMmfTests
{
    public class CtorTests
    {
        [Fact]
        public void Test1()
        {
            var arrayMmf = ArrayMmf<long>.CreateFromFile("TestPath");
            Assert.True(true);
        }
    }
}
