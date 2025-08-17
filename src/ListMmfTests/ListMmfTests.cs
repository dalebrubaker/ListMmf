using System;
using System.Collections.Generic;
using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using NLog;
using Xunit;

namespace ListMmfTests;

public class ListMmfTests
{
    [Fact]
    public void CreateFile_SetsCapacity()
    {
        const string FileName = $"{nameof(CreateFile_SetsCapacity)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        using (var listBT = new ListMmf<long>(FileName, DataType.Int64))
        {
            listBT.Capacity.Should().Be(listBT.CapacityFirstPage,
                "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
        }
        File.Delete(FileName);
    }

    [Fact]
    public void CapacitySet_ShouldGrowCapacityFile()
    {
        const string FileName = $"{nameof(CapacitySet_ShouldGrowCapacityFile)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        using (var listBT = new ListMmf<long>(FileName, DataType.Int64))
        {
            listBT.Capacity.Should().Be(listBT.CapacityFirstPage,
                "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            listBT.Capacity = listBT.CapacityFirstPage + listBT.CapacityPerPageAfterFirstPage; // add another page
            listBT.Capacity.Should().Be(listBT.CapacityFirstPage + listBT.CapacityPerPageAfterFirstPage);
        }
        File.Delete(FileName);
    }

    [Fact]
    public void Add_ShouldGrowCount()
    {
        const string FileName = $"{nameof(Add_ShouldGrowCount)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        using (var listBT = new ListMmf<long>(FileName, DataType.Int64))
        {
            listBT.Capacity.Should().Be(listBT.CapacityFirstPage,
                "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            listBT.Count.Should().Be(0);
            const int TestValue = 1;
            listBT.Add(TestValue);
            listBT.Count.Should().Be(1);
            var read = listBT[0];
            read.Should().Be(TestValue);
        }
        File.Delete(FileName);
    }

    [Fact]
    public void This_Test()
    {
        const string FileName = $"{nameof(This_Test)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        using (var listBT = new ListMmf<long>(FileName, DataType.Int64))
        {
            listBT.Capacity.Should().Be(listBT.CapacityFirstPage,
                "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            listBT.Count.Should().Be(0);
            for (var i = 0; i < 3; i++)
            {
                listBT.Add(i);
                listBT[i].Should().Be(i);
            }
            listBT.Count.Should().Be(3);
            listBT.SetLast(89);
            listBT[2].Should().Be(89);
        }
        File.Delete(FileName);
    }

    [Fact]
    public void TrimExcess_ShouldTrimFile()
    {
        const string FileName = $"{nameof(TrimExcess_ShouldTrimFile)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        const int CapacityItems = 510 + 512;
        using (var listBT = new ListMmf<long>(FileName, DataType.Int64, CapacityItems))
        {
            listBT.Capacity.Should().Be(listBT.CapacityFirstPage + listBT.CapacityPerPageAfterFirstPage,
                "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            listBT.Count.Should().Be(0);
            for (var i = 0; i < listBT.Capacity; i++)
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
        File.Delete(FileName);
    }

    [Fact]
    public void Dispose_ShouldTrimFile()
    {
        const string FileName = $"{nameof(Dispose_ShouldTrimFile)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        const int CapacityItems = 511 + 512;
        long capacityBytesBeforeAddingEmptyCapacity;
        using (var listBT = new ListMmf<long>(FileName, DataType.Int64, CapacityItems))
        {
            var capacityBytes0 = listBT.CapacityBytes;
            listBT.Capacity.Should().Be(listBT.CapacityFirstPage + 2 * listBT.CapacityPerPageAfterFirstPage,
                "Capacity is rounded up to the 4096 page size used in a view, reduced by header size and the Count location.");
            listBT.Count.Should().Be(0);
            for (var i = 0; i < listBT.Capacity; i++)
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
        var fileInfo = new FileInfo(FileName);
        fileInfo.Length.Should().Be(capacityBytesBeforeAddingEmptyCapacity);
        File.Delete(FileName);
    }

    [Fact]
    public void Update_ShouldChangeLastItem()
    {
        const string FileName = $"{nameof(Update_ShouldChangeLastItem)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        using (var writer = new TestListMmf<long>(FileName, DataType.Int64))
        {
            writer.Add(1);
            writer.Add(2);
            writer.Add(3);
            writer[2].Should().Be(3);
            writer.SetLast(4);
            writer[2].Should().Be(4);
        }
        File.Delete(FileName);
    }

    [Fact]
    public void Truncate_ShouldWork()
    {
        const string FileName = $"{nameof(Truncate_ShouldWork)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        using var writer = new TestListMmf<long>(FileName, DataType.Int64)
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
        File.Delete(FileName);
    }

    [Fact]
    public void AddRange_ShouldWork()
    {
        const string FileName = $"{nameof(AddRange_ShouldWork)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        using (var writer = new TestListMmf<long>(FileName, DataType.Int64))
        {
            var list = new List<long> { 1, 2, 3 };
            writer.AddRange(list);
            writer.Count.Should().Be(3);
            writer[2].Should().Be(3);
        }
        File.Delete(FileName);
    }

    [Fact]
    public void AddRange_Span_ShouldAppendInOrder()
    {
        const string FileName = $"{nameof(AddRange_Span_ShouldAppendInOrder)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }

        using (var list = new TestListMmf<long>(FileName, DataType.Int64))
        {
            list.Add(1);
            list.Add(2);

            var additional = new long[] { 3, 4, 5 }.AsSpan();
            list.AddRange(additional);

            list.Count.Should().Be(5);
            list[0].Should().Be(1);
            list[1].Should().Be(2);
            list[2].Should().Be(3);
            list[3].Should().Be(4);
            list[4].Should().Be(5);
        }

        File.Delete(FileName);
    }

    [Fact]
    public void Ctor_SecondWriterThrows()
    {
        const string FileName = $"{nameof(Ctor_SecondWriterThrows)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }

        // Clean up any stale lock file
        var lockFileName = FileName + UtilsListMmf.LockFileExtension;
        if (File.Exists(lockFileName))
        {
            File.Delete(lockFileName);
        }

        var isThrown = false;
        try
        {
            using var writer1 = new TestListMmf<long>(FileName, DataType.Int64);
            using var writer2 = new TestListMmf<long>(FileName, DataType.Int64);
            writer1.Add(1);
            writer2.Add(1);
        }
        catch (IOException ex)
        {
            Console.WriteLine($"Caught IOException: {ex.Message}");
            isThrown = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Caught unexpected exception: {ex.GetType().Name}: {ex.Message}");
        }
        isThrown.Should().BeTrue();

        // Clean up
        try { File.Delete(FileName); }
        catch { }
        try { File.Delete(lockFileName); }
        catch { }
    }

    [Fact]
    public void TruncateBeginningTest()
    {
        const string FileName = $"{nameof(TruncateBeginningTest)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        const int CapacityItems = 510 + 512;
        using (var listMmf = new ListMmf<long>(FileName, DataType.Int64, CapacityItems))
        {
            for (var i = 0; i < 1000; i++)
            {
                listMmf.Add(i);
            }
            listMmf.Count.Should().Be(1000);
            listMmf.TruncateBeginning(999);
            listMmf.Count.Should().Be(999);
            for (var i = 0; i < 999; i++)
            {
                var value = listMmf[i];
                value.Should().Be(i + 1);
            }
        }
        File.Delete(FileName);
    }
}