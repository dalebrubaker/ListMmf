using System;
using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

public class UtilsListMmfDateTimeTests : IDisposable
{
    private readonly string _testPath;
    private readonly DateTime _baseDate = new(2024, 1, 1, 12, 0, 0, DateTimeKind.Unspecified);

    public UtilsListMmfDateTimeTests()
    {
        var cwd = Directory.GetCurrentDirectory();
        _testPath = Path.Combine(cwd, "Timestamps.bt");
    }

    public void Dispose()
    {
        if (File.Exists(_testPath))
        {
            File.Delete(_testPath);
        }
    }

    [Fact]
    public void GetHeaderInfoDateTime_WithValidIndex_ReturnsCorrectDateTime()
    {
        // Arrange
        var timestamps = new[]
        {
            _baseDate,
            _baseDate.AddHours(1),
            _baseDate.AddHours(2),
            _baseDate.AddHours(3)
        };

        CreateTestTimestampFile(timestamps);

        // Act & Assert
        for (var i = 0; i < timestamps.Length; i++)
        {
            var result = UtilsListMmf.GetHeaderInfoDateTime(_testPath, i);
            result.Should().Be(timestamps[i], $"index {i} should return correct timestamp");
        }
    }

    [Fact]
    public void GetHeaderInfoDateTime_WithNegativeIndex_ThrowsException()
    {
        // Arrange
        CreateTestTimestampFile(new[] { _baseDate });

        // Act & Assert
        var action = () => UtilsListMmf.GetHeaderInfoDateTime(_testPath, -1L);
        action.Should().Throw<ListMmfException>()
            .WithMessage("*Index -1 is out of range*");
    }

    [Fact]
    public void GetHeaderInfoDateTime_WithIndexTooLarge_ThrowsException()
    {
        // Arrange
        CreateTestTimestampFile(new[] { _baseDate, _baseDate.AddHours(1) });

        // Act & Assert
        var action = () => UtilsListMmf.GetHeaderInfoDateTime(_testPath, 2L);
        action.Should().Throw<ListMmfException>()
            .WithMessage("*Index 2 is out of range. File contains 2 records*");
    }

    [Fact]
    public void GetHeaderInfoDateTime_WithNonTimestampFile_ThrowsException()
    {
        // Arrange - Create a file with double data instead of timestamps
        using (var list = new ListMmf<double>(_testPath, DataType.Double))
        {
            list.Add(1.5);
            list.Add(2.5);
        }

        // Act & Assert
        var action = () => UtilsListMmf.GetHeaderInfoDateTime(_testPath, 0L);
        action.Should().Throw<ListMmfException>()
            .WithMessage("*File contains Double data, not timestamp data (UnixSeconds)*");
    }

    [Fact]
    public void GetHeaderInfoDateTime_WithNonExistentFile_ReturnsMinValue()
    {
        // Arrange
        var nonExistentPath = Path.GetTempFileName();
        File.Delete(nonExistentPath); // Ensure it doesn't exist

        // Act
        var result = UtilsListMmf.GetHeaderInfoDateTime(nonExistentPath, 0L);

        // Assert
        result.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void GetHeaderInfoDateTime_WithEmptyFile_ThrowsException()
    {
        // Arrange - Create empty file
        CreateEmptyTimestampFile();

        // Act & Assert
        var action = () => UtilsListMmf.GetHeaderInfoDateTime(_testPath, 0L);
        action.Should().Throw<ListMmfException>()
            .WithMessage("*Index 0 is out of range. File contains 0 records*");
    }

    [Fact]
    public void GetHeaderInfoDateTime_WithFirstAndLastIndex_ReturnsCorrectValues()
    {
        // Arrange
        var startTime = new DateTime(2024, 6, 15, 9, 30, 0, DateTimeKind.Unspecified);
        var timestamps = new[]
        {
            startTime,
            startTime.AddMinutes(30),
            startTime.AddHours(1),
            startTime.AddHours(6.5) // Market close
        };

        CreateTestTimestampFile(timestamps);

        // Act
        var firstTimestamp = UtilsListMmf.GetHeaderInfoDateTime(_testPath, 0L);
        var lastTimestamp = UtilsListMmf.GetHeaderInfoDateTime(_testPath, timestamps.Length - 1);

        // Assert
        firstTimestamp.Should().Be(timestamps[0]);
        lastTimestamp.Should().Be(timestamps[^1]);
    }

    [Fact]
    public void GetHeaderInfoDateTime_WithUnixEpochEdgeCases_HandlesCorrectly()
    {
        // Arrange - Test edge cases around Unix epoch
        var timestamps = new[]
        {
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified), // Unix epoch
            new DateTime(1970, 1, 1, 0, 0, 1, DateTimeKind.Unspecified), // One second after epoch
            new DateTime(2038, 1, 19, 3, 14, 7, DateTimeKind.Unspecified) // Near Int32 limit
        };

        CreateTestTimestampFile(timestamps);

        // Act & Assert
        for (var i = 0; i < timestamps.Length; i++)
        {
            var result = UtilsListMmf.GetHeaderInfoDateTime(_testPath, i);
            result.Should().Be(timestamps[i], $"edge case timestamp at index {i}");
        }
    }

    private void CreateTestTimestampFile(DateTime[] timestamps)
    {
        // Provide a reasonable capacity for new file creation
        var capacity = Math.Max(timestamps.Length, 100);
        using var list = new ListMmf<int>(_testPath, DataType.UnixSeconds, capacity);
        foreach (var timestamp in timestamps)
        {
            list.Add(timestamp.ToUnixSeconds());
        }
    }

    private void CreateEmptyTimestampFile()
    {
        // Provide a positive capacity for new file creation
        using var list = new ListMmf<int>(_testPath, DataType.UnixSeconds, 100);
        // Don't add any data - just create the file with header
    }
}