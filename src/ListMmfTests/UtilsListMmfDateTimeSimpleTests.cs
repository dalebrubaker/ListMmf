using System;
using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

public class UtilsListMmfDateTimeSimpleTests
{
    [Fact]
    public void GetHeaderInfoDateTime_WithValidTimestampFile_ReturnsCorrectDateTime()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var testPath = Path.Combine(tempDir, "Timestamps.bt");
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Unspecified);

        try
        {
            // Create a simple timestamp file with one entry
            {
                using var list = new ListMmf<int>(testPath, DataType.UnixSeconds);
                list.Add(testDate.ToUnixSeconds());
            } // Dispose to ensure file is written

            // Act
            var result = UtilsListMmf.GetHeaderInfoDateTime(testPath, 0L);

            // Assert
            result.Should().Be(testDate);
        }
        finally
        {
            if (File.Exists(testPath))
            {
                File.Delete(testPath);
            }
        }
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
    public void GetHeaderInfoDateTime_WithIndexOutOfRange_ThrowsException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var testPath = Path.Combine(tempDir, "Timestamps.bt");
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Unspecified);

        try
        {
            {
                using var list = new ListMmf<int>(testPath, DataType.UnixSeconds);
                list.Add(testDate.ToUnixSeconds());
            }

            // Act & Assert
            var action = () => UtilsListMmf.GetHeaderInfoDateTime(testPath, 1L); // Index 1 when only 0 exists
            action.Should().Throw<ListMmfException>()
                .WithMessage("*Index 1 is out of range*");
        }
        finally
        {
            if (File.Exists(testPath))
            {
                File.Delete(testPath);
            }
        }
    }

    [Fact]
    public void GetHeaderInfoDateTime_WithWrongFileName_ThrowsException()
    {
        // Arrange
        var tempDir = Path.GetTempPath();
        var testPath = Path.Combine(tempDir, "WrongName.bt");
        var testDate = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Unspecified);

        try
        {
            {
                using var list = new ListMmf<int>(testPath, DataType.UnixSeconds);
                list.Add(testDate.ToUnixSeconds());
            }

            // Act & Assert
            var action = () => UtilsListMmf.GetHeaderInfoDateTime(testPath, 0L);
            action.Should().Throw<ListMmfException>()
                .WithMessage("*File must be named 'Timestamps.bt'*");
        }
        finally
        {
            if (File.Exists(testPath))
            {
                File.Delete(testPath);
            }
        }
    }
}