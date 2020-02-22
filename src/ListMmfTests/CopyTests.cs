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
                list.Count.Should().Be(init.Count);
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

        [Fact]
        public void Copy_ForwardsNotOverlapping()
        {
            var init = new List<int>
            {
                0, 1, 2, 3
            };
            using (var list = TestListMmf<int>.CreateTestFile(init))
            {
                list.Count.Should().Be(init.Count);
                var toListBefore = list.ToList();
                toListBefore.Should().BeEquivalentTo(init, opt => opt.WithStrictOrdering());
                list.Copy(0, 2, 2);
                list.Count.Should().Be(init.Count);
                var toListAfter = list.ToList();
                var expected = new List<int>
                {
                    0, 1, 0, 1
                };
                toListAfter.Should().BeEquivalentTo(expected, opt => opt.WithStrictOrdering());
            }
        }


        [Fact]
        public void Copy_BackwardsNotOverlapping()
        {
            var init = new List<int>
            {
                0, 1, 2, 3
            };
            using (var list = TestListMmf<int>.CreateTestFile(init))
            {
                list.Count.Should().Be(init.Count);
                var toListBefore = list.ToList();
                toListBefore.Should().BeEquivalentTo(init, opt => opt.WithStrictOrdering());
                list.Copy(2, 0, 2);
                list.Count.Should().Be(init.Count);
                var toListAfter = list.ToList();
                var expected = new List<int>
                {
                    2, 3, 2, 3
                };
                toListAfter.Should().BeEquivalentTo(expected, opt => opt.WithStrictOrdering());
            }
        }

        [Fact]
        public void Copy_ForwardsPastCount()
        {
            var init = new List<int>
            {
                0, 1, 2
            };
            using (var list = TestListMmf<int>.CreateTestFile(init))
            {
                list.Count.Should().Be(init.Count);
                var toListBefore = list.ToList();
                toListBefore.Should().BeEquivalentTo(init, opt => opt.WithStrictOrdering());
                list.Copy(1, 6, 2);
                list.Count.Should().Be(8);
                var toListAfter = list.ToList();
                var expected = new List<int>
                {
                    0, 1, 2, 0, 0, 0, 1, 2
                };
                toListAfter.Should().BeEquivalentTo(expected, opt => opt.WithStrictOrdering());
            }
        }
        
        [Fact]
        public void Copy_ForwardsOverlapping()
        {
            var init = new List<int>
            {
                0, 1, 2, 3, 4
            };
            using (var list = TestListMmf<int>.CreateTestFile(init))
            {
                list.Count.Should().Be(init.Count);
                var toListBefore = list.ToList();
                toListBefore.Should().BeEquivalentTo(init, opt => opt.WithStrictOrdering());
                list.Copy(0, 1, 3);
                list.Count.Should().Be(init.Count);
                var toListAfter = list.ToList();
                var expected = new List<int>
                {
                    0, 0, 1, 2, 4
                };
                toListAfter.Should().BeEquivalentTo(expected, opt => opt.WithStrictOrdering());
            }
        }

        /*
            TODO
            Copy forwards overlapping and backwards overlapping
         */
    }
}
