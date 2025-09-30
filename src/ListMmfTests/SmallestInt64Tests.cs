using System;
using System.Collections.Generic;
using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

// ReSharper disable BuiltInTypeReferenceStyle

namespace ListMmfTests;

public class SmallestInt64Tests : IDisposable
{
    private const string TestPath = "TestPath";
    private static int s_count;

    public SmallestInt64Tests()
    {
        s_count++;
        if (File.Exists(TestPath)) File.Delete(TestPath);
    }

    public void Dispose()
    {
        if (File.Exists(TestPath)) File.Delete(TestPath);
    }

    [Fact]
    public void SByteTests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.SByte, TestPath);
        smallest.WidthBits.Should().Be(8 * sizeof(SByte));
        smallest.Add(SByte.MaxValue);
        var check = smallest[0];
        check.Should().Be(sbyte.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(SByte.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(SByte.MaxValue);
        smallest.SetLast(SByte.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(SByte.MinValue);

        var readOnlyList = (IReadOnlyList64Mmf<long>)smallest;
        var test = readOnlyList[0];
    }

    [Fact]
    public void ShortTests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt16, TestPath);
        smallest.WidthBits.Should().Be(8 * sizeof(UInt16));
        smallest.Add(UInt16.MaxValue);
        var check = smallest[0];
        check.Should().Be(UInt16.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(UInt16.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(UInt16.MaxValue);
        smallest.SetLast(UInt16.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(UInt16.MinValue);

        //IReadOnlyList64Mmf<short> readOnlyList64BT = smallest;
    }

    [Fact]
    public void IntTests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.Int32, TestPath);
        smallest.WidthBits.Should().Be(8 * sizeof(Int32));
        smallest.Add(Int32.MaxValue);
        var check = smallest[0];
        check.Should().Be(Int32.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        smallest.AddRange(range);
        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(Int32.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(int.MaxValue);
        smallest.SetLast(Int32.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(Int32.MinValue);
    }

    [Fact]
    public void LongTests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.Int64, TestPath);
        smallest.WidthBits.Should().Be(8 * sizeof(Int64));
        smallest.Add(Int64.MaxValue);
        var check = smallest[0];
        check.Should().Be(Int64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(Int64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(Int64.MaxValue);
        smallest.SetLast(Int64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(Int64.MinValue);
    }

    [Fact]
    public void BitArrayTests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.Bit, TestPath);
        smallest.WidthBits.Should().Be(1);
        smallest.Add(1);
        var check = smallest[0];
        check.Should().Be(1);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 0, 1 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(0);
        smallest.SetLast(0);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(0);
        smallest.SetLast(1);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(1);
    }

    [Fact]
    public void ByteTests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.Byte, TestPath);
        smallest.WidthBits.Should().Be(8 * sizeof(Byte));
        smallest.Add(Byte.MaxValue);
        var check = smallest[0];
        check.Should().Be(Byte.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(Byte.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(Byte.MaxValue);
        smallest.SetLast(Byte.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(Byte.MinValue);
    }

    [Fact]
    public void UShortTests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt16, TestPath);
        smallest.WidthBits.Should().Be(8 * sizeof(UInt16));
        smallest.Add(UInt16.MaxValue);
        var check = smallest[0];
        check.Should().Be(UInt16.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(UInt16.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(UInt16.MaxValue);
        smallest.SetLast(UInt16.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(UInt16.MinValue);
    }

    [Fact]
    public void UInt24Tests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt24AsInt64, TestPath);
        smallest.WidthBits.Should().Be(24);
        smallest.Add(UInt24AsInt64.MaxValue);
        var check = smallest[0];
        check.Should().Be(UInt24AsInt64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(UInt24AsInt64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(UInt24AsInt64.MaxValue);
        smallest.SetLast(UInt24AsInt64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(UInt24AsInt64.MinValue);
    }

    [Fact]
    public void Int24Tests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.Int24AsInt64, TestPath);
        smallest.WidthBits.Should().Be(24);

        smallest.Add(Int24AsInt64.MaxValue);
        var check = smallest[0];
        check.Should().Be(Int24AsInt64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(Int24AsInt64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(Int24AsInt64.MaxValue);
        smallest.SetLast(Int24AsInt64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(Int24AsInt64.MinValue);
        smallest.SetLast(Int24AsInt64.MinValue / 2);
        var checkLast3 = smallest[smallest.Count - 1];
        checkLast3.Should().Be(Int24AsInt64.MinValue / 2);
    }

    [Fact]
    public void UInt32Tests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt32, TestPath);
        smallest.WidthBits.Should().Be(8 * sizeof(UInt32));
        smallest.Add(UInt32.MaxValue);
        var check = smallest[0];
        check.Should().Be(UInt32.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(UInt32.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(uint.MaxValue);
        smallest.SetLast(UInt32.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(UInt32.MinValue);
    }

    [Fact]
    public void UInt40Tests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt40AsInt64, TestPath);
        smallest.WidthBits.Should().Be(40);
        smallest.Add(UInt40AsInt64.MaxValue);
        var check = smallest[0];
        check.Should().Be(UInt40AsInt64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(UInt40AsInt64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(UInt40AsInt64.MaxValue);
        smallest.SetLast(UInt40AsInt64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(UInt40AsInt64.MinValue);
    }

    [Fact]
    public void UInt48Tests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt48AsInt64, TestPath);
        smallest.WidthBits.Should().Be(48);
        smallest.Add(UInt48AsInt64.MaxValue);
        var check = smallest[0];
        check.Should().Be(UInt48AsInt64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(UInt48AsInt64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(UInt48AsInt64.MaxValue);
        smallest.SetLast(UInt48AsInt64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(UInt48AsInt64.MinValue);
    }

    [Fact]
    public void UInt56Tests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt56AsInt64, TestPath);
        smallest.WidthBits.Should().Be(56);
        smallest.Add(UInt56AsInt64.MaxValue);
        var check = smallest[0];
        check.Should().Be(UInt56AsInt64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(UInt56AsInt64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(UInt56AsInt64.MaxValue);
        smallest.SetLast(UInt56AsInt64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(UInt56AsInt64.MinValue);
    }

    [Fact]
    public void Int40Tests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.Int40AsInt64, TestPath);
        smallest.WidthBits.Should().Be(40);
        smallest.Add(Int40AsInt64.MaxValue);
        var check = smallest[0];
        check.Should().Be(Int40AsInt64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(Int40AsInt64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(Int40AsInt64.MaxValue);
        smallest.SetLast(Int40AsInt64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(Int40AsInt64.MinValue);
        smallest.SetLast(Int40AsInt64.MinValue / 2);
        var checkLast3 = smallest[smallest.Count - 1];
        checkLast3.Should().Be(Int40AsInt64.MinValue / 2);
    }

    [Fact]
    public void Int48Tests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.Int48AsInt64, TestPath);
        smallest.WidthBits.Should().Be(48);
        smallest.Add(Int48AsInt64.MaxValue);
        var check = smallest[0];
        check.Should().Be(Int48AsInt64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(Int48AsInt64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(Int48AsInt64.MaxValue);
        smallest.SetLast(Int48AsInt64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(Int48AsInt64.MinValue);
        smallest.SetLast(Int48AsInt64.MinValue / 2);
        var checkLast3 = smallest[smallest.Count - 1];
        checkLast3.Should().Be(Int48AsInt64.MinValue / 2);
    }

    [Fact]
    public void Int56Tests()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.Int56AsInt64, TestPath);
        smallest.WidthBits.Should().Be(56);
        smallest.Add(Int56AsInt64.MaxValue);
        var check = smallest[0];
        check.Should().Be(Int56AsInt64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(Int56AsInt64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(Int56AsInt64.MaxValue);
        smallest.SetLast(Int56AsInt64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(Int56AsInt64.MinValue);
        smallest.SetLast(Int56AsInt64.MinValue / 2);
        var checkLast3 = smallest[smallest.Count - 1];
        checkLast3.Should().Be(Int56AsInt64.MinValue / 2);
    }

    [Fact]
    public void ULongTests()
    {
        // We actually do Int64 not UInt64, because this class max values are Int65
        using var smallest = new SmallestInt64ListMmf(DataType.UInt64, TestPath);
        smallest.WidthBits.Should().Be(8 * sizeof(Int64));
        smallest.Add(Int64.MaxValue);
        var check = smallest[0];
        check.Should().Be(Int64.MaxValue);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 1, 2, 3 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(2);
        smallest.SetLast(Int64.MaxValue);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(Int64.MaxValue);
        smallest.SetLast(Int64.MinValue);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(long.MinValue);
    }

    [Fact]
    public void EmptyFileTest()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.AnyStruct, TestPath);
        smallest.WidthBits.Should().Be(0, "There is no file.");
        smallest.Add(0);
        var check = smallest[0];
        check.Should().Be(0);
        var check2 = smallest.ReadUnchecked(0);
        check2.Should().Be(check);
        var range = new List<long> { 0, 0, 0 };
        smallest.AddRange(range);
        foreach (var item in smallest)
        {
            // Check that enumeration works
        }

        var check1 = smallest[2];
        check1.Should().Be(0);
        smallest.SetLast(0);
        var checkLast1 = smallest[smallest.Count - 1];
        checkLast1.Should().Be(0);
        smallest.SetLast(0);
        var checkLast2 = smallest[smallest.Count - 1];
        checkLast2.Should().Be(0);
    }
}