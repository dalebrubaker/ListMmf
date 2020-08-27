using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace DummySkipTests
{
    public class DummyTests
    {
        public DummyTests(ITestOutputHelper output)
        {
            output.WriteLine("Here is a test output from DummyTests.ctor");
        }

        [Fact]
        public void Dummy()
        {
            Assert.True(false, "This always fails.");
        }
    }
}
