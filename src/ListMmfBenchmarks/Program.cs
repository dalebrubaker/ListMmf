using System;
using BenchmarkDotNet.Running;

namespace ListMmfBenchmarks
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            //DebugTestPointerHugeFile();

            //TestArrayModified();

            //BenchmarkRandomReads();
            BenchmarkLocks();
            //BenchmarkLocker();
            
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
            //// For debugging
            //var test = new BenchmarkLocker();
            //test.GlobalSetup();
            //test.ReadRandomMemoryMappedUnsafeGeneric();
            ////test.ReadRandomMemoryMappedUnsafeGenericLockerSemaphore();
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
