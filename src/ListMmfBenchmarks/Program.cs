using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;

namespace ListMmfBenchmarks;

[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[RankColumn]
internal static class Program
{
/*
 	Slow Benchmark startup is because it takes 30 seconds or so to do 10 million random tests on the 10 GB file the first time, vs 350 ms after warm-up. 
    This is almost certainly due to pulling pages into RAM. Second test only 5 seconds, etc., as my RAM gets loaded with more pages. 
    There is overlap with 10 million random accesses into 10 GB.
 */

    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "lowerbound")
        {
            BenchmarkLowerBound();
        }
        else
        {
            //DebugTestPointerHugeFile();
            //TestArrayModified();

            //BenchmarkRandomReads();

            //BenchmarkRandomWrites();
            //var summary = BenchmarkRunner.Run<BenchmarkTwoViews>();

            //DebugAppend();
            BenchmarkReadOnlyLists();
        }

        Console.ReadLine();
    }

    private static void BenchmarkLowerBound()
    {
        Console.WriteLine("Running LowerBound benchmark: MMF vs File I/O");
        var summary = BenchmarkRunner.Run<BenchmarkLowerBound>();
    }

    private static void BenchmarkReadOnlyLists()
    {
        // ReSharper disable once JoinDeclarationAndInitializer
        DebugInProcessConfig config;
#if DEBUG
        config = new DebugInProcessConfig();
#else
        config = null;
#endif

        var summaryW = BenchmarkRunner.Run<BenchmarkReadOnlyList64View>(config);
    }

//     private static void DebugAppend()
//     {
//         //For debugging
//         var test2 = new DebugAppend();
//         test2.GlobalSetup();
//         test2.Append();
//         test2.Append();
//         test2.Append();
//         test2.Append();
//         test2.Append();
//         test2.Append();
//         test2.Append();
//         test2.Append();
//         test2.Append();
//         test2.Append();
//         test2.Append();
//         test2.GlobalCleanup();
//
//         // Seems never to finish var summary = BenchmarkRunner.Run<DebugAppend>();
//         Console.WriteLine("Done with BenchmarkAppend");
//     }
//
//     private static void BenchmarkReadOnlyLists()
//     {
//         // ReSharper disable once JoinDeclarationAndInitializer
//         DebugInProcessConfig config;
// #if DEBUG
//         config = new DebugInProcessConfig();
// #else
// config = null;
// #endif
//
//         
//         // var test = new BenchmarkReadOnlyLists();
//         // test.GlobalSetup();
//         // test.ReadRandomListMmf();
//         // test.GlobalCleanup();
//
//         var summaryW = BenchmarkRunner.Run<BenchmarkReadOnlyList64View>(config);
//     }
//
//     private static void BenchmarkRandomWrites()
//     {
//         //var test = new BenchmarkRandomWrites();
//         //test.GlobalSetup();
//         //test.ReadWriteRandomMemoryMappedUnsafeGeneric();
//         //test.GlobalCleanup();
//         var summaryW = BenchmarkRunner.Run<BenchmarkRandomWrites>();
//     }
//
//     private static void BenchmarkRandomReads()
//     {
//         // For debugging
//         // var test = new BenchmarkRandomReads();
//         // test.GlobalSetup();
//         // test.ReadRandomMemoryMappedUnsafeGeneric();
//         // test.GlobalCleanup();
//
//         var summary = BenchmarkRunner.Run<BenchmarkRandomReads>();
//     }
//
//     private static void DebugTestPointerHugeFile()
//     {
//         var _ = new TestPointerHugeFile();
//         _.WriteRead();
//     }
//
//     /// <summary>
//     ///     Prove that Array, unlike List and other collections,
//     ///     does NOT throw if the collection is modified during foreach()
//     /// </summary>
//     private static void TestArrayModified()
//     {
//         var array = new long[1000];
//         array[2] = 2;
//         var total = 0L;
//         foreach (var item in array)
//         {
//             total += item;
//         }
//         foreach (var item in array)
//         {
//             total += item;
//             array[5] = 5;
//         }
//     }
}
