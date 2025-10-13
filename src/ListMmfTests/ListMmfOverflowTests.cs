using System;
using System.Collections.Generic;
using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

/// <summary>
/// Tests for overflow protection in ListMmf write operations.
///
/// IMPORTANT: C# generics cannot detect overflow at runtime within the library.
/// The protection happens at the CALLER'S cast site when using checked context.
///
/// These tests demonstrate that:
/// 1. Compiler prevents implicit conversions that would overflow
/// 2. checked() casts throw OverflowException at the cast site
/// 3. unchecked() casts silently truncate (dangerous - should be avoided)
///
/// Production code should either:
/// - Use appropriately-sized types (int, long) to avoid overflow
/// - Use checked context when casting to smaller types
/// - Enable project-level checked arithmetic (/checked+ compiler flag)
/// </summary>
public class ListMmfOverflowTests
{
    private static void CleanupFile(string fileName)
    {
        try
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            var lockFile = fileName + UtilsListMmf.LockFileExtension;
            if (File.Exists(lockFile))
            {
                File.Delete(lockFile);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Add_WithInt16_WhenValueExceedsMaxValue_CheckedCastThrowsOverflowException()
    {
        // Arrange
        const string fileName = nameof(Add_WithInt16_WhenValueExceedsMaxValue_CheckedCastThrowsOverflowException);
        CleanupFile(fileName);

        using var list = new ListMmf<short>(fileName, DataType.Int16);

        // Act & Assert
        list.Add(30_000);  // Should succeed (within range)
        list[0].Should().Be(30_000);

        // In production code without explicit cast, this wouldn't compile:
        // list.Add(40_000);  // Compiler error: cannot convert from 'int' to 'short'

        // With checked cast, this throws OverflowException at the cast site (before Add)
        int tooBig = 40_000;
        Action act = () => list.Add(checked((short)tooBig));
        act.Should().Throw<OverflowException>()
            .WithMessage("*overflow*");

        // Verify no data corruption - count unchanged
        list.Count.Should().Be(1);
        list[0].Should().Be(30_000);

        CleanupFile(fileName);
    }

    [Fact]
    public void Add_WithInt16_WhenValueBelowMinValue_CheckedCastThrowsOverflowException()
    {
        // Arrange
        const string fileName = nameof(Add_WithInt16_WhenValueBelowMinValue_CheckedCastThrowsOverflowException);
        CleanupFile(fileName);

        using var list = new ListMmf<short>(fileName, DataType.Int16);

        // Act & Assert
        list.Add(-30_000);  // Should succeed (within range)
        list[0].Should().Be(-30_000);

        // checked cast throws when value below short.MinValue (-32,768)
        int tooSmall = -40_000;
        Action act = () => list.Add(checked((short)tooSmall));
        act.Should().Throw<OverflowException>();

        list.Count.Should().Be(1);
        list[0].Should().Be(-30_000);

        CleanupFile(fileName);
    }

    [Fact]
    public void Add_WithInt32_WhenValueExceedsMaxValue_CheckedCastThrowsOverflowException()
    {
        // Arrange
        const string fileName = nameof(Add_WithInt32_WhenValueExceedsMaxValue_CheckedCastThrowsOverflowException);
        CleanupFile(fileName);

        using var list = new ListMmf<int>(fileName, DataType.Int32);

        // Act & Assert
        list.Add(2_000_000_000);  // Should succeed
        list[0].Should().Be(2_000_000_000);

        // This exceeds int.MaxValue (2,147,483,647)
        long tooBig = 3_000_000_000L;
        Action act = () => list.Add(checked((int)tooBig));
        act.Should().Throw<OverflowException>();

        list.Count.Should().Be(1);

        CleanupFile(fileName);
    }

    [Fact]
    public void Add_WithByte_WhenValueExceedsMaxValue_CheckedCastThrowsOverflowException()
    {
        // Arrange
        const string fileName = nameof(Add_WithByte_WhenValueExceedsMaxValue_CheckedCastThrowsOverflowException);
        CleanupFile(fileName);

        using var list = new ListMmf<byte>(fileName, DataType.Byte);

        // Act & Assert
        list.Add(200);  // Should succeed (within 0-255)
        list[0].Should().Be(200);

        // Exceeds byte.MaxValue (255)
        int tooBig = 300;
        Action act = () => list.Add(checked((byte)tooBig));
        act.Should().Throw<OverflowException>();

        list.Count.Should().Be(1);
        list[0].Should().Be(200);

        CleanupFile(fileName);
    }

    [Fact]
    public void Add_WithUInt16_WhenNegativeValue_CheckedCastThrowsOverflowException()
    {
        // Arrange
        const string fileName = nameof(Add_WithUInt16_WhenNegativeValue_CheckedCastThrowsOverflowException);
        CleanupFile(fileName);

        using var list = new ListMmf<ushort>(fileName, DataType.UInt16);

        // Act & Assert
        list.Add(30_000);  // Should succeed
        list[0].Should().Be(30_000);

        // Negative value overflows ushort (range: 0 to 65,535)
        int negative = -100;
        Action act = () => list.Add(checked((ushort)negative));
        act.Should().Throw<OverflowException>();

        list.Count.Should().Be(1);

        CleanupFile(fileName);
    }

    [Fact]
    public void Add_WithInt64_NeverOverflows_WhenSourceIsAlsoInt64()
    {
        // Arrange
        const string fileName = nameof(Add_WithInt64_NeverOverflows_WhenSourceIsAlsoInt64);
        CleanupFile(fileName);

        using var list = new ListMmf<long>(fileName, DataType.Int64);

        // Act & Assert - long to long never overflows
        list.Add(long.MaxValue);
        list.Add(long.MinValue);
        list.Add(0);

        list.Count.Should().Be(3);
        list[0].Should().Be(long.MaxValue);
        list[1].Should().Be(long.MinValue);
        list[2].Should().Be(0);

        CleanupFile(fileName);
    }

    [Fact]
    public void SetLast_WithInt16_WhenValueExceedsMaxValue_CheckedCastThrowsOverflowException()
    {
        // Arrange
        const string fileName = nameof(SetLast_WithInt16_WhenValueExceedsMaxValue_CheckedCastThrowsOverflowException);
        CleanupFile(fileName);

        using var list = new ListMmf<short>(fileName, DataType.Int16);

        // Add some values
        list.Add(100);
        list.Add(200);
        list.Add(300);
        list.Count.Should().Be(3);

        // Act & Assert - SetLast with valid value
        list.SetLast(999);
        list[2].Should().Be(999);

        // SetLast with overflow value throws at cast site
        int tooBig = 50_000;
        Action act = () => list.SetLast(checked((short)tooBig));
        act.Should().Throw<OverflowException>();

        // Verify last value wasn't corrupted
        list[2].Should().Be(999);
        list.Count.Should().Be(3);

        CleanupFile(fileName);
    }

    [Fact]
    public void AddRange_WithLoop_WhenValueExceedsRange_CheckedCastThrowsOverflowException()
    {
        // Arrange
        const string fileName = nameof(AddRange_WithLoop_WhenValueExceedsRange_CheckedCastThrowsOverflowException);
        CleanupFile(fileName);

        using var list = new ListMmf<short>(fileName, DataType.Int16);

        // Add initial values
        list.Add(100);
        list.Add(200);
        list.Count.Should().Be(2);

        // Act & Assert - Loop that encounters overflow
        var values = new[] { 300, 50_000, 400 };
        Action act = () =>
        {
            foreach (var val in values)
            {
                list.Add(checked((short)val));  // Throws on second item
            }
        };
        act.Should().Throw<OverflowException>();

        // Should have added first value before overflow
        list.Count.Should().Be(3);
        list[2].Should().Be(300);

        CleanupFile(fileName);
    }

    [Fact]
    public void RealWorldScenario_PriceData_CheckedCastPreventsCorruption()
    {
        // Arrange - Simulate real-world price storage
        const string fileName = nameof(RealWorldScenario_PriceData_CheckedCastPreventsCorruption);
        CleanupFile(fileName);

        // Using Int16 for prices in cents (can hold ±32,767 = $327.67 max)
        using var prices = new ListMmf<short>(fileName, DataType.Int16);

        // Act & Assert - Normal trading prices work fine
        prices.Add(10050);  // $100.50
        prices.Add(15025);  // $150.25
        prices.Add(20099);  // $200.99

        prices.Count.Should().Be(3);
        prices[0].Should().Be(10050);

        // Simulate bad data feed: $500.00 = 50,000 cents (exceeds Int16.MaxValue)
        // With checked cast, this throws instead of corrupting data
        int badPrice = 50_000;
        Action act = () => prices.Add(checked((short)badPrice));
        act.Should().Throw<OverflowException>();

        // Verify data integrity - no corruption occurred
        prices.Count.Should().Be(3);
        prices[2].Should().Be(20099);  // Last good value preserved

        CleanupFile(fileName);
    }

    [Fact]
    public void UncheckedCast_SilentlyTruncates_DemonstratingDanger()
    {
        // Arrange
        const string fileName = nameof(UncheckedCast_SilentlyTruncates_DemonstratingDanger);
        CleanupFile(fileName);

        using var list = new ListMmf<short>(fileName, DataType.Int16);

        // Act - unchecked cast silently wraps
        list.Add(unchecked((short)50_000));  // Wraps to -15536

        // Assert - data is CORRUPTED (this is why unchecked is dangerous!)
        list.Count.Should().Be(1);
        list[0].Should().Be(-15536);  // NOT 50,000!

        CleanupFile(fileName);
    }

    [Fact]
    public void ProductionRecommendation_UseAppropriateSizedTypes()
    {
        // Arrange - Use Int32 for prices to avoid overflow
        const string fileName = nameof(ProductionRecommendation_UseAppropriateSizedTypes);
        CleanupFile(fileName);

        // Int32 can hold ±2.1 billion ($21M per share if storing cents)
        using var prices = new ListMmf<int>(fileName, DataType.Int32);

        // Act - No casting needed, no overflow risk
        prices.Add(10050);      // $100.50
        prices.Add(5_000_000);  // $50,000.00 (extreme but valid)
        prices.Add(2_000_000_000);  // $20M (still fits)

        // Assert - All values stored correctly
        prices.Count.Should().Be(3);
        prices[0].Should().Be(10050);
        prices[1].Should().Be(5_000_000);
        prices[2].Should().Be(2_000_000_000);

        CleanupFile(fileName);
    }
}
