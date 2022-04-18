using System;
using System.Collections.Generic;
using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

public enum TestEnum
{
    MinusOne = -1,
    Zero,
    One,
    IntMaxValue = byte.MaxValue
}

public class SmallestEnumTests : IDisposable
{
    private const string TestPath = "TestPath";

    public SmallestEnumTests()
    {
        if (File.Exists(TestPath))
        {
            File.Delete(TestPath);
        }
    }

    public void Dispose()
    {
        if (File.Exists(TestPath))
        {
            File.Delete(TestPath);
        }
    }

    [Fact]
    public void TestEnumTests()
    {
        using var smallest = new SmallestEnumListMmf<TestEnum>(typeof(TestEnum), TestPath);
        smallest.WidthBits.Should().Be(8 * sizeof(short));
        smallest.Add(TestEnum.One);
        var check = smallest[0];
        check.Should().Be(TestEnum.One);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<TestEnum> { TestEnum.MinusOne, TestEnum.Zero, TestEnum.IntMaxValue };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }
        smallest.Count.Should().Be(4);
        var check1 = smallest[2];
        check1.Should().Be(TestEnum.Zero);
        smallest.SetLast(TestEnum.MinusOne);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(TestEnum.MinusOne);
        smallest.SetLast(TestEnum.Zero);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(TestEnum.Zero);

        var readOnlyList = (IReadOnlyList64Mmf<TestEnum>)smallest;
        var test = readOnlyList[0];
    }
}