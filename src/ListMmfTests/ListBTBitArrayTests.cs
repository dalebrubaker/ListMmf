using System.Collections;
using System.IO;
using System.IO.MemoryMappedFiles;
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
        var path = nameof(Get_ShouldEqualSet);
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
        using (var listBTBitArray = new ListMmfBitArray(path, TestSize, MemoryMappedFileAccess.ReadWrite))
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
        File.Delete(path);
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
        using (var listBTBitArray = new ListMmfBitArray(path, TestSize, MemoryMappedFileAccess.ReadWrite))
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
        using (var listBTBitArray1 = new ListMmfBitArray(Path, TestSize, MemoryMappedFileAccess.ReadWrite))
        {
            using var listBTBitArray2 = new ListMmfBitArray(path2, TestSize, MemoryMappedFileAccess.ReadWrite);
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
        using (var listBTBitArray1 = new ListMmfBitArray(path, TestSize, MemoryMappedFileAccess.ReadWrite))
        {
            using var listBTBitArray2 = new ListMmfBitArray(path2, TestSize, MemoryMappedFileAccess.ReadWrite);
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
        using (var listBTBitArray1 = new ListMmfBitArray(path, TestSize, MemoryMappedFileAccess.ReadWrite))
        {
            using var listBTBitArray2 = new ListMmfBitArray(path2, TestSize, MemoryMappedFileAccess.ReadWrite);
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
    public void CtorBitArray_WriterThenReadersWorks()
    {
        var fileName = $"{nameof(CtorBitArray_WriterThenReadersWorks)}";
        if (File.Exists(fileName))
        {
            File.Delete(fileName);
        }
        var mapName = fileName;
        using (var writer1 = new ListMmfBitArray(fileName, 10, MemoryMappedFileAccess.ReadWrite))
        {
            using var reader = new ListMmfBitArray(mapName);
            writer1.Set(0, true);
            reader[0].Should().Be(true);
        }
        File.Delete(fileName);
    }
}