using System;
using System.IO;

namespace BruSoftware.ListMmf;

/// <summary>
/// Utilities to convert odd-byte ListMmf files (Int24/40/48/56 and UInt variants)
/// to standard widths for faster reads (Int32/UInt32 or Int64/UInt64).
/// </summary>
public static class ListMmfWidthConverter
{
    /// <summary>
    /// Returns true if the given <paramref name="dataType"/> is one of the odd-byte integer types.
    /// </summary>
    public static bool IsOddByte(DataType dataType)
    {
        return dataType is DataType.Int24AsInt64 or DataType.Int40AsInt64 or DataType.Int48AsInt64 or DataType.Int56AsInt64
            or DataType.UInt24AsInt64 or DataType.UInt40AsInt64 or DataType.UInt48AsInt64 or DataType.UInt56AsInt64;
    }

    /// <summary>
    /// Map odd-byte DataType to the standard width destination DataType.
    /// Int24/UInt24 -> Int32/UInt32, 40/48/56-bit -> Int64/UInt64.
    /// </summary>
    public static bool TryGetStandardDataType(DataType source, out DataType dest)
    {
        switch (source)
        {
            case DataType.Int24AsInt64:
                dest = DataType.Int32;
                return true;
            case DataType.UInt24AsInt64:
                dest = DataType.UInt32;
                return true;
            case DataType.Int40AsInt64:
            case DataType.Int48AsInt64:
            case DataType.Int56AsInt64:
                dest = DataType.Int64;
                return true;
            case DataType.UInt40AsInt64:
            case DataType.UInt48AsInt64:
            case DataType.UInt56AsInt64:
                dest = DataType.UInt64;
                return true;
            default:
                dest = DataType.AnyStruct;
                return false;
        }
    }

    /// <summary>
    /// Convert a single odd-byte ListMmf file to a standard width file. If <paramref name="destinationPath"/>
    /// is null or empty, a sibling file with suffix ".std.bt" will be created.
    /// </summary>
    /// <remarks>
    /// This method opens the source file in ReadWrite mode (as per ListMmf design) and requires exclusive writer access.
    /// Run it when no writer is using the file.
    /// </remarks>
    public static void ConvertOddByteFileToStandard(string sourcePath, string? destinationPath = null, int chunkSize = 100_000)
    {
        var (version, dataType, count) = UtilsListMmf.GetHeaderInfo(sourcePath);
        if (count <= 0)
        {
            // Create empty destination with correct type if odd-byte
            if (!TryGetStandardDataType(dataType, out var destType)) return;
            var dest = destinationPath ?? Path.ChangeExtension(sourcePath, null) + ".std.bt";
            using var empty = CreateEmptyList(destType, dest);
            return;
        }

        if (!TryGetStandardDataType(dataType, out var destDataType))
        {
            // Not an odd-byte type; nothing to do
            return;
        }

        var destPath = destinationPath ?? Path.ChangeExtension(sourcePath, null) + ".std.bt";

        switch (dataType)
        {
            case DataType.Int24AsInt64:
                ConvertInt24ToInt32(sourcePath, destPath, chunkSize);
                break;
            case DataType.UInt24AsInt64:
                ConvertUInt24ToUInt32(sourcePath, destPath, chunkSize);
                break;
            case DataType.Int40AsInt64:
                ConvertInt40ToInt64(sourcePath, destPath, chunkSize);
                break;
            case DataType.Int48AsInt64:
                ConvertInt48ToInt64(sourcePath, destPath, chunkSize);
                break;
            case DataType.Int56AsInt64:
                ConvertInt56ToInt64(sourcePath, destPath, chunkSize);
                break;
            case DataType.UInt40AsInt64:
                ConvertUInt40ToUInt64(sourcePath, destPath, chunkSize);
                break;
            case DataType.UInt48AsInt64:
                ConvertUInt48ToUInt64(sourcePath, destPath, chunkSize);
                break;
            case DataType.UInt56AsInt64:
                ConvertUInt56ToUInt64(sourcePath, destPath, chunkSize);
                break;
            default:
                return;
        }
    }

    private static IListMmf CreateEmptyList(DataType dataType, string path)
    {
        return dataType switch
        {
            DataType.Int32 => new ListMmf<int>(path, DataType.Int32, 0),
            DataType.UInt32 => new ListMmf<uint>(path, DataType.UInt32, 0),
            DataType.Int64 => new ListMmf<long>(path, DataType.Int64, 0),
            DataType.UInt64 => new ListMmf<ulong>(path, DataType.UInt64, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null)
        };
    }

    private static void ConvertUInt24ToUInt32(string sourcePath, string destPath, int chunkSize)
    {
        using var src = new ListMmf<UInt24AsInt64>(sourcePath, DataType.UInt24AsInt64);
        using var dst = new ListMmf<uint>(destPath, DataType.UInt32, src.Count);
        var buffer = new uint[Math.Min(chunkSize, (int)src.Count)];
        for (long start = 0; start < src.Count; start += buffer.Length)
        {
            var len = (int)Math.Min(buffer.Length, src.Count - start);
            var span = src.AsSpan(start, len);
            for (var i = 0; i < len; i++)
            {
                long v = span[i]; // implicit conversion (no allocation after optimization)
                buffer[i] = (uint)v;
            }
            dst.AddRange(buffer.AsSpan(0, len));
        }
    }

    private static void ConvertInt24ToInt32(string sourcePath, string destPath, int chunkSize)
    {
        using var src = new ListMmf<Int24AsInt64>(sourcePath, DataType.Int24AsInt64);
        using var dst = new ListMmf<int>(destPath, DataType.Int32, src.Count);
        var buffer = new int[Math.Min(chunkSize, (int)src.Count)];
        for (long start = 0; start < src.Count; start += buffer.Length)
        {
            var len = (int)Math.Min(buffer.Length, src.Count - start);
            var span = src.AsSpan(start, len);
            for (var i = 0; i < len; i++)
            {
                long v = span[i];
                buffer[i] = (int)v;
            }
            dst.AddRange(buffer.AsSpan(0, len));
        }
    }

    private static void ConvertInt40ToInt64(string sourcePath, string destPath, int chunkSize)
    {
        using var src = new ListMmf<Int40AsInt64>(sourcePath, DataType.Int40AsInt64);
        using var dst = new ListMmf<long>(destPath, DataType.Int64, src.Count);
        ConvertToInt64(src, dst, chunkSize);
    }

    private static void ConvertInt48ToInt64(string sourcePath, string destPath, int chunkSize)
    {
        using var src = new ListMmf<Int48AsInt64>(sourcePath, DataType.Int48AsInt64);
        using var dst = new ListMmf<long>(destPath, DataType.Int64, src.Count);
        ConvertToInt64(src, dst, chunkSize);
    }

    private static void ConvertInt56ToInt64(string sourcePath, string destPath, int chunkSize)
    {
        using var src = new ListMmf<Int56AsInt64>(sourcePath, DataType.Int56AsInt64);
        using var dst = new ListMmf<long>(destPath, DataType.Int64, src.Count);
        ConvertToInt64(src, dst, chunkSize);
    }

    private static void ConvertUInt40ToUInt64(string sourcePath, string destPath, int chunkSize)
    {
        using var src = new ListMmf<UInt40AsInt64>(sourcePath, DataType.UInt40AsInt64);
        using var dst = new ListMmf<ulong>(destPath, DataType.UInt64, src.Count);
        ConvertToUInt64(src, dst, chunkSize);
    }

    private static void ConvertUInt48ToUInt64(string sourcePath, string destPath, int chunkSize)
    {
        using var src = new ListMmf<UInt48AsInt64>(sourcePath, DataType.UInt48AsInt64);
        using var dst = new ListMmf<ulong>(destPath, DataType.UInt64, src.Count);
        ConvertToUInt64(src, dst, chunkSize);
    }

    private static void ConvertUInt56ToUInt64(string sourcePath, string destPath, int chunkSize)
    {
        using var src = new ListMmf<UInt56AsInt64>(sourcePath, DataType.UInt56AsInt64);
        using var dst = new ListMmf<ulong>(destPath, DataType.UInt64, src.Count);
        ConvertToUInt64(src, dst, chunkSize);
    }

    private static void ConvertToInt64<TOdd>(ListMmf<TOdd> src, ListMmf<long> dst, int chunkSize) where TOdd : struct
    {
        var buffer = new long[Math.Min(chunkSize, (int)src.Count)];
        for (long start = 0; start < src.Count; start += buffer.Length)
        {
            var len = (int)Math.Min(buffer.Length, src.Count - start);
            var span = src.AsSpan(start, len);
            for (var i = 0; i < len; i++)
            {
                buffer[i] = (long)(dynamic)span[i]; // relies on implicit operator long; kept local to conversion
            }
            dst.AddRange(buffer.AsSpan(0, len));
        }
    }

    private static void ConvertToUInt64<TOdd>(ListMmf<TOdd> src, ListMmf<ulong> dst, int chunkSize) where TOdd : struct
    {
        var buffer = new ulong[Math.Min(chunkSize, (int)src.Count)];
        for (long start = 0; start < src.Count; start += buffer.Length)
        {
            var len = (int)Math.Min(buffer.Length, src.Count - start);
            var span = src.AsSpan(start, len);
            for (var i = 0; i < len; i++)
            {
                var v = (long)(dynamic)span[i];
                buffer[i] = unchecked((ulong)v);
            }
            dst.AddRange(buffer.AsSpan(0, len));
        }
    }
}

