using System;
using System.IO;
using System.Linq;
using BruSoftware.ListMmf;
using Xunit;

namespace ListMmfTests;

/// <summary>
/// Unit tests for Span&lt;T&gt; support in ListMmf classes
/// </summary>
public class SpanSupportTests : IDisposable
{
    private readonly string _tempDirectory;

    public SpanSupportTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private string GetTempFilePath(string fileName)
    {
        return Path.Combine(_tempDirectory, fileName);
    }

    [Fact]
    public void GetRange_ValidRange_ReturnsCorrectSpan()
    {
        // Arrange
        var path = GetTempFilePath("test_getrange_valid.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        var testData = new[] { 10, 20, 30, 40, 50 };
        foreach (var item in testData)
        {
            list.Add(item);
        }

        // Act
        var span = list.GetRange(1, 3);

        // Assert
        Assert.Equal(3, span.Length);
        Assert.Equal(20, span[0]);
        Assert.Equal(30, span[1]);
        Assert.Equal(40, span[2]);
    }

    [Fact]
    public void AsSpan_Alias_ReturnsSameData()
    {
        // Arrange
        var path = GetTempFilePath("test_asspan_alias.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        var testData = new[] { 1, 2, 3, 4, 5 };
        foreach (var item in testData)
        {
            list.Add(item);
        }

        // Act
        var getRangeSpan = list.GetRange(1, 3);
        var aliasSpan = list.AsSpan(1, 3);

        // Assert
        Assert.Equal(getRangeSpan.ToArray(), aliasSpan.ToArray());
    }

    [Fact]
    public void AsSpan_InterfaceExtension_UsesImplementation()
    {
        // Arrange
        var path = GetTempFilePath("test_asspan_interface.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        var testData = new[] { 10, 20, 30, 40 };
        foreach (var item in testData)
        {
            list.Add(item);
        }

        IReadOnlyList64Mmf<int> asInterface = list;

        // Act
        var spanFromAlias = asInterface.AsSpan(0, 2);

        // Assert
        Assert.Equal(2, spanFromAlias.Length);
        Assert.Equal(10, spanFromAlias[0]);
        Assert.Equal(20, spanFromAlias[1]);
    }

    [Fact]
    public void GetRange_StartToEnd_ReturnsCorrectSpan()
    {
        // Arrange
        var path = GetTempFilePath("test_getrange_toend.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        var testData = new[] { 10, 20, 30, 40, 50 };
        foreach (var item in testData)
        {
            list.Add(item);
        }

        // Act
        var span = list.GetRange(2); // From index 2 to end

        // Assert
        Assert.Equal(3, span.Length);
        Assert.Equal(30, span[0]);
        Assert.Equal(40, span[1]);
        Assert.Equal(50, span[2]);
    }

    [Fact]
    public void GetRange_EmptyRange_ReturnsEmptySpan()
    {
        // Arrange
        var path = GetTempFilePath("test_getrange_empty.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        list.Add(10);
        list.Add(20);

        // Act
        var span = list.GetRange(1, 0);

        // Assert
        Assert.Equal(0, span.Length);
        Assert.True(span.IsEmpty);
    }

    [Fact]
    public void GetRange_InvalidStart_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var path = GetTempFilePath("test_getrange_invalidstart.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        list.Add(10);
        list.Add(20);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => list.GetRange(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.GetRange(3, 1));
    }

    [Fact]
    public void GetRange_InvalidLength_ThrowsArgumentOutOfRange()
    {
        // Arrange
        var path = GetTempFilePath("test_getrange_invalidlength.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        list.Add(10);
        list.Add(20);

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => list.GetRange(0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => list.GetRange(0, 3)); // beyond count
    }

    [Fact]
    public void AddRange_Span_AppendsCorrectly()
    {
        // Arrange
        var path = GetTempFilePath("test_addrange_span.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        list.Add(10);
        list.Add(20);

        var newData = new[] { 30, 40, 50 }.AsSpan();

        // Act
        list.AddRange(newData);

        // Assert
        Assert.Equal(5, list.Count);
        Assert.Equal(10, list[0]);
        Assert.Equal(20, list[1]);
        Assert.Equal(30, list[2]);
        Assert.Equal(40, list[3]);
        Assert.Equal(50, list[4]);
    }

    [Fact]
    public void AddRange_EmptySpan_DoesNothing()
    {
        // Arrange
        var path = GetTempFilePath("test_addrange_empty.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        list.Add(10);
        list.Add(20);

        var emptySpan = new int[0].AsSpan();

        // Act
        list.AddRange(emptySpan);

        // Assert
        Assert.Equal(2, list.Count);
        Assert.Equal(10, list[0]);
        Assert.Equal(20, list[1]);
    }

    [Fact]
    public void AddRange_LargeSpan_WorksCorrectly()
    {
        // Arrange
        var path = GetTempFilePath("test_addrange_large.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        var largeData = Enumerable.Range(1, 10000).ToArray().AsSpan();

        // Act
        list.AddRange(largeData);

        // Assert
        Assert.Equal(10000, list.Count);
        Assert.Equal(1, list[0]);
        Assert.Equal(5000, list[4999]);
        Assert.Equal(10000, list[9999]);
    }

    [Fact]
    public void TimeSeries_GetRange_ReturnsDateTimeSpan()
    {
        // Arrange
        var path = GetTempFilePath("test_timeseries_getrange.mmf");
        using var timeSeries = new ListMmfTimeSeriesDateTime(path, TimeSeriesOrder.Ascending);

        var testDates = new[]
        {
            new DateTime(2023, 1, 1, 10, 0, 0),
            new DateTime(2023, 1, 1, 11, 0, 0),
            new DateTime(2023, 1, 1, 12, 0, 0),
            new DateTime(2023, 1, 1, 13, 0, 0)
        };

        foreach (var date in testDates)
        {
            timeSeries.Add(date);
        }

        // Act
        var span = timeSeries.GetRange(1, 2);

        // Assert
        Assert.Equal(2, span.Length);
        Assert.Equal(testDates[1], span[0]);
        Assert.Equal(testDates[2], span[1]);
    }

    [Fact]
    public void TimeSeries_AddRange_Span_AppendsWithValidation()
    {
        // Arrange
        var path = GetTempFilePath("test_timeseries_addrange.mmf");
        using var timeSeries = new ListMmfTimeSeriesDateTime(path, TimeSeriesOrder.Ascending);

        timeSeries.Add(new DateTime(2023, 1, 1, 10, 0, 0));

        var newDates = new[]
        {
            new DateTime(2023, 1, 1, 11, 0, 0),
            new DateTime(2023, 1, 1, 12, 0, 0),
            new DateTime(2023, 1, 1, 13, 0, 0)
        }.AsSpan();

        // Act
        timeSeries.AddRange(newDates);

        // Assert
        Assert.Equal(4, timeSeries.Count);
        Assert.Equal(new DateTime(2023, 1, 1, 10, 0, 0), timeSeries[0]);
        Assert.Equal(new DateTime(2023, 1, 1, 11, 0, 0), timeSeries[1]);
        Assert.Equal(new DateTime(2023, 1, 1, 12, 0, 0), timeSeries[2]);
        Assert.Equal(new DateTime(2023, 1, 1, 13, 0, 0), timeSeries[3]);
    }

    [Fact]
    public void TimeSeries_AddRange_OutOfOrder_ThrowsException()
    {
        // Arrange
        var path = GetTempFilePath("test_timeseries_outoforder.mmf");
        using var timeSeries = new ListMmfTimeSeriesDateTime(path, TimeSeriesOrder.Ascending);

        timeSeries.Add(new DateTime(2023, 1, 1, 12, 0, 0));

        var outOfOrderDates = new[]
        {
            new DateTime(2023, 1, 1, 11, 0, 0), // This is earlier than existing
            new DateTime(2023, 1, 1, 13, 0, 0)
        };

        // Act & Assert
        Assert.Throws<OutOfOrderException>(() => timeSeries.AddRange(outOfOrderDates.AsSpan()));
    }

    [Fact]
    public void BitArray_GetRange_ReturnsCorrectBoolSpan()
    {
        // Arrange
        var path = GetTempFilePath("test_bitarray_getrange.mmf");
        using var bitArray = new ListMmfBitArray(path);

        bitArray.Add(true);
        bitArray.Add(false);
        bitArray.Add(true);
        bitArray.Add(false);

        // Act
        var span = bitArray.GetRange(1, 2);

        // Assert
        Assert.Equal(2, span.Length);
        Assert.False(span[0]);
        Assert.True(span[1]);
    }

    [Fact]
    public void BitArray_GetRange_StartToEnd_ReturnsCorrectSpan()
    {
        // Arrange
        var path = GetTempFilePath("test_bitarray_getrange_toend.mmf");
        using var bitArray = new ListMmfBitArray(path);

        bitArray.Add(true);
        bitArray.Add(false);
        bitArray.Add(true);
        bitArray.Add(false);

        // Act
        var span = bitArray.GetRange(2); // From index 2 to end

        // Assert
        Assert.Equal(2, span.Length);
        Assert.True(span[0]);
        Assert.False(span[1]);
    }

    [Fact]
    public void BitArray_AddRange_Span_AppendsCorrectly()
    {
        // Arrange
        var path = GetTempFilePath("test_bitarray_addrange.mmf");
        using var bitArray = new ListMmfBitArray(path);

        bitArray.Add(true);
        bitArray.Add(false);

        var boolData = new[] { true, false, true };

        // Act
        bitArray.AddRange(boolData.AsSpan());

        // Assert
        Assert.Equal(5, bitArray.Length);
        Assert.True(bitArray[0]);
        Assert.False(bitArray[1]);
        Assert.True(bitArray[2]);
        Assert.False(bitArray[3]);
        Assert.True(bitArray[4]);
    }

    [Fact]
    public void GetRange_SpanToArray_CreatesCorrectArray()
    {
        // Arrange
        var path = GetTempFilePath("test_getrange_toarray.mmf");
        using var list = new ListMmf<int>(path, DataType.Int32);

        var testData = new[] { 100, 200, 300, 400 };
        foreach (var item in testData)
        {
            list.Add(item);
        }

        // Act
        var span = list.GetRange(1, 2);
        var array = span.ToArray();

        // Assert
        Assert.Equal(2, array.Length);
        Assert.Equal(200, array[0]);
        Assert.Equal(300, array[1]);
    }
}