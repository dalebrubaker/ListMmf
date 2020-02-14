using System;

namespace ListMmfBenchmarks
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var tmp = new TestPointerHugeFile();
            tmp.WriteRead();

            //TestArrayModified();

            // For debugging
            //var test = new BenchmarkRandomReads();
            //test.GlobalSetup();
            //test.ReadRandomMemoryMappedUnsafeGenericReAcquirePointer();
            //test.GlobalCleanup();
            //var summary = BenchmarkRunner.Run<BenchmarkRandomReads>();
            //var summary = BenchmarkRunner.Run<BenchmarkTwoViews>();

            //var test = new BenchmarkRandomWrites();
            //test.GlobalSetup();
            //test.ReadWriteRandomMemoryMappedUnsafeGeneric();
            //test.GlobalCleanup();

            //var summaryW = BenchmarkRunner.Run<BenchmarkRandomWrites>();

            //var test2 = new BenchmarkAppend();
            //test2.GlobalSetup();
            //test2.Append();
            //test2.GlobalCleanup();

            Console.ReadLine();
        }

        /// <summary>
        /// Prove that Array, unlike List and other collections,
        /// does NOT throw if the collection is modifed during foreach()
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