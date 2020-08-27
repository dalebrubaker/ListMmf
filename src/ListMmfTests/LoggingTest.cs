using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using Xunit;
using Xunit.Abstractions;

namespace ListMmfTests
{
    public class LoggingTest
    {
        private static readonly ILogger s_logger = LogManager.GetCurrentClassLogger();

        public LoggingTest(ITestOutputHelper output)
        {
            output.WriteLine("Here is a test output from LoggingTest.ctor");
        }


        [Fact]
        public void Fails_Test()
        {
            Assert.True(false, "This always fails.");
        }

        [Fact]
        public void NLog_Test()
        {

            Assert.True(true, "This never fails.");
        }

        [Fact]
        public void X64_Test()
        {
            Assert.True(Environment.Is64BitProcess, "64 Bit process is required.");
        }

    }
}
