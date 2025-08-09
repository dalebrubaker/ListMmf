using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Order;
using BruSoftware.ListMmf;

namespace ListMmfBenchmarks;

//[MemoryDiagnoser]
//[ThreadingDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
public class ListMmfTimeSeriesDateTimeSecondsBenchmark
{
    private ListMmfTimeSeriesDateTimeSeconds _list;

    [Params(1000, 10000, 100000)]
    public int ItemsCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Initialize the ListMmfTimeSeriesDateTimeSeconds with some data
        _list = new ListMmfTimeSeriesDateTimeSeconds("path/to/file", TimeSeriesOrder.None, ItemsCount);
        var random = new Random();
        // add a random long
        for (var i = 0; i < ItemsCount; i++)
        {
            var randomDateTime = DateTime.Now.AddSeconds(random.Next(0, 10000));
            _list.Add(randomDateTime);
        }
    }

    [Benchmark]
    public void IndexerBenchmark()
    {
        // Access the indexer
        var count = 0;
        for (var i = 0; i < ItemsCount; i++)
        {
            var value = _list[i];
        }
        count++;
        if (count > ItemsCount)
        {
            Console.WriteLine("Dummy statement to avoid compiler optimization");
        }
    }
}

public class ProgramOld
{
    // public static void Main(string[] args)
    // {
    //     // ReSharper disable once JoinDeclarationAndInitializer
    //     DebugInProcessConfig config;
    // #if DEBUG
    //     config = new DebugInProcessConfig();
    // #else
    //     config = null;
    // #endif
    //
    //     var summary = BenchmarkRunner.Run<ListMmfTimeSeriesDateTimeSecondsBenchmark>(config);
    // }
}

/*
 OLD OBJECT LOCK
 *| Method           | ItemsCount | Mean        | Error     | StdDev    | Rank |
|----------------- |----------- |------------:|----------:|----------:|-----:|
| IndexerBenchmark | 1000       |    41.11 us |  0.159 us |  0.149 us |    1 |
| IndexerBenchmark | 10000      |   412.50 us |  2.122 us |  1.985 us |    2 |
| IndexerBenchmark | 100000     | 4,085.46 us | 24.158 us | 21.416 us |    3 |
 *
 *
 *
NEW System.Threading.Lock

| Method           | ItemsCount | Mean        | Error     | StdDev    | Rank |
|----------------- |----------- |------------:|----------:|----------:|-----:|
| IndexerBenchmark | 1000       |    20.53 us |  0.181 us |  0.160 us |    1 |
| IndexerBenchmark | 10000      |   203.67 us |  0.450 us |  0.421 us |    2 |
| IndexerBenchmark | 100000     | 2,055.47 us | 19.947 us | 18.658 us |    3 |

 *
*/