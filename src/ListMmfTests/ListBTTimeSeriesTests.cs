using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

public class ListMmfTimeSeriesTests
{
    [Fact]
    public void BinarySearch_ShouldWork()
    {
        // Test BruTrader BinarySearch vs. Array.BinarySearch
        var date0 = new DateTime(2000, 1, 1);
        var date1 = new DateTime(2001, 1, 1);
        var date2 = new DateTime(2002, 1, 1);
        var date3 = new DateTime(2003, 1, 1);
        var date4 = new DateTime(2004, 1, 1);
        var date5 = new DateTime(2005, 1, 1);
        var array = new[] { date1, date2, date2, date4 };
        var expected0 = Array.BinarySearch(array, date0);
        var expected1 = Array.BinarySearch(array, date1);
        var expected2 = Array.BinarySearch(array, date2);
        var expected3 = Array.BinarySearch(array, date3);
        var expected4 = Array.BinarySearch(array, date4);
        var expected5 = Array.BinarySearch(array, date5);

        var path = nameof(BinarySearch_ShouldWork);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        const long testSize = 4;
        using (var timeSeries = new ListMmfTimeSeriesDateTime(path, TimeSeriesOrder.AscendingOrEqual, testSize, MemoryMappedFileAccess.ReadWrite))
        {
            timeSeries.Add(date1);
            timeSeries.Add(date2);
            timeSeries.Add(date2);
            timeSeries.Add(date4);

            var result0 = timeSeries.BinarySearch(date0, 0, testSize);
            result0.Should().Be(expected0);
            var result0C = ~result0;
            Assert.Equal(0, result0C);
            var result1 = timeSeries.BinarySearch(date1, 0, testSize);
            result1.Should().Be(expected1);
            Assert.Equal(0, result1);
            var result2 = timeSeries.BinarySearch(date2, 0, testSize);
            result2.Should().Be(expected2);
            Assert.Equal(1, result2);
            var result3 = timeSeries.BinarySearch(date3, 0, testSize);
            result3.Should().Be(expected3);
            var result4 = timeSeries.BinarySearch(date4, 0, testSize);
            result4.Should().Be(expected4);
            Assert.Equal(3, result4);
            var result5 = timeSeries.BinarySearch(date5, 0, testSize);
            result5.Should().Be(expected5);
            var result5C = ~result5;
            Assert.Equal(4, result5C);
        }
        File.Delete(path);
    }

    [Fact]
    public void GetUpperBound_ShouldWork()
    {
        var date0 = new DateTime(2000, 1, 1);
        var date1 = new DateTime(2001, 1, 1);
        var date2 = new DateTime(2002, 1, 1);
        var date3 = new DateTime(2003, 1, 1);
        var date4 = new DateTime(2004, 1, 1);
        var date5 = new DateTime(2005, 1, 1);

        var path = nameof(GetUpperBound_ShouldWork);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        const long testSize = 4;
        using (var timeSeries = new ListMmfTimeSeriesDateTime(path, TimeSeriesOrder.AscendingOrEqual, testSize, MemoryMappedFileAccess.ReadWrite))
        {
            timeSeries.Add(date1);
            timeSeries.Add(date2);
            timeSeries.Add(date2);
            timeSeries.Add(date4);

            var upper0 = timeSeries.GetUpperBound(date0, 0, testSize);
            Assert.Equal(0, upper0);
            var upper1 = timeSeries.GetUpperBound(date1, 0, testSize);
            Assert.Equal(1, upper1);
            var upper3 = timeSeries.GetUpperBound(date3, 0, testSize);
            Assert.Equal(3, upper3);
            var upper4 = timeSeries.GetUpperBound(date4, 0, testSize);
            Assert.Equal(4, upper4);
            var upper5 = timeSeries.GetUpperBound(date5, 0, testSize);
            Assert.Equal(4, upper5);
        }
        File.Delete(path);
    }

    [Fact]
    public void GetLowerBound_ShouldWork()
    {
        var date0 = new DateTime(2000, 1, 1);
        var date1 = new DateTime(2001, 1, 1);
        var date2 = new DateTime(2002, 1, 1);
        var date3 = new DateTime(2003, 1, 1);
        var date4 = new DateTime(2004, 1, 1);
        var date5 = new DateTime(2005, 1, 1);

        var path = nameof(GetLowerBound_ShouldWork);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        const long testSize = 4;
        using (var timeSeries = new ListMmfTimeSeriesDateTime(path, TimeSeriesOrder.AscendingOrEqual, testSize, MemoryMappedFileAccess.ReadWrite))
        {
            timeSeries.Add(date1);
            timeSeries.Add(date2);
            timeSeries.Add(date2);
            timeSeries.Add(date4);

            var lower0 = timeSeries.GetLowerBound(date0, 0, testSize);
            Assert.Equal(-1, lower0);
            var lower1 = timeSeries.GetLowerBound(date1, 0, testSize);
            Assert.Equal(0, lower1);
            var lower3 = timeSeries.GetLowerBound(date3, 0, testSize);
            Assert.Equal(2, lower3);
            var lower4 = timeSeries.GetLowerBound(date4, 0, testSize);
            Assert.Equal(3, lower4);
            var lower5 = timeSeries.GetLowerBound(date5, 0, testSize);
            Assert.Equal(3, lower5);
        }
        File.Delete(path);
    }
}