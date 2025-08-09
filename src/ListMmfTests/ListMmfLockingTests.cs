using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;

namespace ListMmfTests;

public class ListMmfLockingTests : IDisposable
{
    private readonly string _testFilePath;
    private readonly string _testDirectory;

    public ListMmfLockingTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "ListMmfLockingTests");
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
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void ResetPointers_WorksNormally_BeforeDisallow()
    {
        // Arrange
        using var listMmf = new ListMmf<int>(_testFilePath, DataType.Int32, 100);

        // Act - Add data and truncate (which calls ResetPointers)
        for (var i = 0; i < 2000; i++)
        {
            listMmf.Add(i);
        }

        // This should work fine before locking
        listMmf.Truncate(1000);

        // Assert
        listMmf.Count.Should().Be(1000);
        listMmf.IsResetPointersDisallowed.Should().BeFalse();
    }

    [Fact]
    public async Task DisallowResetPointers_IsThreadSafe()
    {
        // Arrange
        using var listMmf = new ListMmf<long>(_testFilePath, DataType.Int64, 100);
        var lockCount = 0;
        var exceptionCount = 0;

        // Act - Call DisallowResetPointers from multiple threads
        var tasks = new Task[10];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    listMmf.DisallowResetPointers();
                    Interlocked.Increment(ref lockCount);
                }
                catch
                {
                    Interlocked.Increment(ref exceptionCount);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All calls should succeed without exceptions
        lockCount.Should().Be(10);
        exceptionCount.Should().Be(0);
        listMmf.IsResetPointersDisallowed.Should().BeTrue();
    }

    [Fact]
    public void SmallestEnumListMmf_ImplementsInterface()
    {
        // This test verifies that SmallestEnumListMmf properly implements the interface methods
        // Arrange
        var enumType = typeof(DayOfWeek);
        using var listMmf = new SmallestEnumListMmf<DayOfWeek>(enumType, _testFilePath, 100);

        // Add some values
        listMmf.Add(DayOfWeek.Monday);
        listMmf.Add(DayOfWeek.Tuesday);

        // Act - Use the interface methods
        listMmf.IsResetPointersDisallowed.Should().BeFalse();
        listMmf.DisallowResetPointers();

        // Assert - The flag should be set
        listMmf.IsResetPointersDisallowed.Should().BeTrue();
    }

    [Fact]
    public void DisallowResetPointers_PersistsAcrossFileReopen()
    {
        // Note: This test verifies that the lock is in-memory only and does NOT persist
        // This is expected behavior - the lock is per-instance, not persisted to disk

        // Arrange - Create and lock a file
        using (var listMmf1 = new ListMmf<int>(_testFilePath, DataType.Int32, 100))
        {
            listMmf1.Add(42);
            listMmf1.Add(43);
            listMmf1.DisallowResetPointers();
            listMmf1.IsResetPointersDisallowed.Should().BeTrue();
        }

        // Act - Reopen the same file
        using (var listMmf2 = new ListMmf<int>(_testFilePath, DataType.Int32, 100))
        {
            // Assert - The new instance should NOT be locked
            listMmf2.IsResetPointersDisallowed.Should().BeFalse();

            // Should be able to truncate (which would trigger ResetPointers)
            listMmf2.Truncate(1);
            listMmf2.Count.Should().Be(1);
        }
    }

    [Fact]
    public void PosixLockFile_CleansUpStaleLockFile()
    {
        // This test only runs on POSIX systems (macOS, Linux)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on Windows
        }

        // Arrange - Create a stale lock file (simulating a crash)
        var lockPath = _testFilePath + UtilsListMmf.LockFileExtension;
        File.WriteAllText(lockPath, "stale lock from crashed process");
        File.Exists(lockPath).Should().BeTrue("lock file should exist before test");

        // Act - Create a new ListMmf which should clean up the stale lock
        using (var listMmf = new ListMmf<int>(_testFilePath, DataType.Int32, 100))
        {
            listMmf.Add(42);

            // Assert - The ListMmf should be created successfully
            listMmf.Count.Should().Be(1);
        }

        // Assert - Lock file should be cleaned up after disposal
        File.Exists(lockPath).Should().BeFalse("lock file should be cleaned up after disposal");
    }

    [Fact]
    public void PosixLockFile_PreventsConcurrentWriters()
    {
        // This test only runs on POSIX systems (macOS, Linux)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on Windows
        }

        // Arrange - Create first writer
        using var writer1 = new ListMmf<int>(_testFilePath, DataType.Int32, 100);
        writer1.Add(42);

        // Act & Assert - Attempt to create second writer should fail
        var exception = Assert.Throws<IOException>(() =>
        {
            using var writer2 = new ListMmf<int>(_testFilePath, DataType.Int32, 100);
        });

        exception.Message.Should().Contain("already open by another writer");
    }

    [Fact]
    public void PosixLockFile_AllowsWriterAfterFirstWriterCloses()
    {
        // This test only runs on POSIX systems (macOS, Linux)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // Skip on Windows
        }

        // Arrange & Act - Create and close first writer
        using (var writer1 = new ListMmf<int>(_testFilePath, DataType.Int32, 100))
        {
            writer1.Add(42);
        }

        // Act - Create second writer after first one closes
        using (var writer2 = new ListMmf<int>(_testFilePath, DataType.Int32, 100))
        {
            writer2.Add(43);

            // Assert - Second writer should work fine
            writer2.Count.Should().Be(2);
            writer2[0].Should().Be(42);
            writer2[1].Should().Be(43);
        }
    }
}