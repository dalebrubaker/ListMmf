using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

// ReSharper disable ConvertToUsingDeclaration

namespace ListMmfTests
{
    public class CtorTests
    {
        [Fact]
        public void CreateNew_SetsCapacity()
        {
            var mapName = $"{nameof(CreateNew_SetsCapacity)}";
            const int capacityItems = 10;
            using (var listMmf = ListMmf<long>.CreateNew(mapName, capacityItems))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            }
        }

        [Fact]
        public void CreateFile_SetsCapacity()
        {
            var fileName = $"{nameof(CreateFile_SetsCapacity)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            const int capacityItems = 10;
            using (var listMmf = ListMmf<long>.CreateFromFile(fileName, capacityItems: capacityItems))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
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
            const int capacityItems = 10;
            using (var listMmf = ListMmf<long>.CreateFromFile(fileName, capacityItems: capacityItems))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Capacity = 511 + 512; // add another page
                listMmf.Capacity.Should().Be(511 + 512);
            }
            File.Delete(fileName);
        }

        [Fact]
        public void Add_ShouldGrowCount()
        {
            var mapName = $"{nameof(Add_ShouldGrowCount)}";
            const int capacityItems = 10;
            using (var listMmf = ListMmf<long>.CreateNew(mapName, capacityItems))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Count.Should().Be(0);
                const int testValue = 1;
                listMmf.Add(testValue);
                listMmf.Count.Should().Be(1);
                var read = listMmf[0];
                read.Should().Be(testValue);
            }
        }

        [Fact]
        public void This_Test()
        {
            var mapName = $"{nameof(This_Test)}";
            const int capacityItems = 10;
            using (var listMmf = ListMmf<long>.CreateNew(mapName, capacityItems))
            {
                listMmf.Capacity.Should().Be(511, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Count.Should().Be(0);
                for (int i = 0; i < 3; i++)
                {
                    listMmf.Add(i);
                    listMmf[i].Should().Be(i);
                }
                listMmf.Count.Should().Be(3);
                listMmf[2] = 89;
                listMmf[2].Should().Be(89);
            }
        }

        [Fact]
        public void TrimExcess_ShouldTrimFile()
        {
            var fileName = $"{nameof(TrimExcess_ShouldTrimFile)}";
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
            const int capacityItems = 511 + 512;
            using (var listMmf = ListMmf<long>.CreateFromFile(fileName, capacityItems: capacityItems))
            {
                listMmf.Capacity.Should().Be(511 + 512, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Count.Should().Be(0);
                for (int i = 0; i < listMmf.Capacity; i++)
                {
                    listMmf.Add(i);
                }
                var capacity = listMmf.Capacity;
                listMmf.Count.Should().Be(capacity);
                listMmf.Capacity += 512;
                listMmf.Count.Should().Be(capacity, "Increased capacity should not have changed Count");
                listMmf.Capacity.Should().Be(2047, "Capacity was doubled");
                listMmf.TrimExcess();
                listMmf.Capacity.Should().Be(capacity, "Should have removed capacity beyond Count");
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
            long capacityBytesBeforeAddingEmptyCapacity = 0;
            using (var listMmf = ListMmf<long>.CreateFromFile(fileName, capacityItems: capacityItems))
            {
                listMmf.Capacity.Should().Be(511 + 512, "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
                listMmf.Count.Should().Be(0);
                for (int i = 0; i < listMmf.Capacity; i++)
                {
                    listMmf.Add(i);
                }
                var capacity = listMmf.Capacity;
                listMmf.Count.Should().Be(capacity);
                capacityBytesBeforeAddingEmptyCapacity = listMmf.CapacityBytes;
                listMmf.Capacity += 512;
                listMmf.Count.Should().Be(capacity, "Increased capacity should not have changed Count");
                listMmf.Capacity.Should().Be(2047, "Capacity doubled.");
            }
            var fileInfo = new FileInfo(fileName);
            fileInfo.Length.Should().Be(capacityBytesBeforeAddingEmptyCapacity);
            File.Delete(fileName);
        }

        [Fact]
        public void IsAnyoneReadingWriting_Tests()
        {
            var mapName = $"{nameof(IsAnyoneReadingWriting_Tests)}";
            var isAnyoneReading = ListMmf.IsAnyoneReading(mapName);
            isAnyoneReading.Should().BeFalse("No reader yet.");
            var isAnyoneWriting = ListMmf.IsAnyoneWriting(mapName);
            isAnyoneWriting.Should().BeFalse("No writer yet.");

            const int capacityItems = 10;
            using (var listMmf = ListMmf<long>.CreateNew(mapName, capacityItems))
            {
                isAnyoneReading = ListMmf.IsAnyoneReading(mapName);
                isAnyoneReading.Should().BeFalse("This is a writer, not a reader");
                isAnyoneWriting = ListMmf.IsAnyoneWriting(mapName);
                isAnyoneWriting.Should().BeTrue("This is a writer.");
            }
            isAnyoneReading = ListMmf.IsAnyoneReading(mapName);
            isAnyoneReading.Should().BeFalse("No reader yet.");
            isAnyoneWriting = ListMmf.IsAnyoneWriting(mapName);
            isAnyoneWriting.Should().BeFalse("Writer finished.");
        }

        [Fact]
        public async Task CreateNewBlocking_ShouldTimeout()
        {
            var mapName = nameof(CreateNewBlocking_ShouldTimeout);
            const int capacityItems = 10;
            var timeout = 100;
            using (var listMmf1 = ListMmf<long>.CreateNew(mapName, capacityItems, maximumCount: 1, timeout: timeout))
            {
                listMmf1.Should().NotBeNull("Was first so was created.");
                var sw = Stopwatch.StartNew();
                using (var listMmf2 = ListMmf<long>.CreateNew(mapName, capacityItems, timeout: timeout))
                {
                    var elapsed = sw.ElapsedMilliseconds;
                    elapsed.Should().BeGreaterThan(timeout - 10, "Blocked until timed out.");
                    await Task.Delay(timeout + 10);
                    listMmf2.Should().BeNull("Blocked then timed out");
                }
            }
        }
    }
}
