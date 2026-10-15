using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

/// <summary>
/// Reads from other threads while the writer remaps or disposes. A read through an unmapped view is an
/// AccessViolationException, which kills the test host, so a failure here may show up as a crashed run.
/// </summary>
public class ListMmfConcurrentReadTests : IDisposable
{
    private const int ReaderThreads = 4;

    private readonly string _testDirectory;
    private readonly string _testFilePath;

    public ListMmfConcurrentReadTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ListMmfConcurrentReadTests");
        Directory.CreateDirectory(_testDirectory);
        _testFilePath = Path.Combine(_testDirectory, $"test_{Guid.NewGuid()}.bt");
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch (Exception ex)
        {
            // Best effort cleanup
            Console.WriteLine($"Cleanup of {_testDirectory} failed: {ex.Message}");
        }
    }

    [Fact]
    public void Reads_WhileWriterGrowsAndShrinksThroughManyRemaps_SeeConsistentCountAndItems()
    {
        // Arrange
        const int Cycles = 200;
        const int AddsPerCycle = 500;
        using var list = new ListMmf<long>(_testFilePath, DataType.Int64, 1);
        var failures = new ConcurrentQueue<string>();
        var stop = false;
        var reads = 0L;

        var readers = StartReaders(() =>
        {
            var random = new Random();
            var lastCount = 0L;
            while (!Volatile.Read(ref stop))
            {
                var count = list.Count;
                if (count < lastCount)
                {
                    failures.Enqueue($"Count went from {lastCount} to {count}");
                }
                lastCount = count;
                if (count > 0)
                {
                    var last = list[count - 1];
                    if (last != count - 1)
                    {
                        failures.Enqueue($"list[{count - 1}] was {last}");
                    }
                    var index = random.NextInt64(count);
                    var item = list.ReadUnchecked(index);
                    if (item != index)
                    {
                        failures.Enqueue($"ReadUnchecked({index}) was {item}");
                    }
                }
                if (list.DataType != DataType.Int64)
                {
                    failures.Enqueue($"DataType was {list.DataType}");
                }
                Interlocked.Increment(ref reads);
            }
        }, failures);

        // Act: each cycle grows the file (remap) on Add, then shrinks it to Count (remap again)
        var expected = 0L;
        for (var cycle = 0; cycle < Cycles && failures.IsEmpty; cycle++)
        {
            for (var i = 0; i < AddsPerCycle; i++)
            {
                list.Add(expected++);
            }
            list.Capacity = list.Count;
        }
        Volatile.Write(ref stop, true);
        JoinAll(readers);

        // Assert
        failures.Should().BeEmpty();
        list.Count.Should().Be(Cycles * AddsPerCycle);
        Interlocked.Read(ref reads).Should().BeGreaterThan(0);
    }

    [Fact]
    public void Reads_WhileListIsDisposed_SeeZeroOrDisposed()
    {
        for (var round = 0; round < 20; round++)
        {
            // Arrange
            var path = Path.Combine(_testDirectory, $"dispose_{round}_{Guid.NewGuid()}.bt");
            var list = new ListMmf<long>(path, DataType.Int64, 1000);
            for (var i = 0; i < 1000; i++)
            {
                list.Add(i);
            }
            var failures = new ConcurrentQueue<string>();
            var readers = StartReaders(() =>
            {
                while (true)
                {
                    var count = list.Count;
                    try
                    {
                        _ = list[999];
                    }
                    catch (ListMmfTruncatedException)
                    {
                        // Expected once disposed: the indexer sees Count 0
                    }
                    try
                    {
                        _ = list.ReadUnchecked(999);
                    }
                    catch (ObjectDisposedException)
                    {
                        // Expected once disposed
                    }
                    if (count == 0)
                    {
                        return;
                    }
                    if (count != 1000)
                    {
                        failures.Enqueue($"Count was {count}");
                    }
                }
            }, failures);

            // Act
            Thread.Sleep(5);
            list.Dispose();
            JoinAll(readers);

            // Assert
            failures.Should().BeEmpty();
            list.Count.Should().Be(0);
        }
    }

    [Fact]
    public void Count_AfterDispose_IsZero_AndReadsThrow()
    {
        // Arrange
        var list = new ListMmf<int>(_testFilePath, DataType.Int32, 100);
        list.Add(1);
        list.Add(2);

        // Act
        list.Dispose();

        // Assert
        list.Count.Should().Be(0);
        list.GetInfo().Should().StartWith("count=0/");
        var indexer = () => list[0];
        indexer.Should().Throw<ListMmfTruncatedException>();
        var readUnchecked = () => list.ReadUnchecked(0);
        readUnchecked.Should().Throw<ObjectDisposedException>();
        var version = () => list.Version;
        version.Should().Throw<ObjectDisposedException>();
        var span = list.AsSpan(0, 0);
        span.Length.Should().Be(0);
    }

    [Fact]
    public void BitArray_CountAfterDispose_IsZero()
    {
        // Arrange
        var bits = new ListMmfBitArray(_testFilePath, 64);
        bits.Add(true);
        bits.Add(false);
        bits.Count.Should().Be(2);

        // Act
        bits.Dispose();

        // Assert
        bits.Count.Should().Be(0);
    }

    [Fact]
    public void Indexer_OutOfRange_ThrowsTruncated()
    {
        // Arrange
        using var list = new ListMmf<long>(_testFilePath, DataType.Int64, 10);
        list.Add(7);

        // Act
        var tooHigh = () => list[1];
        var negative = () => list[-1];

        // Assert
        list[0].Should().Be(7);
        tooHigh.Should().Throw<ListMmfTruncatedException>();
        negative.Should().Throw<ListMmfTruncatedException>();
    }

    [Fact]
    public void TimeSeriesDateTime_Indexer_ReadsInRange_AndThrowsOutOfRangeAndAfterDispose()
    {
        // Arrange
        var first = new DateTime(2026, 1, 2, 3, 4, 5);
        var series = new ListMmfTimeSeriesDateTime(_testFilePath, TimeSeriesOrder.AscendingOrEqual, 10);
        series.Add(first);
        series.Add(first.AddMinutes(1));

        // Act
        var tooHigh = () => series[2];
        var negative = () => series[-1];

        // Assert
        series[0].Should().Be(first);
        series[1].Should().Be(first.AddMinutes(1));
        tooHigh.Should().Throw<ArgumentOutOfRangeException>();
        negative.Should().Throw<ArgumentOutOfRangeException>();

        series.Dispose();
        var afterDispose = () => series[0];
        afterDispose.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void TimeSeriesDateTimeSeconds_Indexer_ReadsInRange_AndReturnsMinValueAfterDispose()
    {
        // Arrange
        var first = new DateTime(2026, 1, 2, 3, 4, 5);
        var series = new ListMmfTimeSeriesDateTimeSeconds(_testFilePath, TimeSeriesOrder.Ascending, 10);
        series.Add(first);
        series.Add(first.AddMinutes(1));

        // Act / Assert
        series[0].Should().Be(first);
        series[1].Should().Be(first.AddMinutes(1));

        series.Dispose();
        series[0].Should().Be(DateTime.MinValue);
    }

    private static Thread[] StartReaders(Action read, ConcurrentQueue<string> failures)
    {
        var readers = new Thread[ReaderThreads];
        for (var i = 0; i < readers.Length; i++)
        {
            readers[i] = new Thread(() =>
            {
                try
                {
                    read();
                }
                catch (Exception ex)
                {
                    failures.Enqueue(ex.ToString());
                }
            }) { IsBackground = true, Name = $"Reader {i}" };
            readers[i].Start();
        }
        return readers;
    }

    private static void JoinAll(Thread[] readers)
    {
        foreach (var reader in readers)
        {
            reader.Join(TimeSpan.FromSeconds(30)).Should().BeTrue($"{reader.Name} should finish");
        }
    }
}
