using BenchmarkDotNet.Attributes;
using BruSoftware.ListMmf;

namespace ListMmfBenchmarks;

/// <summary>
/// Measures the per-call cost of reading Count, the indexer and ReadUnchecked on a ListMmf.
/// With ConcurrentWriter=true, a background thread keeps appending, truncating and shrinking the list,
/// so the file is remapped repeatedly while the benchmark thread reads.
/// </summary>
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class BenchmarkCountRead
{
    private const int Ops = 100_000;
    private const int InitialCount = 1 << 20;
    private const int IndexMask = InitialCount - 1;
    private const int WriterBurst = 200_000;

    private string _workDir = null!;
    private ListMmf<long> _list = null!;
    private Thread? _writer;
    private volatile bool _stopWriter;

    [Params(false, true)]
    public bool ConcurrentWriter { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("Requires a 64-bit process (x64 or ARM64).");
        }

        _workDir = Path.Combine(Path.GetTempPath(), "ListMmfBench_CountRead_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_workDir);
        _list = new ListMmf<long>(Path.Combine(_workDir, "count.bt"), DataType.Int64, InitialCount);
        for (var i = 0; i < InitialCount; i++)
        {
            _list.Add(i);
        }

        if (ConcurrentWriter)
        {
            _stopWriter = false;
            _writer = new Thread(WriterLoop) { IsBackground = true, Name = "BenchmarkCountRead writer" };
            _writer.Start();
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _stopWriter = true;
        _writer?.Join();
        _list.Dispose();
        Directory.Delete(_workDir, true);
    }

    /// <summary>
    /// Appends a burst (growing and remapping the file), truncates back, then shrinks Capacity (remapping again).
    /// </summary>
    private void WriterLoop()
    {
        while (!_stopWriter)
        {
            for (var i = 0; i < WriterBurst && !_stopWriter; i++)
            {
                _list.Add(i);
            }
            _list.Truncate(InitialCount);
            _list.Capacity = InitialCount;
        }
    }

    [Benchmark(OperationsPerInvoke = Ops)]
    public long Count()
    {
        long sum = 0;
        for (var i = 0; i < Ops; i++)
        {
            sum += _list.Count;
        }
        return sum;
    }

    [Benchmark(OperationsPerInvoke = Ops)]
    public long Indexer()
    {
        long sum = 0;
        for (var i = 0; i < Ops; i++)
        {
            sum += _list[i & IndexMask];
        }
        return sum;
    }

    [Benchmark(OperationsPerInvoke = Ops)]
    public long ReadUnchecked()
    {
        long sum = 0;
        for (var i = 0; i < Ops; i++)
        {
            sum += _list.ReadUnchecked(i & IndexMask);
        }
        return sum;
    }
}
