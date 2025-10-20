using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BruSoftware.ListMmf;

internal static class Int64Conversion<T>
    where T : struct
{
    private enum ConverterKind
    {
        Unsupported,
        Int64,
        Int32,
        UInt32,
        Int16,
        UInt16,
        SByte,
        Byte,
        Int24,
        UInt24,
        Int40,
        UInt40,
        Int48,
        UInt48,
        Int56,
        UInt56
    }

    private static readonly ConverterKind s_kind = DetermineKind();

    public static bool IsSupported => s_kind != ConverterKind.Unsupported;

    public static long ToInt64(T value)
    {
        EnsureSupported();
        ref var valueRef = ref Unsafe.AsRef(in value);
        switch (s_kind)
        {
            case ConverterKind.Int64:
                return Unsafe.As<T, long>(ref valueRef);
            case ConverterKind.Int32:
                return Unsafe.As<T, int>(ref valueRef);
            case ConverterKind.UInt32:
                return Unsafe.As<T, uint>(ref valueRef);
            case ConverterKind.Int16:
                return Unsafe.As<T, short>(ref valueRef);
            case ConverterKind.UInt16:
                return Unsafe.As<T, ushort>(ref valueRef);
            case ConverterKind.SByte:
                return Unsafe.As<T, sbyte>(ref valueRef);
            case ConverterKind.Byte:
                return Unsafe.As<T, byte>(ref valueRef);
            case ConverterKind.Int24:
                return Unsafe.As<T, Int24AsInt64>(ref valueRef);
            case ConverterKind.UInt24:
                return Unsafe.As<T, UInt24AsInt64>(ref valueRef);
            case ConverterKind.Int40:
                return Unsafe.As<T, Int40AsInt64>(ref valueRef);
            case ConverterKind.UInt40:
                return Unsafe.As<T, UInt40AsInt64>(ref valueRef);
            case ConverterKind.Int48:
                return Unsafe.As<T, Int48AsInt64>(ref valueRef);
            case ConverterKind.UInt48:
                return Unsafe.As<T, UInt48AsInt64>(ref valueRef);
            case ConverterKind.Int56:
                return Unsafe.As<T, Int56AsInt64>(ref valueRef);
            case ConverterKind.UInt56:
                return Unsafe.As<T, UInt56AsInt64>(ref valueRef);
            default:
                throw new NotSupportedException(GetMessage());
        }
    }

    public static T FromInt64(long value)
    {
        EnsureSupported();
        switch (s_kind)
        {
            case ConverterKind.Int64:
                {
                    var tmp = value;
                    return Unsafe.As<long, T>(ref tmp);
                }
            case ConverterKind.Int32:
                {
                    var tmp = checked((int)value);
                    return Unsafe.As<int, T>(ref tmp);
                }
            case ConverterKind.UInt32:
                {
                    var tmp = checked((uint)value);
                    return Unsafe.As<uint, T>(ref tmp);
                }
            case ConverterKind.Int16:
                {
                    var tmp = checked((short)value);
                    return Unsafe.As<short, T>(ref tmp);
                }
            case ConverterKind.UInt16:
                {
                    var tmp = checked((ushort)value);
                    return Unsafe.As<ushort, T>(ref tmp);
                }
            case ConverterKind.SByte:
                {
                    var tmp = checked((sbyte)value);
                    return Unsafe.As<sbyte, T>(ref tmp);
                }
            case ConverterKind.Byte:
                {
                    var tmp = checked((byte)value);
                    return Unsafe.As<byte, T>(ref tmp);
                }
            case ConverterKind.Int24:
                {
                    var tmp = new Int24AsInt64(value);
                    return Unsafe.As<Int24AsInt64, T>(ref tmp);
                }
            case ConverterKind.UInt24:
                {
                    var tmp = new UInt24AsInt64(value);
                    return Unsafe.As<UInt24AsInt64, T>(ref tmp);
                }
            case ConverterKind.Int40:
                {
                    var tmp = new Int40AsInt64(value);
                    return Unsafe.As<Int40AsInt64, T>(ref tmp);
                }
            case ConverterKind.UInt40:
                {
                    var tmp = new UInt40AsInt64(value);
                    return Unsafe.As<UInt40AsInt64, T>(ref tmp);
                }
            case ConverterKind.Int48:
                {
                    var tmp = new Int48AsInt64(value);
                    return Unsafe.As<Int48AsInt64, T>(ref tmp);
                }
            case ConverterKind.UInt48:
                {
                    var tmp = new UInt48AsInt64(value);
                    return Unsafe.As<UInt48AsInt64, T>(ref tmp);
                }
            case ConverterKind.Int56:
                {
                    var tmp = new Int56AsInt64(value);
                    return Unsafe.As<Int56AsInt64, T>(ref tmp);
                }
            case ConverterKind.UInt56:
                {
                    var tmp = new UInt56AsInt64(value);
                    return Unsafe.As<UInt56AsInt64, T>(ref tmp);
                }
            default:
                throw new NotSupportedException(GetMessage());
        }
    }

    public static void CopyToInt64(ReadOnlySpan<T> source, Span<long> destination)
    {
        EnsureSupported();
        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination span is shorter than source span", nameof(destination));
        }

        switch (s_kind)
        {
            case ConverterKind.Int64:
                MemoryMarshal.Cast<T, long>(source).CopyTo(destination);
                break;
            case ConverterKind.Int32:
                Copy32BitToInt64(MemoryMarshal.Cast<T, int>(source), destination);
                break;
            case ConverterKind.UInt32:
                CopyUInt32ToInt64(MemoryMarshal.Cast<T, uint>(source), destination);
                break;
            case ConverterKind.Int16:
                Copy16BitToInt64(MemoryMarshal.Cast<T, short>(source), destination);
                break;
            case ConverterKind.UInt16:
                CopyUInt16ToInt64(MemoryMarshal.Cast<T, ushort>(source), destination);
                break;
            case ConverterKind.SByte:
                CopySByteToInt64(MemoryMarshal.Cast<T, sbyte>(source), destination);
                break;
            case ConverterKind.Byte:
                CopyByteToInt64(MemoryMarshal.Cast<T, byte>(source), destination);
                break;
            case ConverterKind.Int24:
                CopyOddToInt64(MemoryMarshal.Cast<T, Int24AsInt64>(source), destination);
                break;
            case ConverterKind.UInt24:
                CopyOddToInt64(MemoryMarshal.Cast<T, UInt24AsInt64>(source), destination);
                break;
            case ConverterKind.Int40:
                CopyOddToInt64(MemoryMarshal.Cast<T, Int40AsInt64>(source), destination);
                break;
            case ConverterKind.UInt40:
                CopyOddToInt64(MemoryMarshal.Cast<T, UInt40AsInt64>(source), destination);
                break;
            case ConverterKind.Int48:
                CopyOddToInt64(MemoryMarshal.Cast<T, Int48AsInt64>(source), destination);
                break;
            case ConverterKind.UInt48:
                CopyOddToInt64(MemoryMarshal.Cast<T, UInt48AsInt64>(source), destination);
                break;
            case ConverterKind.Int56:
                CopyOddToInt64(MemoryMarshal.Cast<T, Int56AsInt64>(source), destination);
                break;
            case ConverterKind.UInt56:
                CopyOddToInt64(MemoryMarshal.Cast<T, UInt56AsInt64>(source), destination);
                break;
            default:
                throw new NotSupportedException(GetMessage());
        }
    }

    public static void CopyFromInt64(ReadOnlySpan<long> source, Span<T> destination)
    {
        EnsureSupported();
        if (destination.Length < source.Length)
        {
            throw new ArgumentException("Destination span is shorter than source span", nameof(destination));
        }

        switch (s_kind)
        {
            case ConverterKind.Int64:
                MemoryMarshal.Cast<long, T>(source).CopyTo(destination);
                break;
            case ConverterKind.Int32:
                CopyInt64To32Bit(source, MemoryMarshal.Cast<T, int>(destination));
                break;
            case ConverterKind.UInt32:
                CopyInt64ToUInt32(source, MemoryMarshal.Cast<T, uint>(destination));
                break;
            case ConverterKind.Int16:
                CopyInt64To16Bit(source, MemoryMarshal.Cast<T, short>(destination));
                break;
            case ConverterKind.UInt16:
                CopyInt64ToUInt16(source, MemoryMarshal.Cast<T, ushort>(destination));
                break;
            case ConverterKind.SByte:
                CopyInt64ToSByte(source, MemoryMarshal.Cast<T, sbyte>(destination));
                break;
            case ConverterKind.Byte:
                CopyInt64ToByte(source, MemoryMarshal.Cast<T, byte>(destination));
                break;
            case ConverterKind.Int24:
                CopyInt64ToInt24(source, MemoryMarshal.Cast<T, Int24AsInt64>(destination));
                break;
            case ConverterKind.UInt24:
                CopyInt64ToUInt24(source, MemoryMarshal.Cast<T, UInt24AsInt64>(destination));
                break;
            case ConverterKind.Int40:
                CopyInt64ToInt40(source, MemoryMarshal.Cast<T, Int40AsInt64>(destination));
                break;
            case ConverterKind.UInt40:
                CopyInt64ToUInt40(source, MemoryMarshal.Cast<T, UInt40AsInt64>(destination));
                break;
            case ConverterKind.Int48:
                CopyInt64ToInt48(source, MemoryMarshal.Cast<T, Int48AsInt64>(destination));
                break;
            case ConverterKind.UInt48:
                CopyInt64ToUInt48(source, MemoryMarshal.Cast<T, UInt48AsInt64>(destination));
                break;
            case ConverterKind.Int56:
                CopyInt64ToInt56(source, MemoryMarshal.Cast<T, Int56AsInt64>(destination));
                break;
            case ConverterKind.UInt56:
                CopyInt64ToUInt56(source, MemoryMarshal.Cast<T, UInt56AsInt64>(destination));
                break;
            default:
                throw new NotSupportedException(GetMessage());
        }
    }

    private static ConverterKind DetermineKind()
    {
        if (typeof(T) == typeof(long)) return ConverterKind.Int64;
        if (typeof(T) == typeof(int)) return ConverterKind.Int32;
        if (typeof(T) == typeof(uint)) return ConverterKind.UInt32;
        if (typeof(T) == typeof(short)) return ConverterKind.Int16;
        if (typeof(T) == typeof(ushort)) return ConverterKind.UInt16;
        if (typeof(T) == typeof(sbyte)) return ConverterKind.SByte;
        if (typeof(T) == typeof(byte)) return ConverterKind.Byte;
        if (typeof(T) == typeof(Int24AsInt64)) return ConverterKind.Int24;
        if (typeof(T) == typeof(UInt24AsInt64)) return ConverterKind.UInt24;
        if (typeof(T) == typeof(Int40AsInt64)) return ConverterKind.Int40;
        if (typeof(T) == typeof(UInt40AsInt64)) return ConverterKind.UInt40;
        if (typeof(T) == typeof(Int48AsInt64)) return ConverterKind.Int48;
        if (typeof(T) == typeof(UInt48AsInt64)) return ConverterKind.UInt48;
        if (typeof(T) == typeof(Int56AsInt64)) return ConverterKind.Int56;
        if (typeof(T) == typeof(UInt56AsInt64)) return ConverterKind.UInt56;
        return ConverterKind.Unsupported;
    }

    private static void EnsureSupported()
    {
        if (!IsSupported)
        {
            throw new NotSupportedException(GetMessage());
        }
    }

    private static string GetMessage()
    {
        return $"Type {typeof(T)} is not supported for Int64 conversion.";
    }

    private static void Copy32BitToInt64(ReadOnlySpan<int> source, Span<long> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private static void CopyUInt32ToInt64(ReadOnlySpan<uint> source, Span<long> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private static void Copy16BitToInt64(ReadOnlySpan<short> source, Span<long> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private static void CopyUInt16ToInt64(ReadOnlySpan<ushort> source, Span<long> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private static void CopySByteToInt64(ReadOnlySpan<sbyte> source, Span<long> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private static void CopyByteToInt64(ReadOnlySpan<byte> source, Span<long> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private static void CopyOddToInt64<TOdd>(ReadOnlySpan<TOdd> source, Span<long> destination)
        where TOdd : struct
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = Int64Conversion<TOdd>.ToInt64(source[i]);
        }
    }

    private static void CopyInt64To32Bit(ReadOnlySpan<long> source, Span<int> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = checked((int)source[i]);
        }
    }

    private static void CopyInt64ToUInt32(ReadOnlySpan<long> source, Span<uint> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = checked((uint)source[i]);
        }
    }

    private static void CopyInt64To16Bit(ReadOnlySpan<long> source, Span<short> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = checked((short)source[i]);
        }
    }

    private static void CopyInt64ToUInt16(ReadOnlySpan<long> source, Span<ushort> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = checked((ushort)source[i]);
        }
    }

    private static void CopyInt64ToSByte(ReadOnlySpan<long> source, Span<sbyte> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = checked((sbyte)source[i]);
        }
    }

    private static void CopyInt64ToByte(ReadOnlySpan<long> source, Span<byte> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = checked((byte)source[i]);
        }
    }

    private static void CopyInt64ToInt24(ReadOnlySpan<long> source, Span<Int24AsInt64> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = new Int24AsInt64(source[i]);
        }
    }

    private static void CopyInt64ToUInt24(ReadOnlySpan<long> source, Span<UInt24AsInt64> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = new UInt24AsInt64(source[i]);
        }
    }

    private static void CopyInt64ToInt40(ReadOnlySpan<long> source, Span<Int40AsInt64> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = new Int40AsInt64(source[i]);
        }
    }

    private static void CopyInt64ToUInt40(ReadOnlySpan<long> source, Span<UInt40AsInt64> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = new UInt40AsInt64(source[i]);
        }
    }

    private static void CopyInt64ToInt48(ReadOnlySpan<long> source, Span<Int48AsInt64> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = new Int48AsInt64(source[i]);
        }
    }

    private static void CopyInt64ToUInt48(ReadOnlySpan<long> source, Span<UInt48AsInt64> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = new UInt48AsInt64(source[i]);
        }
    }

    private static void CopyInt64ToInt56(ReadOnlySpan<long> source, Span<Int56AsInt64> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = new Int56AsInt64(source[i]);
        }
    }

    private static void CopyInt64ToUInt56(ReadOnlySpan<long> source, Span<UInt56AsInt64> destination)
    {
        for (var i = 0; i < source.Length; i++)
        {
            destination[i] = new UInt56AsInt64(source[i]);
        }
    }
}
