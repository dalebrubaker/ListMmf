using System;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

/// <summary>
/// Comprehensive tests for Unix seconds conversion extensions in UtilsListMmf
/// These test the ToUnixSeconds() and FromUnixSecondsToDateTime() methods
/// </summary>
public class UnixSecondsConversionTests
{
    private static readonly DateTime UnixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    [Fact]
    public void ToUnixSeconds_WithUnixEpoch_ReturnsZero()
    {
        // Arrange
        var unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var result = unixEpoch.ToUnixSeconds();

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void FromUnixSecondsToDateTime_WithZero_ReturnsUnixEpoch()
    {
        // Arrange
        const int zero = 0;

        // Act
        var result = zero.FromUnixSecondsToDateTime();

        // Assert
        result.Should().Be(UnixEpoch);
    }

    [Fact]
    public void ToUnixSeconds_WithDateTimeMinValue_ReturnsIntMinValue()
    {
        // Arrange
        var minValue = DateTime.MinValue;

        // Act
        var result = minValue.ToUnixSeconds();

        // Assert
        result.Should().Be(int.MinValue);
    }

    [Fact]
    public void FromUnixSecondsToDateTime_WithIntMinValue_ReturnsDateTimeMinValue()
    {
        // Arrange
        const int intMinValue = int.MinValue;

        // Act
        var result = intMinValue.FromUnixSecondsToDateTime();

        // Assert
        result.Should().Be(DateTime.MinValue);
    }

    [Fact]
    public void ToUnixSeconds_WithIntMaxValueLimit_ReturnsIntMaxValue()
    {
        // Arrange - DateTime that would exceed int.MaxValue when converted
        var futureDate = new DateTime(2040, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var result = futureDate.ToUnixSeconds();

        // Assert
        result.Should().Be(int.MaxValue, "dates beyond int.MaxValue limit should be clamped to int.MaxValue");
    }

    [Fact]
    public void FromUnixSecondsToDateTime_WithIntMaxValue_Returns2038Date_NOT_DateTimeMaxValue()
    {
        // Arrange
        const int intMaxValue = int.MaxValue; // 2147483647

        // Act
        var result = intMaxValue.FromUnixSecondsToDateTime();

        // Assert - This test exposes the bug in the current implementation
        // int.MaxValue as Unix seconds should be 2038-01-19 03:14:07, NOT DateTime.MaxValue (9999-12-31)
        var expected2038Date = new DateTime(2038, 1, 19, 3, 14, 7, DateTimeKind.Unspecified);
        result.Should().Be(expected2038Date, "int.MaxValue Unix seconds should convert to the actual 2038 date, not DateTime.MaxValue");
        
        // The current implementation incorrectly returns DateTime.MaxValue
        result.Should().NotBe(DateTime.MaxValue, "int.MaxValue should NOT return DateTime.MaxValue - this is a bug");
    }

    [Fact]
    public void ToUnixSeconds_WithYear2038Limit_HandlesCorrectly()
    {
        // Arrange - Test the exact 2038 limit (int.MaxValue seconds = 2147483647)
        // This is 2038-01-19 03:14:07 UTC
        var year2038Limit = new DateTime(2038, 1, 19, 3, 14, 7, DateTimeKind.Unspecified);

        // Act
        var result = year2038Limit.ToUnixSeconds();

        // Assert
        result.Should().Be(int.MaxValue, "2038-01-19 03:14:07 should be at the int.MaxValue limit");
    }

    [Fact]
    public void ToUnixSeconds_WithDateBeforeUnixEpoch_ReturnsNegativeValue()
    {
        // Arrange
        var preEpochDate = new DateTime(1969, 12, 31, 23, 59, 0, DateTimeKind.Unspecified);

        // Act
        var result = preEpochDate.ToUnixSeconds();

        // Assert
        result.Should().Be(-60, "one minute before epoch should be -60 seconds");
    }

    [Fact]
    public void FromUnixSecondsToDateTime_WithNegativeValue_ReturnsDateBeforeEpoch()
    {
        // Arrange
        const int negativeSeconds = -3600; // One hour before epoch

        // Act
        var result = negativeSeconds.FromUnixSecondsToDateTime();

        // Assert
        var expectedDate = new DateTime(1969, 12, 31, 23, 0, 0, DateTimeKind.Unspecified);
        result.Should().Be(expectedDate);
    }

    [Theory]
    [InlineData(1, 1970, 1, 1, 0, 0, 1)]
    [InlineData(3600, 1970, 1, 1, 1, 0, 0)]
    [InlineData(86400, 1970, 1, 2, 0, 0, 0)]
    [InlineData(31536000, 1971, 1, 1, 0, 0, 0)]
    [InlineData(946684800, 2000, 1, 1, 0, 0, 0)]
    [InlineData(1577836800, 2020, 1, 1, 0, 0, 0)]
    public void FromUnixSecondsToDateTime_WithKnownValues_ReturnsCorrectDateTime(
        int unixSeconds, int year, int month, int day, int hour, int minute, int second)
    {
        // Arrange
        var expectedDateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

        // Act
        var result = unixSeconds.FromUnixSecondsToDateTime();

        // Assert
        result.Should().Be(expectedDateTime);
    }

    [Theory]
    [InlineData(1970, 1, 1, 0, 0, 1, 1)]
    [InlineData(1970, 1, 1, 1, 0, 0, 3600)]
    [InlineData(1970, 1, 2, 0, 0, 0, 86400)]
    [InlineData(1971, 1, 1, 0, 0, 0, 31536000)]
    [InlineData(2000, 1, 1, 0, 0, 0, 946684800)]
    [InlineData(2020, 1, 1, 0, 0, 0, 1577836800)]
    public void ToUnixSeconds_WithKnownValues_ReturnsCorrectUnixSeconds(
        int year, int month, int day, int hour, int minute, int second, int expectedUnixSeconds)
    {
        // Arrange
        var dateTime = new DateTime(year, month, day, hour, minute, second, DateTimeKind.Unspecified);

        // Act
        var result = dateTime.ToUnixSeconds();

        // Assert
        result.Should().Be(expectedUnixSeconds);
    }

    [Fact]
    public void RoundTripConversion_WithValidDates_PreservesAccuracy()
    {
        // Arrange - Test various dates within the valid range
        var testDates = new[]
        {
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
            new DateTime(1990, 6, 15, 12, 30, 45, DateTimeKind.Unspecified),
            new DateTime(2000, 12, 31, 23, 59, 59, DateTimeKind.Unspecified),
            new DateTime(2020, 3, 15, 9, 30, 0, DateTimeKind.Unspecified),
            new DateTime(2037, 12, 31, 23, 59, 59, DateTimeKind.Unspecified)
        };

        foreach (var originalDate in testDates)
        {
            // Act
            var unixSeconds = originalDate.ToUnixSeconds();
            var roundTripDate = unixSeconds.FromUnixSecondsToDateTime();

            // Assert
            roundTripDate.Should().Be(originalDate, $"round-trip conversion should preserve {originalDate}");
        }
    }

    [Fact]
    public void RoundTripConversion_WithUnixSecondsValues_PreservesAccuracy()
    {
        // Arrange - Test various Unix seconds values
        var testValues = new[] { 0, 1, 3600, 86400, 946684800, 1577836800, 1234567890 };

        foreach (var originalUnixSeconds in testValues)
        {
            // Act
            var dateTime = originalUnixSeconds.FromUnixSecondsToDateTime();
            var roundTripUnixSeconds = dateTime.ToUnixSeconds();

            // Assert
            roundTripUnixSeconds.Should().Be(originalUnixSeconds, $"round-trip conversion should preserve {originalUnixSeconds}");
        }
    }

    [Fact]
    public void ToUnixSeconds_WithSubSecondPrecision_TruncatesSeconds()
    {
        // Arrange - Date with milliseconds
        var dateWithMilliseconds = new DateTime(2020, 1, 1, 12, 0, 0, 500, DateTimeKind.Unspecified);
        var expectedDate = new DateTime(2020, 1, 1, 12, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var unixSeconds = dateWithMilliseconds.ToUnixSeconds();
        var resultDate = unixSeconds.FromUnixSecondsToDateTime();

        // Assert
        resultDate.Should().Be(expectedDate, "sub-second precision should be truncated");
    }

    [Fact]
    public void ToUnixSeconds_WithVeryOldDate_ReturnsIntMinValue()
    {
        // Arrange - Date way before Unix epoch that would underflow
        var veryOldDate = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        // Act
        var result = veryOldDate.ToUnixSeconds();

        // Assert
        result.Should().Be(int.MinValue, "dates that would underflow should be clamped to int.MinValue");
    }

    [Fact]
    public void ConversionLimits_Match2038Problem_Correctly()
    {
        // Arrange - Test the exact boundaries of the 2038 problem
        var maxSafeDate = new DateTime(2038, 1, 19, 3, 14, 6, DateTimeKind.Unspecified); // One second before limit
        var limitDate = new DateTime(2038, 1, 19, 3, 14, 7, DateTimeKind.Unspecified);   // Exact limit
        var beyondLimitDate = new DateTime(2038, 1, 19, 3, 14, 8, DateTimeKind.Unspecified); // One second beyond

        // Act
        var maxSafeUnix = maxSafeDate.ToUnixSeconds();
        var limitUnix = limitDate.ToUnixSeconds();
        var beyondLimitUnix = beyondLimitDate.ToUnixSeconds();

        // Assert
        maxSafeUnix.Should().Be(int.MaxValue - 1);
        limitUnix.Should().Be(int.MaxValue);
        beyondLimitUnix.Should().Be(int.MaxValue, "dates beyond the limit should be clamped to int.MaxValue");
    }

    [Fact]
    public void FromUnixSecondsToDateTime_WithTypicalTradingDates_WorksCorrectly()
    {
        // Arrange - Test dates commonly used in trading applications
        var tradingDates = new[]
        {
            (946684800, new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)),   // Y2K
            (1609459200, new DateTime(2021, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)),  // Recent year
            (1640995200, new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)),  // Another recent year
            (1577836800, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Unspecified))   // Covid year
        };

        foreach (var (unixSeconds, expectedDate) in tradingDates)
        {
            // Act
            var result = unixSeconds.FromUnixSecondsToDateTime();

            // Assert
            result.Should().Be(expectedDate, $"Unix seconds {unixSeconds} should convert to {expectedDate}");
        }
    }

    [Fact]
    public void ToUnixSeconds_WithMarketHours_ConvertsProperly()
    {
        // Arrange - Test typical market trading hours
        var marketOpen = new DateTime(2023, 6, 15, 9, 30, 0, DateTimeKind.Unspecified);  // 9:30 AM
        var marketClose = new DateTime(2023, 6, 15, 16, 0, 0, DateTimeKind.Unspecified); // 4:00 PM

        // Act
        var openUnix = marketOpen.ToUnixSeconds();
        var closeUnix = marketClose.ToUnixSeconds();

        // Assert
        var timeDifference = closeUnix - openUnix;
        timeDifference.Should().Be((int)(6.5 * 3600), "market session should be 6.5 hours = 23400 seconds");

        // Verify round-trip
        openUnix.FromUnixSecondsToDateTime().Should().Be(marketOpen);
        closeUnix.FromUnixSecondsToDateTime().Should().Be(marketClose);
    }
}