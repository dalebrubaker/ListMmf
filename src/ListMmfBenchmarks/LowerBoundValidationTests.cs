using System.Runtime.InteropServices;
using System.Text;
using BruSoftware.ListMmf;
using Xunit;
using Xunit.Abstractions;

namespace ListMmfBenchmarks;

/// <summary>
/// Unit tests to validate that file-based LowerBound implementations return identical results
/// to the proven ListMmfTimeSeriesDateTimeSeconds implementation
/// </summary>
public class LowerBoundValidationTests
{
    private const string TestFilePath = @"C:\BruTrader21Data\Data\Future\MES\MES#\1T\Timestamps.bt";
    private const int HeaderSize = 16; // MMF header: 4 bytes version + 4 bytes dataType + 8 bytes count

    private readonly ITestOutputHelper _output;

    public LowerBoundValidationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void LowerBoundFileIO_ShouldMatchMMF_ForRandomSearches()
    {
        // Skip test if data file doesn't exist
        if (!File.Exists(TestFilePath))
        {
            _output.WriteLine($"Skipping test - file not found: {TestFilePath}");
            return;
        }

        // Setup
        using var mmfTimestamps = new ListMmfTimeSeriesDateTimeSeconds(TestFilePath, TimeSeriesOrder.Ascending);
        using var fileStream = new FileStream(TestFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Use header info to get the authoritative count (file may have extra data beyond valid count)
        var (_, _, headerCount) = UtilsListMmf.GetHeaderInfo(TestFilePath);
        var itemCount = headerCount;
        var fileCalculatedCount = (fileStream.Length - HeaderSize) / sizeof(int);
        _output.WriteLine($"Testing with {itemCount:N0} timestamps from {TestFilePath}");
        _output.WriteLine($"Header count: {itemCount:N0}, File calculated count: {fileCalculatedCount:N0}, MMF Count: {mmfTimestamps.Count:N0}");

        // Test with various search patterns
        var random = new Random(42); // Fixed seed for reproducible tests
        var testCases = new[]
        {
            // Test boundary cases and random values
            0, // First possible value  
            (int)(itemCount - 1), // Last possible value
            (int)(itemCount / 2), // Middle value
            (int)(itemCount / 4), // Quarter value
            (int)(itemCount * 3 / 4) // Three-quarter value
        };

        // Add some random indices
        var randomCases = new int[20];
        for (var i = 0; i < randomCases.Length; i++)
        {
            randomCases[i] = random.Next(0, (int)Math.Min(itemCount, int.MaxValue));
        }

        var allTestCases = new int[testCases.Length + randomCases.Length];
        Array.Copy(testCases, allTestCases, testCases.Length);
        Array.Copy(randomCases, 0, allTestCases, testCases.Length, randomCases.Length);

        var matchCount = 0;
        var totalTests = 0;

        foreach (var testIndex in allTestCases)
        {
            if (testIndex >= itemCount)
            {
                continue;
            }

            // Get the actual timestamp value to search for
            var searchTime = mmfTimestamps[testIndex];
            var searchValue = searchTime.ToUnixSeconds();

            // Test MMF implementation
            var mmfResult = mmfTimestamps.LowerBound(searchTime);

            // Test file I/O implementation
            var fileResult = LowerBoundFile(fileStream, searchValue, itemCount);

            // Test BinaryReader implementation
            var binaryReaderResult = LowerBoundBinaryReader(fileStream, searchValue, itemCount);

            // Test optimized file I/O implementation
            var optimizedResult = LowerBoundOptimized(fileStream, searchValue, itemCount);

            totalTests++;

            // Validate all implementations match
            if (mmfResult == fileResult && mmfResult == binaryReaderResult && mmfResult == optimizedResult)
            {
                matchCount++;
            }
            else
            {
                _output.WriteLine(
                    $"MISMATCH at index {testIndex}: MMF={mmfResult}, FileIO={fileResult}, BinaryReader={binaryReaderResult}, Optimized={optimizedResult}, SearchValue={searchValue}");

                // Add some context for debugging
                if (testIndex > 0)
                {
                    var prevTime = mmfTimestamps[testIndex - 1];
                    _output.WriteLine($"  Previous timestamp: {prevTime} ({prevTime.ToUnixSeconds()})");
                }
                _output.WriteLine($"  Search timestamp: {searchTime} ({searchValue})");
                if (testIndex < itemCount - 1)
                {
                    var nextTime = mmfTimestamps[testIndex + 1];
                    _output.WriteLine($"  Next timestamp: {nextTime} ({nextTime.ToUnixSeconds()})");
                }
            }

            Assert.Equal(mmfResult, fileResult);
            Assert.Equal(mmfResult, binaryReaderResult);
            Assert.Equal(mmfResult, optimizedResult);
        }

        _output.WriteLine($"Validation complete: {matchCount}/{totalTests} tests passed");
        Assert.Equal(totalTests, matchCount);
    }

    [Fact]
    public void LowerBoundFileIO_ShouldMatchMMF_ForEdgeCases()
    {
        // Skip test if data file doesn't exist
        if (!File.Exists(TestFilePath))
        {
            _output.WriteLine($"Skipping test - file not found: {TestFilePath}");
            return;
        }

        using var mmfTimestamps = new ListMmfTimeSeriesDateTimeSeconds(TestFilePath, TimeSeriesOrder.Ascending);
        using var fileStream = new FileStream(TestFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Use header info to get the authoritative count (file may have extra data beyond valid count)
        var (_, _, headerCount) = UtilsListMmf.GetHeaderInfo(TestFilePath);
        var itemCount = headerCount;

        // Test edge cases
        var firstTimestamp = mmfTimestamps[0];
        var lastTimestamp = mmfTimestamps[itemCount - 1];

        // Test cases: before first, exactly first, exactly last, after last
        var testCases = new[]
        {
            (firstTimestamp.AddSeconds(-1), "Before first"),
            (firstTimestamp, "Exactly first"),
            (lastTimestamp, "Exactly last"),
            (lastTimestamp.AddSeconds(1), "After last")
        };

        foreach (var (searchTime, description) in testCases)
        {
            var searchValue = searchTime.ToUnixSeconds();

            var mmfResult = mmfTimestamps.LowerBound(searchTime);
            var fileResult = LowerBoundFile(fileStream, searchValue, itemCount);
            var binaryReaderResult = LowerBoundBinaryReader(fileStream, searchValue, itemCount);

            _output.WriteLine($"{description}: MMF={mmfResult}, FileIO={fileResult}, BinaryReader={binaryReaderResult}");

            Assert.Equal(mmfResult, fileResult);
            Assert.Equal(mmfResult, binaryReaderResult);
        }
    }

    /// <summary>
    /// LowerBound implementation for file I/O using binary search - matches BenchmarkLowerBound
    /// </summary>
    private static long LowerBoundFile(FileStream file, int target, long count)
    {
        const int itemSize = sizeof(int);
        long first = 0;

        Span<byte> buffer = stackalloc byte[itemSize];

        while (count > 0)
        {
            var step = count / 2;
            var pos = first + step;

            // Seek to position (accounting for header)
            var fileOffset = HeaderSize + pos * itemSize;
            file.Seek(fileOffset, SeekOrigin.Begin);

            // Read value using Span for zero-copy
            file.ReadExactly(buffer);
            var value = MemoryMarshal.Read<int>(buffer);

            if (value < target)
            {
                first = pos + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }

    /// <summary>
    /// LowerBound implementation using BinaryReader - matches BenchmarkLowerBound
    /// </summary>
    private static long LowerBoundBinaryReader(FileStream file, int target, long count)
    {
        const int itemSize = sizeof(int);
        long first = 0;

        // Create a new BinaryReader to avoid interfering with other tests
        using var reader = new BinaryReader(file, Encoding.UTF8, true);

        while (count > 0)
        {
            var step = count / 2;
            var pos = first + step;

            // Seek to position (accounting for header)
            var fileOffset = HeaderSize + pos * itemSize;
            reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);

            var value = reader.ReadInt32();

            if (value < target)
            {
                first = pos + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }

    /// <summary>
    /// Optimized LowerBound implementation using larger buffer reads - matches BenchmarkLowerBound
    /// </summary>
    private static long LowerBoundOptimized(FileStream file, int target, long count)
    {
        const int itemSize = sizeof(int);
        const int bufferSize = 64 * 1024; // 64KB buffer - holds 16K integers
        const int itemsPerBuffer = bufferSize / itemSize;

        long first = 0;

        // Reusable buffer for reading chunks
        Span<byte> buffer = stackalloc byte[bufferSize];
        long cachedBufferStart = -1;
        var cachedItemCount = 0;

        while (count > 0)
        {
            var step = count / 2;
            var pos = first + step;

            // Check if we need to read a new buffer chunk
            if (pos < cachedBufferStart || pos >= cachedBufferStart + cachedItemCount)
            {
                // Calculate optimal buffer position - align to buffer boundaries when possible
                var bufferStartPos = Math.Max(0, pos - itemsPerBuffer / 2);
                bufferStartPos = Math.Min(bufferStartPos, count - 1);

                // Seek to buffer start
                var fileOffset = HeaderSize + bufferStartPos * itemSize;
                file.Seek(fileOffset, SeekOrigin.Begin);

                // Read as much as we can (up to buffer size or remaining data)
                var remainingItems = count - bufferStartPos;
                var itemsToRead = (int)Math.Min(itemsPerBuffer, remainingItems);
                var bytesToRead = itemsToRead * itemSize;

                var readBuffer = buffer.Slice(0, bytesToRead);
                var bytesRead = file.Read(readBuffer);

                cachedBufferStart = bufferStartPos;
                cachedItemCount = bytesRead / itemSize;
            }

            // Read value from cached buffer
            var bufferIndex = pos - cachedBufferStart;
            if (bufferIndex >= 0 && bufferIndex < cachedItemCount)
            {
                var byteIndex = (int)(bufferIndex * itemSize);
                var valueSpan = buffer.Slice(byteIndex, itemSize);
                var value = MemoryMarshal.Read<int>(valueSpan);

                if (value < target)
                {
                    first = pos + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }
            }
            else
            {
                // Fallback to direct read if somehow out of cached range
                var fileOffset = HeaderSize + pos * itemSize;
                file.Seek(fileOffset, SeekOrigin.Begin);

                Span<byte> singleBuffer = stackalloc byte[itemSize];
                file.ReadExactly(singleBuffer);
                var value = MemoryMarshal.Read<int>(singleBuffer);

                if (value < target)
                {
                    first = pos + 1;
                    count -= step + 1;
                }
                else
                {
                    count = step;
                }

                // Invalidate cache after direct seek
                cachedBufferStart = -1;
            }
        }

        return first;
    }
}