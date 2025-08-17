using System;
using BenchmarkDotNet.Attributes;
using BruSoftware.ListMmf;

namespace ListMmfBenchmarks;

public class BenchmarkReadOnlyList64View
{
    private ListMmf<long> _listMmf;
    private ReadOnlyList64View<long> _listView;
    private int[] _testIndexes;

    [Params(10000000)] //, 10000000)] 
    public int NumTests { get; set; } = 1000000;

    [GlobalSetup]
    public void GlobalSetup()
    {
        if (!Environment.Is64BitProcess)
        {
            throw new PlatformNotSupportedException("Requires a 64-bit process (x64 or ARM64).");
        }
        const string TestFilePath = @"C:\_HugeArray\Timestamps.btd"; // 9.91 GB of longs
        _listMmf = new ListMmf<long>(TestFilePath, DataType.Int64);
        _listView = new ReadOnlyList64View<long>(_listMmf, 0);
        var fi = new FileInfo(TestFilePath);
        var count = fi.Length / 8; // the Count in the testFilePath is dateTime.Ticks
        var random = new Random(1);
        _testIndexes = new int[NumTests];
        for (var i = 0; i < NumTests; i++)
        {
            var index = random.Next(0, (int)count);
            _testIndexes[i] = index;
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _listMmf.Dispose();
    }

    [Benchmark(Baseline = true)]
    public long ReadRandomListMmf()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];
            value = _listMmf[index];
        }
        return value;
    }

    [Benchmark]
    public long ReadRandomListView()
    {
        var value = 0L;
        for (var i = 0; i < _testIndexes.Length; i++)
        {
            var index = _testIndexes[i];
            value = _listView[index];
        }
        return value;
    }

    /*
                  Method | NumTests |     Mean |    Error |   StdDev |   Median | Ratio | RatioSD |
    |------------------- |--------- |---------:|---------:|---------:|---------:|------:|--------:|
    |   ReadRandomListMmf |  1000000 | 43.86 ms | 0.807 ms | 1.516 ms | 43.67 ms |  1.00 |    0.00 |
    | ReadRandomListView |  1000000 | 88.90 ms | 2.819 ms | 8.267 ms | 86.70 ms |  1.96 |    0.19 |

    |             Method | NumTests |     Mean |    Error |   StdDev | Ratio | RatioSD |
    |------------------- |--------- |---------:|---------:|---------:|------:|--------:|
    |   ReadRandomListMmf | 10000000 | 412.2 ms |  8.08 ms |  8.65 ms |  1.00 |    0.00 |
    | ReadRandomListView | 10000000 | 802.2 ms | 15.97 ms | 21.32 ms |  1.94 |    0.07 |
    */
}