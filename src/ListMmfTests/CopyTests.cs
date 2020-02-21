using System.Collections.Generic;
using FluentAssertions;
using Xunit;

// ReSharper disable ConvertToUsingDeclaration

namespace ListMmfTests
{
    public class CopyTests
    {
        [Fact]
        public void Copy_ZeroIntoEmpty()
        {
            using (var list = TestListMmf<int>.CreateTestFile())
            {
                list.Copy(0, 0, 0);
                list.Count.Should().Be(0);
            }
        }

        [Fact]
        public void Copy_LikeAddRange()
        {
            var init = new List<int>
            {
                0, 1
            };
            using (var list = TestListMmf<int>.CreateTestFile(init))
            {
                list.Count.Should().Be(2);
                var toListBefore = list.ToList();
                toListBefore.Should().BeEquivalentTo(init, opt => opt.WithStrictOrdering());
                list.Copy(0, 2, 2);
                list.Count.Should().Be(4);
                var toListAfter = list.ToList();
                var expected = new List<int>
                {
                    0, 1, 0, 1
                };
                toListAfter.Should().BeEquivalentTo(expected, opt => opt.WithStrictOrdering());
            }
        }

        /*
            TODO
            2 at a time
            Each test asserts Count
            Compare .ToList with initialized array
            Copy to start at. Count
            Copy backwards and forwards
            Copy forwards overlapping and backwards overlapping
         */
    }
}
