using System.Collections;
using System.IO;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

public class ListMmfBitArrayTests
{
    [Fact]
    public void Get_ShouldEqualSet()
    {
        const int TestSize = 1000000;
        const string FileName = nameof(Get_ShouldEqualSet);
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        var bitArray = new BitArray(TestSize);
        for (var i = 0; i < TestSize; i++)
        {
            var value = i % 2 != 0;
            bitArray.Set(i, value);
        }
        using (var listBTBitArray = new ListMmfBitArray(FileName, TestSize))
        {
            for (var i = 0; i < TestSize; i++)
            {
                listBTBitArray[i] = bitArray[i];
            }
            for (var i = 0; i < TestSize; i++)
            {
                listBTBitArray[i].Should().Be(bitArray[i]);
            }
        }
        File.Delete(FileName);
    }

    [Fact]
    public void Not_ShouldWork()
    {
        const int TestSize = 1000000;
        var path = nameof(Not_ShouldWork);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        var bitArray = new BitArray(TestSize);
        for (var i = 0; i < TestSize; i++)
        {
            var value = i % 2 != 0;
            bitArray.Set(i, value);
        }
        using (var listBTBitArray = new ListMmfBitArray(path, TestSize))
        {
            for (var i = 0; i < TestSize; i++)
            {
                listBTBitArray[i] = bitArray[i];
            }
            listBTBitArray.Not();
            for (var i = 0; i < TestSize; i++)
            {
                listBTBitArray[i].Should().Be(!bitArray[i]);
            }
        }
        File.Delete(path);
    }

    [Fact]
    public void And_ShouldWork()
    {
        const int TestSize = 1000000;
        const string Path = nameof(And_ShouldWork);
        var path2 = nameof(And_ShouldWork) + 2;
        if (File.Exists(Path))
        {
            File.Delete(Path);
        }
        if (File.Exists(path2))
        {
            File.Delete(path2);
        }
        var bitArray = new BitArray(TestSize);
        for (var i = 0; i < TestSize; i++)
        {
            var value = i % 2 != 0;
            bitArray.Set(i, value);
        }
        using (var listBTBitArray1 = new ListMmfBitArray(Path, TestSize))
        {
            using var listBTBitArray2 = new ListMmfBitArray(path2, TestSize);
            for (var i = 0; i < TestSize; i++)
            {
                listBTBitArray1[i] = bitArray[i];
                listBTBitArray2[i] = !bitArray[i];
            }
            listBTBitArray1.And(listBTBitArray2);
            for (var i = 0; i < TestSize; i++)
            {
                var value = listBTBitArray1[i];
                var bitArrayValue = bitArray[i];
                if (value)
                {
                }
                listBTBitArray1[i].Should().Be(false);
            }
        }
        File.Delete(Path);
        File.Delete(path2);
    }

    [Fact]
    public void Or_ShouldWork()
    {
        const int TestSize = 1000000;
        var path = nameof(Or_ShouldWork);
        var path2 = nameof(Or_ShouldWork) + 2;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        if (File.Exists(path2))
        {
            File.Delete(path2);
        }
        var bitArray = new BitArray(TestSize);
        for (var i = 0; i < TestSize; i++)
        {
            var value = i % 2 != 0;
            bitArray.Set(i, value);
        }
        using (var listBTBitArray1 = new ListMmfBitArray(path, TestSize))
        {
            using var listBTBitArray2 = new ListMmfBitArray(path2, TestSize);
            for (var i = 0; i < TestSize; i++)
            {
                listBTBitArray1[i] = bitArray[i];
                listBTBitArray2[i] = !bitArray[i];
            }
            listBTBitArray1.Or(listBTBitArray2);
            for (var i = 0; i < TestSize; i++)
            {
                var value = listBTBitArray1[i];
                var bitArrayValue = bitArray[i];
                if (!value)
                {
                }
                listBTBitArray1[i].Should().Be(true);
            }
        }
        File.Delete(path);
        File.Delete(path2);
    }

    [Fact]
    public void Xor_ShouldWork()
    {
        const int TestSize = 1000000;
        var path = nameof(Xor_ShouldWork);
        var path2 = nameof(Xor_ShouldWork) + 2;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        if (File.Exists(path2))
        {
            File.Delete(path2);
        }
        var bitArray = new BitArray(TestSize);
        for (var i = 0; i < TestSize; i++)
        {
            var value = i % 2 != 0;
            bitArray.Set(i, value);
        }
        using (var listBTBitArray1 = new ListMmfBitArray(path, TestSize))
        {
            using var listBTBitArray2 = new ListMmfBitArray(path2, TestSize);
            for (var i = 0; i < TestSize; i++)
            {
                listBTBitArray1[i] = bitArray[i];
                listBTBitArray2[i] = !bitArray[i];
            }
            listBTBitArray1.Xor(listBTBitArray2);
            for (var i = 0; i < TestSize; i++)
            {
                var value = listBTBitArray1[i];
                var bitArrayValue = bitArray[i];
                if (!value)
                {
                }
                listBTBitArray1[i].Should().Be(true);
            }
        }
        File.Delete(path);
        File.Delete(path2);
    }

    

    [Fact]
    public void TruncateBeginningBitArrayTest()
    {
        const string FileName = $"{nameof(TruncateBeginningBitArrayTest)}";
        if (File.Exists(FileName))
        {
            File.Delete(FileName);
        }
        const int NumItems = 1000;
        const int CapacityItems = 510 + 512;
        using (var listMmfBitArray = new ListMmfBitArray(FileName, CapacityItems))
        {
            for (var i = 0; i < NumItems; i++)
            {
                var value = i % 2 != 0;
                listMmfBitArray.Set(i, value);
            }
            listMmfBitArray.Length.Should().Be(NumItems);
            listMmfBitArray.TruncateBeginning(NumItems - 1);
            listMmfBitArray.Length.Should().Be(NumItems - 1);
            for (var i = 0; i < NumItems - 1; i++)
            {
                var value = listMmfBitArray.Get(i);
                var valueShouldBe = i % 2 == 0; // opposite of what was set because we're moving by 1
                if (value != valueShouldBe)
                {
                }
                value.Should().Be(valueShouldBe);
            }
        }
        File.Delete(FileName);
    }
}