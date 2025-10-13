using System;
using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

public sealed class OddByteConversionExtensionsTests : IDisposable
{
    private readonly string _directory;

    public OddByteConversionExtensionsTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "ListMmfTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, true);
        }
    }

    private string GetPath(string fileName)
    {
        return Path.Combine(_directory, fileName);
    }

    [Fact]
    public void CopyAsInt64_UInt24_FillsDestination()
    {
        var path = GetPath("uint24-copy.mmf");
        var count = 1024;
        using var list = new ListMmf<UInt24AsInt64>(path, DataType.UInt24AsInt64, count);

        var expected = new long[count];
        for (var i = 0; i < count; i++)
        {
            var value = (long)(i * 7 % (UInt24AsInt64.MaxValue + 1));
            expected[i] = value;
            list.Add(new UInt24AsInt64(value));
        }

        var destination = new long[count];
        list.CopyAsInt64(0, destination);
        destination.Should().Equal(expected);

        IReadOnlyList64Mmf<UInt24AsInt64> readOnly = list;
        var readOnlyDestination = new long[count];
        readOnly.CopyAsInt64(0, readOnlyDestination);
        readOnlyDestination.Should().Equal(expected);
    }

    [Fact]
    public void CopyAsInt64_Int24_HandlesBoundaryValues()
    {
        var path = GetPath("int24-boundaries.mmf");
        using var list = new ListMmf<Int24AsInt64>(path, DataType.Int24AsInt64, 16);

        var values = new[]
        {
            0L,
            1L,
            -1L,
            Int24AsInt64.MaxValue,
            Int24AsInt64.MaxValue - 1,
            Int24AsInt64.MinValue,
            Int24AsInt64.MinValue + 1
        };

        foreach (var value in values)
        {
            list.Add(new Int24AsInt64(value));
        }

        var destination = new long[values.Length];
        list.CopyAsInt64(0, destination);
        destination.Should().Equal(values);

        IReadOnlyList64Mmf<Int24AsInt64> readOnly = list;
        var readOnlyDestination = new long[values.Length];
        readOnly.CopyAsInt64(0, readOnlyDestination);
        readOnlyDestination.Should().Equal(values);
    }

    [Fact]
    public void CopyAsInt64_UInt40_HandlesBoundaryValues()
    {
        var path = GetPath("uint40-boundaries.mmf");
        using var list = new ListMmf<UInt40AsInt64>(path, DataType.UInt40AsInt64, 8);

        var values = new[]
        {
            0L,
            1L,
            UInt40AsInt64.MaxValue,
            UInt40AsInt64.MaxValue - 1,
            123456789L
        };

        foreach (var value in values)
        {
            list.Add(new UInt40AsInt64(value));
        }

        var destination = new long[values.Length];
        list.CopyAsInt64(0, destination);
        destination.Should().Equal(values);

        IReadOnlyList64Mmf<UInt40AsInt64> readOnly = list;
        var readOnlyDestination = new long[values.Length];
        readOnly.CopyAsInt64(0, readOnlyDestination);
        readOnlyDestination.Should().Equal(values);
    }

    [Fact]
    public void CopyAsInt64_Int40_HandlesBoundaryValues()
    {
        var path = GetPath("int40-boundaries.mmf");
        using var list = new ListMmf<Int40AsInt64>(path, DataType.Int40AsInt64, 8);

        var values = new[]
        {
            0L,
            1L,
            -1L,
            Int40AsInt64.MaxValue,
            Int40AsInt64.MaxValue - 1,
            Int40AsInt64.MinValue,
            Int40AsInt64.MinValue + 1
        };

        foreach (var value in values)
        {
            list.Add(new Int40AsInt64(value));
        }

        var destination = new long[values.Length];
        list.CopyAsInt64(0, destination);
        destination.Should().Equal(values);

        IReadOnlyList64Mmf<Int40AsInt64> readOnly = list;
        var readOnlyDestination = new long[values.Length];
        readOnly.CopyAsInt64(0, readOnlyDestination);
        readOnlyDestination.Should().Equal(values);
    }

    [Fact]
    public void RentAsInt64_ReturnsTrimmedMemory()
    {
        var path = GetPath("rent-trim.mmf");
        using var list = new ListMmf<UInt24AsInt64>(path, DataType.UInt24AsInt64, 32);

        var values = new long[] { 0L, 7L, 42L, UInt24AsInt64.MaxValue };
        foreach (var value in values)
        {
            list.Add(new UInt24AsInt64(value));
        }

        var owner = list.RentAsInt64(0, values.Length);
        var span = owner.Memory.Span;
        span.Length.Should().Be(values.Length);
        span.ToArray().Should().Equal(values);

        Action dispose = owner.Dispose;
        dispose.Should().NotThrow();

        using var readOnlyList = new ListMmf<UInt24AsInt64>(GetPath("rent-ro.mmf"), DataType.UInt24AsInt64, values.Length);
        foreach (var value in values)
        {
            readOnlyList.Add(new UInt24AsInt64(value));
        }

        IReadOnlyList64Mmf<UInt24AsInt64> readOnly = readOnlyList;
        var readOnlyOwner = readOnly.RentAsInt64(1, values.Length - 1);
        var readOnlySpan = readOnlyOwner.Memory.Span;
        readOnlySpan.Length.Should().Be(values.Length - 1);
        readOnlySpan.ToArray().Should().Equal(values[1..]);

        Action readOnlyDispose = readOnlyOwner.Dispose;
        readOnlyDispose.Should().NotThrow();
    }
}
