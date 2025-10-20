using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using BruSoftware.ListMmf;

namespace ListMmfBenchmarks;

/// <summary>
/// Compare read throughput between:
///  - Standard 4-byte storage (ListMmf<uint>)
///  - Odd-byte 3-byte storage via struct (ListMmf<UInt24AsInt64>)
///  - Odd-byte 3-byte storage read via SmallestInt64ListMmf (allocates long[] during AsSpan)
///
/// This demonstrates the remaining overhead when expanding odd-byte elements to long[],
/// and the relative speed of direct zero-copy reads for standard widths.
/// </summary>
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
[SimpleJob(warmupCount: 1, iterationCount: 3)] // Fast job configuration for quicker results
public class BenchmarkOddByteVsStandard
{
    private string _workDir;
    private string _odd24Path;
    private string _stdU32Path;
    private string _odd40Path;
    private string _stdU64Path;

    // Sweep sizes to expose bandwidth vs compute tradeoffs
    [Params(100_000, 1_000_000)] // Reduced from 10M to 1M max for faster benchmarks
    public int ItemCount { get; set; }

    // Repeat passes over the same data to amplify per-pass costs
    [Params(1)] // Reduced from 5 to 1 for faster benchmarks
    public int Passes { get; set; }

    // Number of random index reads per invocation (capped by ItemCount)
    [Params(100_000)] // Reduced from 1M to 100K for faster benchmarks
    public int RandomOps { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("Requires a 64-bit process (x64 or ARM64).");
        }

        _workDir = Path.Combine(Path.GetTempPath(), "ListMmfBench_OddVsStd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
        _odd24Path = Path.Combine(_workDir, "u24.bt");
        _stdU32Path = Path.Combine(_workDir, "u32.bt");
        _odd40Path = Path.Combine(_workDir, "u40.bt");
        _stdU64Path = Path.Combine(_workDir, "u64.bt");

        // Generate data within UInt24 range
        var count = ItemCount;

        // Write standard 4-byte file in chunks
        using (var u32List = new ListMmf<uint>(_stdU32Path, DataType.UInt32, count))
        {
            const int chunk = 200_000;
            var buf = new uint[Math.Min(chunk, count)];
            int written = 0;
            while (written < count)
            {
                var len = Math.Min(buf.Length, count - written);
                for (var i = 0; i < len; i++)
                {
                    buf[i] = (uint)((written + i) & 0x00FF_FFFF); // 24-bit range
                }
                u32List.AddRange(buf.AsSpan(0, len));
                written += len;
            }
        }

        // Write odd-byte 3-byte file in chunks
        using (var u24List = new ListMmf<UInt24AsInt64>(_odd24Path, DataType.UInt24AsInt64, count))
        {
            const int chunk = 200_000;
            var buf = new UInt24AsInt64[Math.Min(chunk, count)];
            int written = 0;
            while (written < count)
            {
                var len = Math.Min(buf.Length, count - written);
                for (var i = 0; i < len; i++)
                {
                    var v = (uint)((written + i) & 0x00FF_FFFF);
                    buf[i] = new UInt24AsInt64(v);
                }
                u24List.AddRange(buf.AsSpan(0, len));
                written += len;
            }
        }

        // Write standard 8-byte file (UInt64) to compare vs 40-bit
        using (var u64List = new ListMmf<ulong>(_stdU64Path, DataType.UInt64, count))
        {
            const int chunk = 200_000;
            var buf = new ulong[Math.Min(chunk, count)];
            int written = 0;
            while (written < count)
            {
                var len = Math.Min(buf.Length, count - written);
                for (var i = 0; i < len; i++)
                {
                    buf[i] = (ulong) (written + i);
                }
                u64List.AddRange(buf.AsSpan(0, len));
                written += len;
            }
        }

        // Write odd-byte 5-byte file in chunks (UInt40)
        using (var u40List = new ListMmf<UInt40AsInt64>(_odd40Path, DataType.UInt40AsInt64, count))
        {
            const int chunk = 200_000;
            var buf = new UInt40AsInt64[Math.Min(chunk, count)];
            int written = 0;
            while (written < count)
            {
                var len = Math.Min(buf.Length, count - written);
                for (var i = 0; i < len; i++)
                {
                    buf[i] = new UInt40AsInt64(written + i);
                }
                u40List.AddRange(buf.AsSpan(0, len));
                written += len;
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        try
        {
            if (File.Exists(_odd24Path)) File.Delete(_odd24Path);
            if (File.Exists(_stdU32Path)) File.Delete(_stdU32Path);
            if (File.Exists(_odd40Path)) File.Delete(_odd40Path);
            if (File.Exists(_stdU64Path)) File.Delete(_stdU64Path);
            if (Directory.Exists(_workDir)) Directory.Delete(_workDir, true);
        }
        catch
        {
            // ignore
        }
    }

    [Benchmark(Baseline = true)]
    public long Read_Std_UInt32_AsSpan_Sum()
    {
        using var list = new ListMmf<uint>(_stdU32Path, DataType.UInt32);
        long sum = 0;
        for (var p = 0; p < Passes; p++)
        {
            var span = list.AsSpan(0, (int)list.Count);
            for (var i = 0; i < span.Length; i++) sum += span[i];
        }
        return sum;
    }

    [Benchmark]
    public long Read_Odd_UInt24_Struct_AsSpan_Sum()
    {
        using var list = new ListMmf<UInt24AsInt64>(_odd24Path, DataType.UInt24AsInt64);
        long sum = 0;
        for (var p = 0; p < Passes; p++)
        {
            var span = list.AsSpan(0, (int)list.Count);
            for (var i = 0; i < span.Length; i++) sum += (long)span[i];
        }
        return sum;
    }

    [Benchmark]
    public long Read_Odd_UInt24_Smallest_AsSpan_Sum()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt24AsInt64, _odd24Path);
        long sum = 0;
        for (var p = 0; p < Passes; p++)
        {
            var span = smallest.AsSpan(0, (int)smallest.Count); // allocates long[] internally
            for (var i = 0; i < span.Length; i++) sum += span[i];
        }
        return sum;
    }

    [Benchmark]
    public long Read_Odd_UInt24_Adapter_AsSpan_Sum()
    {
        using var adapter = (IListMmfLongAdapter)UtilsListMmf.OpenAsInt64(_odd24Path, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.ReadWrite);
        long sum = 0;
        for (var p = 0; p < Passes; p++)
        {
            var span = adapter.AsSpan(0, (int)adapter.Count); // uses pooled buffer + Int64Conversion
            for (var i = 0; i < span.Length; i++) sum += span[i];
        }
        return sum;
    }

    [Benchmark]
    public long Read_Std_UInt64_AsSpan_Sum()
    {
        using var list = new ListMmf<ulong>(_stdU64Path, DataType.UInt64);
        long sum = 0;
        for (var p = 0; p < Passes; p++)
        {
            var span = list.AsSpan(0, (int)list.Count);
            for (var i = 0; i < span.Length; i++) sum += (long)span[i];
        }
        return sum;
    }

    [Benchmark]
    public long Read_Odd_UInt40_Struct_AsSpan_Sum()
    {
        using var list = new ListMmf<UInt40AsInt64>(_odd40Path, DataType.UInt40AsInt64);
        long sum = 0;
        for (var p = 0; p < Passes; p++)
        {
            var span = list.AsSpan(0, (int)list.Count);
            for (var i = 0; i < span.Length; i++) sum += (long)span[i];
        }
        return sum;
    }

    [Benchmark]
    public long Read_Odd_UInt40_Smallest_AsSpan_Sum()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt40AsInt64, _odd40Path);
        long sum = 0;
        for (var p = 0; p < Passes; p++)
        {
            var span = smallest.AsSpan(0, (int)smallest.Count);
            for (var i = 0; i < span.Length; i++) sum += span[i];
        }
        return sum;
    }

    [Benchmark]
    public long Read_Odd_UInt40_Adapter_AsSpan_Sum()
    {
        using var adapter = (IListMmfLongAdapter)UtilsListMmf.OpenAsInt64(_odd40Path, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.ReadWrite);
        long sum = 0;
        for (var p = 0; p < Passes; p++)
        {
            var span = adapter.AsSpan(0, (int)adapter.Count);
            for (var i = 0; i < span.Length; i++) sum += span[i];
        }
        return sum;
    }

    // Random access variants (RandomOps operations)

    [Benchmark]
    public long Random_Std_UInt32_Indexer_Sum()
    {
        using var list = new ListMmf<uint>(_stdU32Path, DataType.UInt32);
        var rng = new Random(1);
        long sum = 0;
        var ops = Math.Min(RandomOps, (int)list.Count);
        for (var i = 0; i < ops; i++)
        {
            var idx = rng.Next(0, (int)list.Count);
            sum += list[idx];
        }
        return sum;
    }

    [Benchmark]
    public long Random_Odd_UInt24_Struct_Indexer_Sum()
    {
        using var list = new ListMmf<UInt24AsInt64>(_odd24Path, DataType.UInt24AsInt64);
        var rng = new Random(1);
        long sum = 0;
        var ops = Math.Min(RandomOps, (int)list.Count);
        for (var i = 0; i < ops; i++)
        {
            var idx = rng.Next(0, (int)list.Count);
            sum += (long)list[idx];
        }
        return sum;
    }

    [Benchmark]
    public long Random_Odd_UInt24_Smallest_Indexer_Sum()
    {
        using var smallest = new SmallestInt64ListMmf(DataType.UInt24AsInt64, _odd24Path);
        var rng = new Random(1);
        long sum = 0;
        var ops = Math.Min(RandomOps, (int)smallest.Count);
        for (var i = 0; i < ops; i++)
        {
            var idx = rng.Next(0, (int)smallest.Count);
            sum += smallest[idx];
        }
        return sum;
    }

    [Benchmark]
    public long Random_Odd_UInt24_Adapter_Indexer_Sum()
    {
        using var adapter = (IListMmfLongAdapter)UtilsListMmf.OpenAsInt64(_odd24Path, System.IO.MemoryMappedFiles.MemoryMappedFileAccess.ReadWrite);
        var rng = new Random(1);
        long sum = 0;
        var ops = Math.Min(RandomOps, (int)adapter.Count);
        for (var i = 0; i < ops; i++)
        {
            var idx = rng.Next(0, (int)adapter.Count);
            sum += adapter[idx];
        }
        return sum;
    }
}


