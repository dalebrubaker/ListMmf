using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

// ReSharper disable ConvertToUsingDeclaration

namespace ListMmfTests
{
    public class CopyTests
    {
        [Fact]
        public void Copy_ZeroCount()
        {
            using (var list = TestListMmf<int>.CreateTestFile(0))
            {
                list.Copy(0, 0, 0);
                
            }
        }


    }
}
