using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Threading.Tasks;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests
{
    public class ListMmfTests
    {
        [Fact]
        public void CreateFile_SetsCapacity()
        {
            var fileName = $"{nameof(CreateFile_SetsCapacity)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (var listBT = new ListMmf<long>(fileName, DataType.Int64, access: MemoryMappedFileAccess.ReadWrite))
            {
                listBT.Capacity.Should().Be(listBT.CapacityFirstPage, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            }
            File.Delete(fileName);
        }

        [Fact]
        public void CapacitySet_ShouldGrowCapacityFile()
        {
            var fileName = $"{nameof(CapacitySet_ShouldGrowCapacityFile)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (var listBT = new ListMmf<long>(fileName, DataType.Int64, access: MemoryMappedFileAccess.ReadWrite))
            {
                listBT.Capacity.Should().Be(listBT.CapacityFirstPage, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listBT.Capacity = listBT.CapacityFirstPage + listBT.CapacityPerPageAfterFirstPage; // add another page
                listBT.Capacity.Should().Be(listBT.CapacityFirstPage + listBT.CapacityPerPageAfterFirstPage);
            }
            File.Delete(fileName);
        }

        [Fact]
        public void Add_ShouldGrowCount()
        {
            var fileName = $"{nameof(Add_ShouldGrowCount)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (var listBT = new ListMmf<long>(fileName, DataType.Int64, access: MemoryMappedFileAccess.ReadWrite))
            {
                listBT.Capacity.Should().Be(listBT.CapacityFirstPage, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listBT.Count.Should().Be(0);
                const int TestValue = 1;
                listBT.Add(TestValue);
                listBT.Count.Should().Be(1);
                var read = listBT[0];
                read.Should().Be(TestValue);
            }
            File.Delete(fileName);
        }

        [Fact]
        public void This_Test()
        {
            var fileName = $"{nameof(This_Test)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (var listBT = new ListMmf<long>(fileName, DataType.Int64, access: MemoryMappedFileAccess.ReadWrite))
            {
                listBT.Capacity.Should().Be(listBT.CapacityFirstPage, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listBT.Count.Should().Be(0);
                for (int i = 0; i < 3; i++)
                {
                    listBT.Add(i);
                    listBT[i].Should().Be(i);
                }
                listBT.Count.Should().Be(3);
                listBT.SetLast(89);
                listBT[2].Should().Be(89);
            }
            File.Delete(fileName);
        }

        [Fact]
        public void TrimExcess_ShouldTrimFile()
        {
            var fileName = $"{nameof(TrimExcess_ShouldTrimFile)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            const int capacityItems = 510 + 512;
            using (var listBT = new ListMmf<long>(fileName, DataType.Int64, capacityItems, MemoryMappedFileAccess.ReadWrite))
            {
                listBT.Capacity.Should().Be(listBT.CapacityFirstPage + listBT.CapacityPerPageAfterFirstPage,
                    "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listBT.Count.Should().Be(0);
                for (int i = 0; i < listBT.Capacity; i++)
                {
                    listBT.Add(i);
                }
                var capacity = listBT.Capacity;
                listBT.Count.Should().Be(capacity);
                listBT.Capacity += listBT.CapacityPerPageAfterFirstPage;
                listBT.Count.Should().Be(capacity, "Increased capacity should not have changed Count");
                var expected = listBT.CapacityFirstPage + 3 * listBT.CapacityPerPageAfterFirstPage;
                listBT.Capacity.Should().Be(expected, "Capacity was doubled");
                listBT.TrimExcess();
                listBT.Capacity.Should().Be(capacity, "Should have removed capacity beyond Count");
            }
            File.Delete(fileName);
        }

        [Fact]
        public void Dispose_ShouldTrimFile()
        {
            var fileName = $"{nameof(Dispose_ShouldTrimFile)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            const int capacityItems = 511 + 512;
            long capacityBytesBeforeAddingEmptyCapacity;
            using (var listBT = new ListMmf<long>(fileName, DataType.Int64, capacityItems, MemoryMappedFileAccess.ReadWrite))
            {
                var capacityBytes0 = listBT.CapacityBytes;
                listBT.Capacity.Should().Be(listBT.CapacityFirstPage + 2 *listBT.CapacityPerPageAfterFirstPage,
                    "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listBT.Count.Should().Be(0);
                for (int i = 0; i < listBT.Capacity; i++)
                {
                    listBT.Add(i);
                }
                var capacity = listBT.Capacity;
                listBT.Count.Should().Be(capacity);
                capacityBytesBeforeAddingEmptyCapacity = listBT.CapacityBytes;
                listBT.Capacity += 50;
                var capacityBytes1 = listBT.CapacityBytes;
                listBT.Count.Should().Be(capacity, "Increased capacity should not have changed Count");
                listBT.Capacity.Should().Be(listBT.CapacityFirstPage + 5 * listBT.CapacityPerPageAfterFirstPage, "Capacity doubled.");
            }
            var fileInfo = new FileInfo(fileName);
            fileInfo.Length.Should().Be(capacityBytesBeforeAddingEmptyCapacity);
            File.Delete(fileName);
        }

        [Fact]
        public void Update_ShouldChangeLastItem()
        {
            var fileName = $"{nameof(Update_ShouldChangeLastItem)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (var writer = new TestListMmf<long>(fileName, DataType.Int64))
            {
                writer.Add(1);
                writer.Add(2);
                writer.Add(3);
                writer[2].Should().Be(3);
                writer.SetLast(4);
                writer[2].Should().Be(4);
            }
            File.Delete(fileName);
        }

        [Fact]
        public void Truncate_ShouldWork()
        {
            var fileName = $"{nameof(Truncate_ShouldWork)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using var writer = new TestListMmf<long>(fileName, DataType.Int64)
            {
                1, 2, 3
            };
            writer.Count.Should().Be(3);
            writer.Truncate(2);
            writer.Count.Should().Be(2);
            long tmp;
            Action act = () => tmp = writer[3];
            act.Should().Throw<ListMmfTruncatedException>();
            writer.Dispose();
            File.Delete(fileName);
        }

        [Fact]
        public void AddRange_ShouldWork()
        {
            var fileName = $"{nameof(Truncate_ShouldWork)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (var writer = new TestListMmf<long>(fileName, DataType.Int64))
            {
                var list = new List<long> {1, 2, 3};
                writer.AddRange(list);
                writer.Count.Should().Be(3);
                writer[2].Should().Be(3);
            }
            File.Delete(fileName);
        }

        [Fact]
        public void Ctor_SecondWriterThrows()
        {
            var fileName = $"{nameof(Ctor_SecondWriterThrows)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            var isThrown = false;
            try
            {
                using var writer1 = new TestListMmf<long>(fileName, DataType.Int64);
                using var writer2 = new TestListMmf<long>(fileName, DataType.Int64);
                writer1.Add(1);
                writer2.Add(1);
            }
            catch (ReadWriteNotAvailableException)
            {
                isThrown = true;
            }
            isThrown.Should().BeTrue();
            File.Delete(fileName);
        }

        [Fact]
        public void Ctor_WriterThenReadersWorks()
        {
            var fileName = $"{nameof(Ctor_WriterThenReadersWorks)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (var writer1 = new ListMmf<long>(fileName, DataType.Int64, access: MemoryMappedFileAccess.ReadWrite))
            {
                using var reader = new ListMmf<long>(fileName, DataType.Int64);
                writer1.Add(1);
                reader[0].Should().Be(1);
            }
            File.Delete(fileName);
        }

        [Fact]
        public void Ctor_ReaderFirstThrows()
        {
            var mapName = $"{nameof(Ctor_ReaderFirstThrows)}";
            var isThrown = false;
            try
            {
                using var reader = new ListMmf<long>(mapName, DataType.Int64);
            }
            catch (ListMmfException)
            {
                isThrown = true;
            }
            isThrown.Should().BeTrue();
        }

        /// <summary>
        /// This test does NOT crash the reader as I hoped it would.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task WriterChangeCapacity_ShouldNotCrashReader()
        {
            const int NumValues = 1000;
            var fileName = $"{nameof(WriterChangeCapacity_ShouldNotCrashReader)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using var cts = new CancellationTokenSource();
            ListMmf<long> writer = null;
            var isDataWritten = false;
            var taskWriter = Task.Run(async () =>
            {
                writer = new ListMmf<long>(fileName, DataType.Int64, access: MemoryMappedFileAccess.ReadWrite);
                for (long i = 0L; i < NumValues; i++)
                {
                    writer.Add(i);
                }
                isDataWritten = true;
                var counter = 0;
                while (!cts.IsCancellationRequested)
                {
                    await Task.Delay(1, cts.Token);
                    if (counter++ % 2 == 0)
                    {
                        writer.Capacity = NumValues * 10;
                    }
                    else
                    {
                        writer.Capacity = NumValues;
                    }
                }
            }, cts.Token);
            while (!isDataWritten)
            {
                await Task.Delay(1, cts.Token);
            }
            using (var reader = new ListMmf<long>(fileName, DataType.Int64))
            {
                for (long i = 0L; i < NumValues; i++)
                {
                    reader[i].Should().Be(i);
                }
                var lastValue = reader[reader.Count - 1];
                lastValue.Should().Be(reader.Count - 1);
                await Task.Delay(100, cts.Token);
            }
            await Task.Delay(100, cts.Token);
            cts.Cancel();

            writer?.Dispose();
            File.Delete(fileName);
        }

        [Fact]
        public void ReadUncheckedTests()
        {
            var fileName = $"{nameof(ReadUncheckedTests)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            using (var writer = new ListMmf<long>(fileName, DataType.Int64, access: MemoryMappedFileAccess.ReadWrite))
            {
                //writer.Add(1);
                var expectedCapacityFirstPage = writer.CapacityFirstPage;
                writer.Capacity.Should().Be(expectedCapacityFirstPage,
                    "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                for (int i = 0; i < 510; i++)
                {
                    writer.Add(i);
                }
                var value = 29;
                writer.Capacity.Should().Be(expectedCapacityFirstPage, "No change");
                using var reader = new ListMmf<long>(fileName, DataType.Int64);
                reader.Capacity.Should().Be(expectedCapacityFirstPage, "Same as initial writer");
                reader[1].Should().Be(1);
                writer.Add(value);
                writer.Count.Should().Be(expectedCapacityFirstPage + 1);
                var expectedCapacityTwoPages = expectedCapacityFirstPage + writer.CapacityPerPageAfterFirstPage;
                writer.Capacity.Should().Be(expectedCapacityTwoPages, "Added a page");
                reader.Capacity.Should().Be(expectedCapacityFirstPage, "No change to reader until we read past the end of its current capacity");

                // Now this next line should not throw
                var value2 = reader.ReadUnchecked(writer.Count - 1);
                value2.Should().Be(value);
                reader.Capacity.Should().Be(expectedCapacityFirstPage + writer.CapacityPerPageAfterFirstPage, "ReadUnchecked() increased the size of the mmf.");
            }
            File.Delete(fileName);
        }
    }
}
