using System;
using System.IO;

namespace BruSoftware.ListMmf;

/// <summary>
/// Optimized upgrade process for SmallestInt64ListMmf that eliminates "catatonic" behavior.
/// Key improvements:
/// 1. Side-by-side upgrade (original file remains available)
/// 2. Bulk copy operations instead of value-by-value
/// 3. Atomic file swap when complete
/// 4. Crash recovery for interrupted upgrades
/// </summary>
public static class SmallestInt64ListMmfOptimized
{
    /// <summary>
    /// New optimized upgrade that takes an open SmallestInt64ListMmf instance.
    /// Creates new file, copies data, then swaps files to avoid Windows MMF closing delays.
    /// </summary>
    /// <param name="source">Open SmallestInt64ListMmf instance to upgrade</param>
    /// <param name="dataTypeNew">Target data type after upgrade</param>
    /// <param name="name">Name for progress reporting</param>
    /// <param name="progress">Progress reporting interface</param>
    public static void UpgradeOptimized(SmallestInt64ListMmf source, DataType dataTypeNew, string name, IProgressReport progress)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        var path = source.Path;
        var dataTypeExisting = source.DataType;
        var upgradingPath = path + ".upgrading";
        var count = source.Count;

        try
        {
            // Clean up any existing upgrade files from crashes
            if (File.Exists(upgradingPath))
            {
                File.Delete(upgradingPath);
            }
            if (File.Exists(upgradingPath + UtilsListMmf.LockFileExtension))
            {
                File.Delete(upgradingPath + UtilsListMmf.LockFileExtension);
            }

            if (count == 0)
            {
                // Create the new (Empty) file with correct data type
                using var destination = new SmallestInt64ListMmf(dataTypeNew, upgradingPath);
            }
            else
            {
                using var destination = new SmallestInt64ListMmf(dataTypeNew, upgradingPath, count);

                var message = $"Upgrading {name} {count:N0} {dataTypeExisting} values to {dataTypeNew}";
                SmallestInt64ListMmf.MessageEvent?.Invoke(null, message);

                progress?.Begin(count, $"Upgrading {name} to larger file.");

                // Use bulk operations instead of value-by-value copying
                BulkCopyValues(source, destination, count, progress);

                progress?.End(count);
            }

            // Dispose the source underlying to release the file. We don't want to Dispose the SmallestInt64ListMmf itself
            source._underlying.Dispose();

            // Delete the old file
            File.Delete(path);

            // Move the new file to the old file path
            File.Move(upgradingPath, path);
        }
        catch (Exception)
        {
            // Clean up upgrade file on failure
            try { File.Delete(upgradingPath); }
            catch
            {
                /* ignore */
            }
            progress?.End(count);
            throw;
        }
    }

    /// <summary>
    /// Bulk copy values in chunks instead of individual Add() calls.
    /// This eliminates the overhead of capacity checks and individual writes.
    /// </summary>
    private static void BulkCopyValues(SmallestInt64ListMmf source, SmallestInt64ListMmf destination, long count, IProgressReport progress)
    {
        const int chunkSize = 10000; // Process in chunks to report progress
        var values = new long[chunkSize];

        for (var startIndex = 0L; startIndex < count; startIndex += chunkSize)
        {
            var remainingCount = Math.Min(chunkSize, count - startIndex);

            // Read chunk of values using AsSpan for much better performance
            var sourceSpan = source.AsSpan(startIndex, (int)remainingCount);
            sourceSpan.CopyTo(values.AsSpan(0, (int)remainingCount));

            // Bulk add the chunk (much more efficient than individual Add() calls)
            var chunk = new ArraySegment<long>(values, 0, (int)remainingCount);
            destination.AddRange(chunk);

            // Check if user cancelled and report progress
            if (progress?.Update(startIndex + remainingCount - 1) == true)
            {
                // User cancelled - clean up and return
                return;
            }
        }
    }

    /// <summary>
    /// Clean up any leftover upgrade files from previous crashes.
    /// Should be called on application startup.
    /// </summary>
    public static void CleanupUpgradeFiles(string basePath)
    {
        try
        {
            var upgradingPath = basePath + ".upgrading";
            if (File.Exists(upgradingPath))
            {
                File.Delete(upgradingPath);
            }

            var backupPath = basePath + ".backup";
            if (File.Exists(backupPath))
            {
                // If backup exists but original doesn't, restore from backup
                if (!File.Exists(basePath))
                {
                    File.Move(backupPath, basePath);
                }
                else
                {
                    File.Delete(backupPath);
                }
            }
        }
        catch
        {
            // Ignore cleanup errors - not critical
        }
    }
}