using System;
using BenchmarkDotNet.Running;

namespace ListMmfBenchmarks
{
    internal class Program
    {
/*
 	Slow Benchmark startup is because it takes 30 seconds or so to do 10 million random tests on the 10 GB file the first time, vs 350 ms after warmup. 
    This is almost certainly due to pulling pages into RAM. Second test only 5 seconds, etc., as my RAM gets loaded with more pages. 
    There is overlap with 10 million random accesses into 10 GB.
 */
 
        private static void Main(string[] args)
        {
            //DebugTestPointerHugeFile();
            //TestArrayModified();

            //BenchmarkRandomReads();
            //BenchmarkLocks();

            BenchmarkLocker();
            
            //BenchmarkRandomWritest();
            //var summary = BenchmarkRunner.Run<BenchmarkTwoViews>();

            //DebugAppend();

            Console.ReadLine();
        }

        private static void DebugAppend()
        {
            //For debugging
            var test2 = new DebugAppend();
            test2.GlobalSetup();
            test2.Append();
            test2.Append();
            test2.Append();
            test2.Append();
            test2.Append();
            test2.Append();
            test2.Append();
            test2.Append();
            test2.Append();
            test2.Append();
            test2.Append();
            test2.GlobalCleanup();

            // Seems never to finish var summary = BenchmarkRunner.Run<DebugAppend>();
            Console.WriteLine("Done with BenchmarkAppend");
        }

        private static void BenchmarkRandomWritest()
        {
            //var test = new BenchmarkRandomWrites();
            //test.GlobalSetup();
            //test.ReadWriteRandomMemoryMappedUnsafeGeneric();
            //test.GlobalCleanup();
            var summaryW = BenchmarkRunner.Run<BenchmarkRandomWrites>();
        }

        private static void BenchmarkRandomReads()
        {
            // For debugging
            //var test = new BenchmarkRandomReads();
            //test.GlobalSetup();
            //test.ReadRandomMemoryMappedUnsafeGeneric();
            //test.GlobalCleanup();
            var summary = BenchmarkRunner.Run<BenchmarkRandomReads>();
        }

        private static void BenchmarkLocks()
        {
            // For debugging
            //var test = new BenchmarkLocks();
            //test.GlobalSetup();
            //test.ReadRandomMemoryMappedUnsafeGenericReAcquirePointer();
            //test.GlobalCleanup();
            var summary = BenchmarkRunner.Run<BenchmarkLocks>();
        }


        private static void BenchmarkLocker()
        {
            // For debugging
            //var test = new BenchmarkLocker();
            //test.GlobalSetup();
            ////test.ReadRandomMemoryMappedUnsafeGeneric();
            //test.ReadRandomMemoryMappedUnsafeGenericLockerNull();
            //test.GlobalCleanup();

            var summary = BenchmarkRunner.Run<BenchmarkLocker>();
        }

        private static void DebugTestPointerHugeFile()
        {
            var tmp = new TestPointerHugeFile();
            tmp.WriteRead();
        }

        /// <summary>
        /// Prove that Array, unlike List and other collections,
        /// does NOT throw if the collection is modified during foreach()
        /// </summary>
        private static void TestArrayModified()
        {
            var array = new long[1000];
            array[2] = 2;
            var total = 0L;
            foreach (var item in array) total += item;
            foreach (var item in array)
            {
                total += item;
                array[5] = 5;
            }
        }
    }
}
