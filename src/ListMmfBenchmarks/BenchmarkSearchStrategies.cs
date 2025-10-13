using BenchmarkDotNet.Attributes;
using BruSoftware.ListMmf;

namespace ListMmfBenchmarks;

/// <summary>
/// Benchmark comparing Binary, Interpolation, and Auto search strategies for LowerBound, UpperBound, and BinarySearch.
/// Tests with both uniform data (daily trades) and non-uniform data to validate the Auto strategy's effectiveness.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkSearchStrategies
{
    private string _uniformFilePath;
    private string _nonUniformFilePath;
    private ListMmfTimeSeriesDateTimeSeconds _uniformTimestamps;
    private ListMmfTimeSeriesDateTimeSeconds _nonUniformTimestamps;
    private DateTime[] _searchValues;

    [Params(1_000_000, 10_000_000, 100_000_000)] // Test different file sizes
    public int ItemCount { get; set; }

    [Params(1000)] // Number of searches to perform
    public int NumSearches { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ListMmfBenchmarks");
        Directory.CreateDirectory(tempDir);

        // Create uniform data file (simulates daily trades with consistent intervals)
        _uniformFilePath = Path.Combine(tempDir, $"uniform_{ItemCount}.bt");
        CreateUniformDataFile(_uniformFilePath, ItemCount);
        _uniformTimestamps = new ListMmfTimeSeriesDateTimeSeconds(_uniformFilePath, TimeSeriesOrder.Ascending);

        // Create non-uniform data file (simulates irregular events)
        _nonUniformFilePath = Path.Combine(tempDir, $"nonuniform_{ItemCount}.bt");
        CreateNonUniformDataFile(_nonUniformFilePath, ItemCount);
        _nonUniformTimestamps = new ListMmfTimeSeriesDateTimeSeconds(_nonUniformFilePath, TimeSeriesOrder.Ascending);

        // Generate test search values - sample from actual data for realistic searches
        var random = new Random(42);
        _searchValues = new DateTime[NumSearches];

        for (var i = 0; i < NumSearches; i++)
        {
            // Pick random timestamps from the uniform file to search for
            var randomIndex = random.Next(0, (int)Math.Min(ItemCount, int.MaxValue));
            _searchValues[i] = _uniformTimestamps[randomIndex];
        }

        Console.WriteLine($"Uniform file: {_uniformFilePath}");
        Console.WriteLine($"Non-uniform file: {_nonUniformFilePath}");
        Console.WriteLine($"Item count: {ItemCount:N0} timestamps");
        Console.WriteLine($"Expected seeks (binary): ~{Math.Log2(ItemCount):F1}");
        Console.WriteLine($"Expected seeks (interpolation): ~{Math.Log2(Math.Log2(ItemCount)):F1}");
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _uniformTimestamps?.Dispose();
        _nonUniformTimestamps?.Dispose();

        try
        {
            if (File.Exists(_uniformFilePath))
            {
                File.Delete(_uniformFilePath);
            }
            if (File.Exists(_nonUniformFilePath))
            {
                File.Delete(_nonUniformFilePath);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    // ========== UNIFORM DATA BENCHMARKS (should favor Interpolation) ==========

    [Benchmark(Description = "Uniform-LowerBound-Binary")]
    public long UniformLowerBoundBinary()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _uniformTimestamps.LowerBound(_searchValues[i], SearchStrategy.Binary);
        }
        return totalIndex;
    }

    [Benchmark(Description = "Uniform-LowerBound-Interpolation")]
    public long UniformLowerBoundInterpolation()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _uniformTimestamps.LowerBound(_searchValues[i], SearchStrategy.Interpolation);
        }
        return totalIndex;
    }

    [Benchmark(Description = "Uniform-LowerBound-Auto", Baseline = true)]
    public long UniformLowerBoundAuto()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _uniformTimestamps.LowerBound(_searchValues[i], SearchStrategy.Auto);
        }
        return totalIndex;
    }

    [Benchmark(Description = "Uniform-UpperBound-Binary")]
    public long UniformUpperBoundBinary()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _uniformTimestamps.UpperBound(_searchValues[i], SearchStrategy.Binary);
        }
        return totalIndex;
    }

    [Benchmark(Description = "Uniform-UpperBound-Interpolation")]
    public long UniformUpperBoundInterpolation()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _uniformTimestamps.UpperBound(_searchValues[i], SearchStrategy.Interpolation);
        }
        return totalIndex;
    }

    [Benchmark(Description = "Uniform-UpperBound-Auto")]
    public long UniformUpperBoundAuto()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _uniformTimestamps.UpperBound(_searchValues[i], SearchStrategy.Auto);
        }
        return totalIndex;
    }

    [Benchmark(Description = "Uniform-BinarySearch-Binary")]
    public long UniformBinarySearchBinary()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _uniformTimestamps.BinarySearch(_searchValues[i], strategy: SearchStrategy.Binary);
        }
        return totalIndex;
    }

    [Benchmark(Description = "Uniform-BinarySearch-Interpolation")]
    public long UniformBinarySearchInterpolation()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _uniformTimestamps.BinarySearch(_searchValues[i], strategy: SearchStrategy.Interpolation);
        }
        return totalIndex;
    }

    [Benchmark(Description = "Uniform-BinarySearch-Auto")]
    public long UniformBinarySearchAuto()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _uniformTimestamps.BinarySearch(_searchValues[i], strategy: SearchStrategy.Auto);
        }
        return totalIndex;
    }

    // ========== NON-UNIFORM DATA BENCHMARKS (should favor Binary or handle gracefully) ==========

    [Benchmark(Description = "NonUniform-LowerBound-Binary")]
    public long NonUniformLowerBoundBinary()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _nonUniformTimestamps.LowerBound(_searchValues[i], SearchStrategy.Binary);
        }
        return totalIndex;
    }

    [Benchmark(Description = "NonUniform-LowerBound-Interpolation")]
    public long NonUniformLowerBoundInterpolation()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _nonUniformTimestamps.LowerBound(_searchValues[i], SearchStrategy.Interpolation);
        }
        return totalIndex;
    }

    [Benchmark(Description = "NonUniform-LowerBound-Auto")]
    public long NonUniformLowerBoundAuto()
    {
        long totalIndex = 0;
        for (var i = 0; i < _searchValues.Length; i++)
        {
            totalIndex += _nonUniformTimestamps.LowerBound(_searchValues[i], SearchStrategy.Auto);
        }
        return totalIndex;
    }

    // ========== HELPER METHODS ==========

    /// <summary>
    /// Creates a file with uniformly distributed timestamps (simulates daily trades at regular intervals)
    /// Perfect for interpolation search testing
    /// </summary>
    private static void CreateUniformDataFile(string path, int count)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var list = new ListMmfTimeSeriesDateTimeSeconds(path, TimeSeriesOrder.Ascending, count);

        // Generate timestamps from 1996 to 2025 with uniform distribution
        var startDate = new DateTime(1996, 1, 1);
        var endDate = new DateTime(2025, 1, 1);
        var totalSeconds = (endDate - startDate).TotalSeconds;
        var secondsPerItem = totalSeconds / count;

        for (var i = 0; i < count; i++)
        {
            var timestamp = startDate.AddSeconds(i * secondsPerItem);
            list.Add(timestamp);
        }
    }

    /// <summary>
    /// Creates a file with non-uniformly distributed timestamps (simulates irregular events)
    /// Should trigger Auto strategy to use Binary search
    /// </summary>
    private static void CreateNonUniformDataFile(string path, int count)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var list = new ListMmfTimeSeriesDateTimeSeconds(path, TimeSeriesOrder.Ascending, count);

        var random = new Random(42);
        var startDate = new DateTime(1996, 1, 1);
        var currentDate = startDate;

        for (var i = 0; i < count; i++)
        {
            // Non-uniform intervals: mix of short and long gaps
            // Some events clustered (1-60 seconds), some sparse (hours/days)
            var intervalSeconds = random.Next(100) < 70
                ? random.Next(1, 60) // 70% short intervals (1-60 seconds)
                : random.Next(3600, 86400); // 30% long intervals (1 hour to 1 day)

            currentDate = currentDate.AddSeconds(intervalSeconds);
            list.Add(currentDate);
        }
    }
}
