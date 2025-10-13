using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

public sealed class ListMmfLongAdapterTests : IDisposable
{
    private readonly string _directory;

    public ListMmfLongAdapterTests()
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

    private string GetPath(string fileName) => Path.Combine(_directory, fileName);

    [Fact]
    public void OpenAsInt64_ProvidesLongViewForOddWidths()
    {
        var path = GetPath("odd-long-view.mmf");
        var initialValues = new[] { 1L, 12345L, 987654L, UInt24AsInt64.MaxValue - 1 };

        using (var writer = new ListMmf<UInt24AsInt64>(path, DataType.UInt24AsInt64, initialValues.Length))
        {
            foreach (var value in initialValues)
            {
                writer.Add(new UInt24AsInt64(value));
            }
        }

        using var adapter = (IListMmfLongAdapter<UInt24AsInt64>)UtilsListMmf.OpenAsInt64(path, MemoryMappedFileAccess.ReadWrite);
        adapter.Count.Should().Be(initialValues.Length);
        adapter.AsSpan(0, initialValues.Length).ToArray().Should().Equal(initialValues);

        var highValue = UInt24AsInt64.MaxValue - 5;
        adapter.Add(highValue);
        adapter[adapter.Count - 1].Should().Be(highValue);

        var status = adapter.GetDataTypeUtilization();
        var expectedObservedMax = Math.Max(highValue, UInt24AsInt64.MaxValue - 1);
        status.ObservedMax.Should().Be(expectedObservedMax);
        status.ObservedMin.Should().Be(initialValues[0]);
        status.AllowedMax.Should().Be(UInt24AsInt64.MaxValue);
        var expectedUtilization = (double)expectedObservedMax / UInt24AsInt64.MaxValue;
        status.Utilization.Should().BeApproximately(expectedUtilization, 1e-9);

        adapter.Invoking(x => x.Add(UInt24AsInt64.MaxValue + 1))
            .Should().Throw<DataTypeOverflowException>()
            .Which.AttemptedValue.Should().Be(UInt24AsInt64.MaxValue + 1);
    }

    [Fact]
    public void ConfigureUtilizationWarning_FiresWhenThresholdReached()
    {
        var path = GetPath("warning-trigger.mmf");
        using (var writer = new ListMmf<Int40AsInt64>(path, DataType.Int40AsInt64, 4))
        {
            writer.Add(new Int40AsInt64(0));
            writer.Add(new Int40AsInt64(100));
        }

        using var adapter = (IListMmfLongAdapter<Int40AsInt64>)UtilsListMmf.OpenAsInt64(path, MemoryMappedFileAccess.ReadWrite);
        var triggered = false;
        adapter.ConfigureUtilizationWarning(0.5, _ => triggered = true);

        triggered.Should().BeFalse();
        adapter.Add(Int40AsInt64.MaxValue / 2);
        triggered.Should().BeTrue();
    }

    [Fact]
    public void OverflowSuggestion_UsesExistingRange_WhenNegativesPresent()
    {
        // Arrange: create a signed Int24 file with negative values
        var path = GetPath("signed-int24-overflow-suggestion.mmf");
        using (var writer = new ListMmf<Int24AsInt64>(path, DataType.Int24AsInt64, 3))
        {
            writer.Add(new Int24AsInt64(-1));
            writer.Add(new Int24AsInt64(-100));
            writer.Add(new Int24AsInt64(0));
        }

        using var adapter = (IListMmfLongAdapter<Int24AsInt64>)UtilsListMmf.OpenAsInt64(path, MemoryMappedFileAccess.ReadWrite);

        // Act: attempt to add a value that exceeds Int24 max on the positive side
        var overflow = Int24AsInt64.MaxValue + 1; // positive overflow
        var act = () => adapter.Add(overflow);

        // Assert: suggestion should NOT be an unsigned type; it should suggest a signed upgrade (Int32 here)
        act.Should().Throw<DataTypeOverflowException>()
            .Which.SuggestedDataType.Should().Be(DataType.Int32);
    }
    [Fact]
    public void OpenExistingListMmf_ReturnsOddTypedList()
    {
        var path = GetPath("typed-open.mmf");
        using (var writer = new ListMmf<Int48AsInt64>(path, DataType.Int48AsInt64, 2))
        {
            writer.Add(new Int48AsInt64(42));
            writer.Add(new Int48AsInt64(Int48AsInt64.MinValue));
        }

        using var list = UtilsListMmf.OpenExistingListMmf(path, MemoryMappedFileAccess.ReadWrite, useSmallestInt: false);
        list.Should().BeOfType<ListMmf<Int48AsInt64>>();
    }
}
