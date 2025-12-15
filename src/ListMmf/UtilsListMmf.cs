using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace BruSoftware.ListMmf;

/// <summary>
/// Some utility helpers
/// </summary>
public static class UtilsListMmf
{
    // We use unspecified/local, NOT Utc internally. It is just too slow to continually convert to local time
    private static readonly DateTime s_unixEpoch = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// File extension used for POSIX lock files
    /// </summary>
    public const string LockFileExtension = ".lock";

    public static FileStream CreateFileStreamFromPath(string path, MemoryMappedFileAccess access)
    {
        if (string.IsNullOrEmpty(path))
        {
            throw new ListMmfException("A path is required.");
        }
        var fileMode = access == MemoryMappedFileAccess.ReadWrite ? FileMode.OpenOrCreate : FileMode.Open;
        var fileAccess = access == MemoryMappedFileAccess.ReadWrite ? FileAccess.ReadWrite : FileAccess.Read;
        var fileShare = access == MemoryMappedFileAccess.ReadWrite ? FileShare.Read : FileShare.ReadWrite;
        if (access == MemoryMappedFileAccess.Read && !File.Exists(path))
        {
            throw new ListMmfException($"Attempt to read non-existing file: {path}");
        }
        if (access == MemoryMappedFileAccess.ReadWrite)
        {
            // Create the directory if needed
            var directory = Path.GetDirectoryName(path);
            if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        return new FileStream(path, fileMode, fileAccess, fileShare);
    }

    public static (int version, DataType dataType, long count) GetHeaderInfo(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return (-1, DataType.AnyStruct, 0);
            }
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < 16)
            {
                throw new ListMmfException("Why?");
            }
            using var br = new BinaryReader(fs);
            var version = br.ReadInt32();
            var dataType = (DataType)br.ReadInt32();
            var count = br.ReadInt64();
            return (version, dataType, count);
        }
        catch (Exception e)
        {
            var msg = $"{e.Message} {path}";
            throw new ListMmfException(msg);
        }
    }

    public static DateTime GetHeaderInfoDateTime(string path, long index)
    {
        try
        {
            if (!File.Exists(path))
            {
                return DateTime.MinValue;
            }

            // Validate that this is specifically a Timestamps.bt file
            var fileName = Path.GetFileName(path);
            if (!string.Equals(fileName, "Timestamps.bt", StringComparison.OrdinalIgnoreCase))
            {
                throw new ListMmfException($"File must be named 'Timestamps.bt', but got '{fileName}'.");
            }

            const int HeaderSize = 16; // 4 bytes for version, 4 for data type, 8 for count
            using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var br = new BinaryReader(fs);

            // Read the header to get file info
            var version = br.ReadInt32();
            var dataType = (DataType)br.ReadInt32();
            var count = br.ReadInt64();

            // Validate that this is a timestamp file (Unix seconds stored as Int32)
            if (dataType != DataType.UnixSeconds)
            {
                throw new ListMmfException($"File contains {dataType} data, not timestamp data (UnixSeconds).");
            }

            // Validate index bounds
            if (index < 0 || index >= count)
            {
                throw new ListMmfException($"Index {index} is out of range. File contains {count} records.");
            }

            // Calculate position and validate file length
            var dataPosition = HeaderSize + index * 4L; // Each Unix timestamp is 4 bytes (Int32)
            if (fs.Length < dataPosition + 4)
            {
                throw new ListMmfException(
                    $"File too short for requested index {index}. Expected at least {dataPosition + 4} bytes, got {fs.Length}.");
            }

            // Seek to the correct position from the beginning of the file
            fs.Seek(HeaderSize + index * 4L, SeekOrigin.Begin);
            var unixSeconds = br.ReadInt32(); // Read the 4-byte Unix timestamp
            var result = unixSeconds.FromUnixSecondsToDateTime();
            return result;
        }
        catch (Exception e)
        {
            var msg = $"{e.Message} {path}";
            throw new ListMmfException(msg);
        }
    }

    /// <summary>
    /// Convert UNIX DateTime to Windows DateTime. Using int instead of long becsuse we are storing 4-bytes seconds.
    /// </summary>
    /// <param name="unixSeconds">time in seconds since Epoch</param>
    public static DateTime FromUnixSecondsToDateTime(this int unixSeconds)
    {
        if (unixSeconds == int.MinValue)
        {
            return DateTime.MinValue;
        }
        return s_unixEpoch.AddSeconds(unixSeconds);
    }

    /// <summary>
    /// Convert Windows DateTime to UNIX seconds. Using int instead of long becuse we are storing 4-bytes seconds.
    /// </summary>
    public static int ToUnixSeconds(this DateTime dateTime)
    {
        if (dateTime == DateTime.MinValue)
        {
            return int.MinValue;
        }
        var longResult = (long)(dateTime - s_unixEpoch).TotalSeconds;
        if (longResult > int.MaxValue)
        {
            // Stay within range rather than go negative, e.g. from UpperBound on a DateTime.MaxValue
            return int.MaxValue;
        }
        if (longResult < int.MinValue)
        {
            return int.MinValue;
        }
        var result = (int)longResult;
        //var check = result.FromUnixSecondsToDateTime();
        return result;
    }

    /// <summary>
    /// Open an existing list. Throws an exception if the file is empty or invalid.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="access"></param>
    /// <param name="useSmallestInt">MemoryMappedFileAccess.Read or .Read/Write</param>
    /// <returns></returns>
    /// <exception cref="ListMmfException">if empty existing file</exception>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public static IListMmf OpenExistingListMmf(string path, MemoryMappedFileAccess access, bool useSmallestInt)
    {
        var (version, dataType, count) = GetHeaderInfo(path);
        if (useSmallestInt)
        {
            return new SmallestInt64ListMmf(dataType, path, isReadOnly: access == MemoryMappedFileAccess.Read);
        }
        return dataType switch
        {
            DataType.AnyStruct => throw new ListMmfException("Unable to determine data type for empty file."),
            DataType.Bit => new ListMmfBitArray(path, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.SByte => new ListMmf<sbyte>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Byte => new ListMmf<byte>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Int16 => new ListMmf<short>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.UInt16 => new ListMmf<ushort>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Int32 => new ListMmf<int>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.UInt32 => new ListMmf<uint>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Int64 => new ListMmf<long>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.UInt64 => new ListMmf<ulong>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Single => new ListMmf<float>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Double => new ListMmf<double>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.DateTime => new ListMmf<DateTime>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Int24AsInt64 => new ListMmf<Int24AsInt64>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Int40AsInt64 => new ListMmf<Int40AsInt64>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Int48AsInt64 => new ListMmf<Int48AsInt64>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.Int56AsInt64 => new ListMmf<Int56AsInt64>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.UInt24AsInt64 => new ListMmf<UInt24AsInt64>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.UInt40AsInt64 => new ListMmf<UInt40AsInt64>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.UInt48AsInt64 => new ListMmf<UInt48AsInt64>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.UInt56AsInt64 => new ListMmf<UInt56AsInt64>(path, dataType, isReadOnly: access == MemoryMappedFileAccess.Read),
            DataType.UnixSeconds => throw new ListMmfException("UnixSeconds is not supported by OpenExistingListMmf."),
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    public static IListMmf<long> OpenAsInt64(string path, MemoryMappedFileAccess access, string? seriesName = null)
    {
        var (_, dataType, _) = GetHeaderInfo(path);
        if (dataType == DataType.AnyStruct)
        {
            throw new ListMmfException("Unable to determine data type for empty file.");
        }

        if (dataType is DataType.Bit)
        {
            throw new ListMmfException("OpenAsInt64 does not support DataType.Bit.");
        }

        if (dataType is DataType.UnixSeconds)
        {
            throw new ListMmfException("OpenAsInt64 does not support DataType.UnixSeconds.");
        }

        var isReadOnly = access == MemoryMappedFileAccess.Read;
        return dataType switch
        {
            DataType.SByte => CreateAdapter(new ListMmf<sbyte>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.Byte => CreateAdapter(new ListMmf<byte>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.Int16 => CreateAdapter(new ListMmf<short>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.UInt16 => CreateAdapter(new ListMmf<ushort>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.Int32 => CreateAdapter(new ListMmf<int>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.UInt32 => CreateAdapter(new ListMmf<uint>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.Int64 => CreateAdapter(new ListMmf<long>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.Int24AsInt64 => CreateAdapter(new ListMmf<Int24AsInt64>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.Int40AsInt64 => CreateAdapter(new ListMmf<Int40AsInt64>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.Int48AsInt64 => CreateAdapter(new ListMmf<Int48AsInt64>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.Int56AsInt64 => CreateAdapter(new ListMmf<Int56AsInt64>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.UInt24AsInt64 => CreateAdapter(new ListMmf<UInt24AsInt64>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.UInt40AsInt64 => CreateAdapter(new ListMmf<UInt40AsInt64>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.UInt48AsInt64 => CreateAdapter(new ListMmf<UInt48AsInt64>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            DataType.UInt56AsInt64 => CreateAdapter(new ListMmf<UInt56AsInt64>(path, dataType, isReadOnly: isReadOnly), dataType, seriesName, isReadOnly),
            _ => throw new ListMmfException($"OpenAsInt64 does not support {dataType}.")
        };
    }

    private static IListMmf<long> CreateAdapter<T>(ListMmf<T> list, DataType dataType, string? seriesName, bool isReadOnly)
        where T : struct
    {
        try
        {
            return ListMmfLongAdapter.Create(list, dataType, seriesName, isReadOnly);
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    public static void OpenAndTruncateExistingListMmf(string path, bool useSmallestInt, long newCount)
    {
        var (version, dataType, count) = GetHeaderInfo(path);
        if (dataType == DataType.AnyStruct || count == 0)
        {
            return;
        }
        using var list = OpenExistingListMmf(path, MemoryMappedFileAccess.ReadWrite, useSmallestInt);
        list.Truncate(newCount);
    }

    /// <summary>
    /// Efficient typed truncation using BulkCopyValues pattern from SmallestInt64ListMmfOptimized
    /// </summary>
    private static void TruncateFileFromBeginningTyped<T>(string sourcePath, string destPath, long itemsToRemove, long targetCount, DataType dataType)
        where T : struct
    {
        const int chunkSize = 10000; // Process in chunks like BulkCopyValues

        using var source = new ListMmf<T>(sourcePath, dataType);
        using var destination = new ListMmf<T>(destPath, dataType, targetCount);

        var values = new T[chunkSize];

        for (var startIndex = itemsToRemove; startIndex < source.Count; startIndex += chunkSize)
        {
            var remainingCount = Math.Min(chunkSize, source.Count - startIndex);

            // Read chunk using GetRange for better performance (like BulkCopyValues)
            var sourceSpan = source.AsSpan(startIndex, (int)remainingCount);
            sourceSpan.CopyTo(values.AsSpan(0, (int)remainingCount));

            // Bulk add the chunk (much more efficient than individual Add() calls)
            destination.AddRange(values.AsSpan(0, (int)remainingCount));
        }
    }

    /// <summary>
    /// Fallback truncation for types that require SmallestInt64ListMmf
    /// </summary>
    private static void TruncateFileFromBeginningSmallestInt(string sourcePath, string destPath, long itemsToRemove, long targetCount,
        DataType dataType)
    {
        const int chunkSize = 10000;

        using var source = new SmallestInt64ListMmf(dataType, sourcePath);
        using var destination = new SmallestInt64ListMmf(dataType, destPath, targetCount);

        var values = new long[chunkSize];

        for (var startIndex = itemsToRemove; startIndex < source.Count; startIndex += chunkSize)
        {
            var remainingCount = Math.Min(chunkSize, source.Count - startIndex);

            // Read chunk using GetRange for better performance
            var sourceSpan = source.AsSpan(startIndex, (int)remainingCount);
            sourceSpan.CopyTo(values.AsSpan(0, (int)remainingCount));

            // Bulk add the chunk
            var chunk = new ArraySegment<long>(values, 0, (int)remainingCount);
            destination.AddRange((IEnumerable<long>)chunk);
        }
    }
}
