using BruSoftware.ListMmf;

// Example: Using Search Strategies with ListMmfTimeSeriesDateTimeSeconds

namespace ListMmfExamples;

public class SearchStrategyExamples
{
    public static void Main()
    {
        // Create a test file with uniform timestamp data (simulating daily trades)
        var path = Path.Combine(Path.GetTempPath(), "trades_example.bt");
        CreateUniformTradeData(path, 1_000_000); // 1M timestamps

        using var timestamps = new ListMmfTimeSeriesDateTimeSeconds(path, TimeSeriesOrder.Ascending);

        Console.WriteLine($"Loaded {timestamps.Count:N0} timestamps");
        Console.WriteLine($"First: {timestamps[0]:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Last: {timestamps[timestamps.Count - 1]:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine();

        // Search for a date somewhere in the middle
        var searchDate = new DateTime(2010, 6, 15, 14, 30, 0);

        // ========== EXAMPLE 1: Default (Auto strategy) ==========
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var index1 = timestamps.LowerBound(searchDate);
        sw.Stop();
        Console.WriteLine($"Auto Strategy:          index={index1:N0}, time={sw.Elapsed.TotalMicroseconds:F1}µs");

        // ========== EXAMPLE 2: Explicit Binary Strategy ==========
        sw.Restart();
        var index2 = timestamps.LowerBound(searchDate, SearchStrategy.Binary);
        sw.Stop();
        Console.WriteLine($"Binary Strategy:        index={index2:N0}, time={sw.Elapsed.TotalMicroseconds:F1}µs");

        // ========== EXAMPLE 3: Explicit Interpolation Strategy ==========
        sw.Restart();
        var index3 = timestamps.LowerBound(searchDate, SearchStrategy.Interpolation);
        sw.Stop();
        Console.WriteLine($"Interpolation Strategy: index={index3:N0}, time={sw.Elapsed.TotalMicroseconds:F1}µs");
        Console.WriteLine();

        // Verify all strategies return the same result
        if (index1 == index2 && index2 == index3)
        {
            Console.WriteLine("✓ All strategies returned the same index (correct!)");
        }
        else
        {
            Console.WriteLine("✗ ERROR: Strategies returned different indices!");
        }
        Console.WriteLine();

        // ========== EXAMPLE 4: Backtesting Simulation ==========
        Console.WriteLine("Simulating backtesting with 1000 searches...");

        var random = new Random(42);
        var searchDates = new DateTime[1000];
        for (var i = 0; i < searchDates.Length; i++)
        {
            var randomIndex = random.Next(0, (int)timestamps.Count);
            searchDates[i] = timestamps[randomIndex];
        }

        // Time Binary strategy
        sw.Restart();
        long totalBinary = 0;
        foreach (var date in searchDates)
        {
            totalBinary += timestamps.LowerBound(date, SearchStrategy.Binary);
        }
        var binaryTime = sw.Elapsed;

        // Time Interpolation strategy
        sw.Restart();
        long totalInterpolation = 0;
        foreach (var date in searchDates)
        {
            totalInterpolation += timestamps.LowerBound(date, SearchStrategy.Interpolation);
        }
        var interpolationTime = sw.Elapsed;

        // Time Auto strategy
        sw.Restart();
        long totalAuto = 0;
        foreach (var date in searchDates)
        {
            totalAuto += timestamps.LowerBound(date, SearchStrategy.Auto);
        }
        var autoTime = sw.Elapsed;

        Console.WriteLine($"Binary:        {binaryTime.TotalMilliseconds:F2}ms (baseline)");
        Console.WriteLine($"Interpolation: {interpolationTime.TotalMilliseconds:F2}ms ({binaryTime.TotalMilliseconds / interpolationTime.TotalMilliseconds:F2}x faster)");
        Console.WriteLine($"Auto:          {autoTime.TotalMilliseconds:F2}ms ({binaryTime.TotalMilliseconds / autoTime.TotalMilliseconds:F2}x faster)");
        Console.WriteLine();

        // ========== EXAMPLE 5: Range Search (common in backtesting) ==========
        var startDate = new DateTime(2010, 1, 1);
        var endDate = new DateTime(2010, 12, 31);

        var startIndex = timestamps.LowerBound(startDate, SearchStrategy.Auto);
        var endIndex = timestamps.UpperBound(endDate, SearchStrategy.Auto);
        var rangeCount = endIndex - startIndex;

        Console.WriteLine($"Date range search: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
        Console.WriteLine($"Found {rangeCount:N0} timestamps in range");

        // Cleanup
        File.Delete(path);
    }

    private static void CreateUniformTradeData(string path, int count)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var list = new ListMmfTimeSeriesDateTimeSeconds(path, TimeSeriesOrder.Ascending, count);

        // Generate timestamps from 1996 to 2025 with uniform distribution (simulating daily trades)
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
}
