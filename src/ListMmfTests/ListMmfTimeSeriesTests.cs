using System;
using System.IO;
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

        const string Path = nameof(BinarySearch_ShouldWork);
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
        const long TestSize = 4;
        using (var timeSeries = new ListMmfTimeSeriesDateTime(Path, TimeSeriesOrder.AscendingOrEqual, TestSize))
        {
            timeSeries.Add(date1);
            timeSeries.Add(date2);
            timeSeries.Add(date2);
            timeSeries.Add(date4);

            var result0 = timeSeries.BinarySearch(date0, 0, TestSize);
            result0.Should().Be(expected0);
            var result0C = ~result0;
            Assert.Equal(0, result0C);
            var result1 = timeSeries.BinarySearch(date1, 0, TestSize);
            result1.Should().Be(expected1);
            Assert.Equal(0, result1);
            var result2 = timeSeries.BinarySearch(date2, 0, TestSize);
            result2.Should().Be(expected2);
            Assert.Equal(1, result2);
            var result3 = timeSeries.BinarySearch(date3, 0, TestSize);
            result3.Should().Be(expected3);
            var result4 = timeSeries.BinarySearch(date4, 0, TestSize);
            result4.Should().Be(expected4);
            Assert.Equal(3, result4);
            var result5 = timeSeries.BinarySearch(date5, 0, TestSize);
            result5.Should().Be(expected5);
            var result5C = ~result5;
            Assert.Equal(4, result5C);
        }
        File.Delete(Path);
    }

    /// <summary>
    /// See example at https://en.cppreference.com/w/cpp/algorithm/lower_bound
    /// </summary>
    [Fact]
    public void GetLowerBound_ShouldWork()
    {
        var date0 = new DateTime(2000, 1, 1);
        var date1 = new DateTime(2001, 1, 1);
        var date2 = new DateTime(2002, 1, 1);
        var date3 = new DateTime(2003, 1, 1);
        var date4 = new DateTime(2004, 1, 1);
        var date5 = new DateTime(2005, 1, 1);
        var date6 = new DateTime(2006, 1, 1);
        var date7 = new DateTime(2007, 1, 1);

        const string Path = nameof(GetLowerBound_ShouldWork);
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
        const long TestSize = 6;
        using (var timeSeries = new ListMmfTimeSeriesDateTime(Path, TimeSeriesOrder.AscendingOrEqual, TestSize))
        {
            timeSeries.Add(date1);
            timeSeries.Add(date2);
            timeSeries.Add(date4);
            timeSeries.Add(date5);
            timeSeries.Add(date5);
            timeSeries.Add(date6);

            var lower0 = timeSeries.LowerBound(0, TestSize, date0);
            Assert.Equal(0, lower0);
            var lower1 = timeSeries.LowerBound(0, TestSize, date1);
            Assert.Equal(0, lower1);
            var lower2 = timeSeries.LowerBound(0, TestSize, date2);
            Assert.Equal(1, lower2);
            var lower3 = timeSeries.LowerBound(0, TestSize, date3);
            Assert.Equal(2, lower3);
            var lower4 = timeSeries.LowerBound(0, TestSize, date4);
            Assert.Equal(2, lower4);
            var lower5 = timeSeries.LowerBound(0, TestSize, date5);
            Assert.Equal(3, lower5);
            var lower6 = timeSeries.LowerBound(0, TestSize, date6);
            Assert.Equal(5, lower6);
            var lower7 = timeSeries.LowerBound(0, TestSize, date7);
            Assert.Equal(6, lower7); // not found

            var date5plus = date5.AddMinutes(100);
            var lower5plus = timeSeries.LowerBound(date5plus);
            Assert.Equal(5,
                lower5plus); // Returns an iterator pointing to the first element in the range [first,last) which does not compare less than val.
        }
        File.Delete(Path);
    }

    /// <summary>
    /// See example at https://en.cppreference.com/w/cpp/algorithm/lower_bound
    /// </summary>
    [Fact]
    public void GetUpperBound_ShouldWork()
    {
        var date0 = new DateTime(2000, 1, 1);
        var date1 = new DateTime(2001, 1, 1);
        var date2 = new DateTime(2002, 1, 1);
        var date3 = new DateTime(2003, 1, 1);
        var date4 = new DateTime(2004, 1, 1);
        var date5 = new DateTime(2005, 1, 1);
        var date6 = new DateTime(2006, 1, 1);

        const string Path = nameof(GetLowerBound_ShouldWork);
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
        const long TestSize = 6;
        using (var timeSeries = new ListMmfTimeSeriesDateTime(Path, TimeSeriesOrder.AscendingOrEqual, TestSize))
        {
            timeSeries.Add(date1);
            timeSeries.Add(date2);
            timeSeries.Add(date4);
            timeSeries.Add(date5);
            timeSeries.Add(date5);
            timeSeries.Add(date6);

            var upper0 = timeSeries.UpperBound(0, TestSize, date0);
            Assert.Equal(0, upper0);
            var upper1 = timeSeries.UpperBound(0, TestSize, date1);
            Assert.Equal(1, upper1);
            var upper2 = timeSeries.UpperBound(0, TestSize, date2);
            Assert.Equal(2, upper2);
            var upper3 = timeSeries.UpperBound(0, TestSize, date3);
            Assert.Equal(2, upper3);
            var upper4 = timeSeries.UpperBound(0, TestSize, date4);
            Assert.Equal(3, upper4);
            var upper5 = timeSeries.UpperBound(0, TestSize, date5);
            Assert.Equal(5, upper5);
            var upper6 = timeSeries.UpperBound(0, TestSize, date6);
            Assert.Equal(6, upper6); // not found
        }
        File.Delete(Path);
    }
}