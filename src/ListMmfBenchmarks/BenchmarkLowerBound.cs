using System.Runtime.InteropServices;
using System.Text;
using BenchmarkDotNet.Attributes;
using BruSoftware.ListMmf;

namespace ListMmfBenchmarks;

/// <summary>
/// Benchmark LowerBound binary search performance: Memory-mapped files vs direct file I/O
/// Tests the hypothesis that binary search on files may be nearly as fast as MMF due to:
/// - Only ~log2(n) seeks required
/// - Modern SSD random access performance
/// - OS file caching
/// - Span&lt;T&gt; zero-copy operations
/// </summary>
public class BenchmarkLowerBound
{
    private const string TestFilePath = @"C:\BruTrader21Data\Data\Future\MES\MES#\1T\Timestamps.bt";
    private const int HeaderSize = 16; // MMF header: 4 bytes version + 4 bytes dataType + 8 bytes count

    private ListMmfTimeSeriesDateTimeSeconds _mmfTimestamps;
    private FileStream _fileStream;
    private int[] _searchValues;
    private long _itemCount;

    [Params(1000)] // Number of searches to perform
    public int NumSearches { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (!File.Exists(TestFilePath))
        {
            throw new FileNotFoundException($"Test file not found: {TestFilePath}");
        }

        // Setup MMF version
        _mmfTimestamps = new ListMmfTimeSeriesDateTimeSeconds(TestFilePath, TimeSeriesOrder.Ascending);

        // Setup file I/O version
        _fileStream = new FileStream(TestFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // Use header info to get the authoritative count (not file size calculation)
        var (_, _, headerCount) = UtilsListMmf.GetHeaderInfo(TestFilePath);
        _itemCount = headerCount;

        Console.WriteLine($"File: {TestFilePath}");
        Console.WriteLine($"File size: {_fileStream.Length:N0} bytes ({_fileStream.Length / 1024.0 / 1024.0:F1} MB)");
        Console.WriteLine($"Item count: {_itemCount:N0} timestamps");
        Console.WriteLine($"Expected seeks per search: ~{Math.Log2(_itemCount):F1}");

        // Generate test search values - sample from actual data for realistic searches
        var random = new Random(42);
        _searchValues = new int[NumSearches];

        for (var i = 0; i < NumSearches; i++)
        {
            // Pick a random timestamp from the file to search for
            var randomIndex = random.Next(0, (int)Math.Min(_itemCount, int.MaxValue));
            var timestamp = _mmfTimestamps[randomIndex];
            _searchValues[i] = timestamp.ToUnixSeconds();
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _mmfTimestamps?.Dispose();
        _fileStream?.Dispose();
    }

    /// <summary>
    /// LowerBound using existing ListMmfTimeSeriesDateTimeSeconds (memory-mapped)
    /// </summary>
    [Benchmark(Baseline = true)]
    public long LowerBoundMMF()
    {
        long totalIndex = 0;

        for (var i = 0; i < _searchValues.Length; i++)
        {
            var searchTime = _searchValues[i].FromUnixSecondsToDateTime();
            var index = _mmfTimestamps.LowerBound(searchTime);
            totalIndex += index;
        }

        return totalIndex;
    }

    /// <summary>
    /// LowerBound using direct file I/O with Span&lt;T&gt; for zero-copy reads
    /// </summary>
    [Benchmark]
    public long LowerBoundFileIO()
    {
        long totalIndex = 0;

        for (var i = 0; i < _searchValues.Length; i++)
        {
            var searchValue = _searchValues[i];
            var index = LowerBoundFile(_fileStream, searchValue, _itemCount);
            totalIndex += index;
        }

        return totalIndex;
    }

    /// <summary>
    /// LowerBound implementation for file I/O using binary search
    /// Equivalent to std::lower_bound - finds first element not less than target
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
    /// Alternative implementation using BinaryReader for comparison
    /// (Should be slightly slower due to extra buffering)
    /// </summary>
    [Benchmark]
    public long LowerBoundBinaryReader()
    {
        long totalIndex = 0;
        using var reader = new BinaryReader(_fileStream, Encoding.UTF8, true);

        for (var i = 0; i < _searchValues.Length; i++)
        {
            var searchValue = _searchValues[i];
            var index = LowerBoundBinaryReader(reader, searchValue, _itemCount);
            totalIndex += index;
        }

        return totalIndex;
    }

    /// <summary>
    /// Optimized file I/O using larger buffered reads to minimize seeks
    /// This should be much closer to MMF performance
    /// </summary>
    [Benchmark]
    public long LowerBoundOptimizedFileIO()
    {
        long totalIndex = 0;

        for (var i = 0; i < _searchValues.Length; i++)
        {
            var searchValue = _searchValues[i];
            var index = LowerBoundOptimized(_fileStream, searchValue, _itemCount);
            totalIndex += index;
        }

        return totalIndex;
    }

    /// <summary>
    /// Optimized implementation that uses larger buffer reads and caching to minimize seeks
    /// Strategy: Read chunks of data and cache them, only seeking when necessary
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

    private static long LowerBoundBinaryReader(BinaryReader reader, int target, long count)
    {
        const int itemSize = sizeof(int);
        long first = 0;

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
}