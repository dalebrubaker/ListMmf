using System;
using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

public class ListMmfTimeSeriesDateTimeSecondsTests
{
    [Fact]
    public void InterpolationLowerBound_LastElement_NoHang()
    {
        // Arrange: create uniformly increasing seconds so interpolation picks pos == high for last value
        const string path = nameof(InterpolationLowerBound_LastElement_NoHang) + ".mmf";
        if (File.Exists(path)) File.Delete(path);

        const int count = 20_000; // >= InterpolationMinSize to ensure interpolation path
        var baseTime = new DateTime(2020, 1, 1);

        using (var list = new ListMmfTimeSeriesDateTimeSeconds(path, TimeSeriesOrder.Ascending, count))
        {
            for (int i = 0; i < count; i++)
            {
                list.Add(baseTime.AddSeconds(i));
            }

            // Act: search for the last element using Interpolation strategy
            var searchTime = baseTime.AddSeconds(count - 1);
            var index = list.LowerBound(searchTime, SearchStrategy.Interpolation);

            // Assert
            index.Should().Be(count - 1);
        }

        File.Delete(path);
    }

    [Fact]
    public void InterpolationUpperBound_LastElement_NoHang()
    {
        // Arrange
        const string path = nameof(InterpolationUpperBound_LastElement_NoHang) + ".mmf";
        if (File.Exists(path)) File.Delete(path);

        const int count = 20_000;
        var baseTime = new DateTime(2020, 1, 1);

        using (var list = new ListMmfTimeSeriesDateTimeSeconds(path, TimeSeriesOrder.Ascending, count))
        {
            for (int i = 0; i < count; i++)
            {
                list.Add(baseTime.AddSeconds(i));
            }

            // Act: upper bound for last value should be Count
            var searchTime = baseTime.AddSeconds(count - 1);
            var index = list.UpperBound(0, list.Count, searchTime, SearchStrategy.Interpolation);

            // Assert
            index.Should().Be(count);
        }

        File.Delete(path);
    }
}

