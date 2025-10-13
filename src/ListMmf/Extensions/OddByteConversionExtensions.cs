using System;
using System.Buffers;

namespace BruSoftware.ListMmf;

public static class OddByteConversionExtensions
{
    public static void CopyAsInt64(this ListMmf<UInt24AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this ListMmf<Int24AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this ListMmf<UInt40AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this ListMmf<Int40AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this ListMmf<UInt48AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this ListMmf<Int48AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this ListMmf<UInt56AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this ListMmf<Int56AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this IReadOnlyList64Mmf<UInt24AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this IReadOnlyList64Mmf<Int24AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this IReadOnlyList64Mmf<UInt40AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this IReadOnlyList64Mmf<Int40AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this IReadOnlyList64Mmf<UInt48AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this IReadOnlyList64Mmf<Int48AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this IReadOnlyList64Mmf<UInt56AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static void CopyAsInt64(this IReadOnlyList64Mmf<Int56AsInt64> list, long start, Span<long> destination)
    {
        CopyAsInt64Internal(list, start, destination);
    }

    public static IMemoryOwner<long> RentAsInt64(this ListMmf<UInt24AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this ListMmf<Int24AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this ListMmf<UInt40AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this ListMmf<Int40AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this ListMmf<UInt48AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this ListMmf<Int48AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this ListMmf<UInt56AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this ListMmf<Int56AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this IReadOnlyList64Mmf<UInt24AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this IReadOnlyList64Mmf<Int24AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this IReadOnlyList64Mmf<UInt40AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this IReadOnlyList64Mmf<Int40AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this IReadOnlyList64Mmf<UInt48AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this IReadOnlyList64Mmf<Int48AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this IReadOnlyList64Mmf<UInt56AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    public static IMemoryOwner<long> RentAsInt64(this IReadOnlyList64Mmf<Int56AsInt64> list, long start, int length)
    {
        return RentAsInt64Internal(list, start, length);
    }

    private static void CopyAsInt64Internal<TOdd>(ListMmf<TOdd> list, long start, Span<long> destination)
        where TOdd : struct
    {
        ValidateRange(list.Count, start, destination.Length, nameof(destination));
        var source = list.AsSpan(start, destination.Length);
        ConvertRange(source, destination);
    }

    private static void CopyAsInt64Internal<TOdd>(IReadOnlyList64Mmf<TOdd> list, long start, Span<long> destination)
        where TOdd : struct
    {
        ValidateRange(list.Count, start, destination.Length, nameof(destination));
        var source = list.GetRange(start, destination.Length);
        ConvertRange(source, destination);
    }

    private static IMemoryOwner<long> RentAsInt64Internal<TOdd>(ListMmf<TOdd> list, long start, int length)
        where TOdd : struct
    {
        ValidateRange(list.Count, start, length, nameof(length));
        var owner = MemoryPool<long>.Shared.Rent(length);
        try
        {
            var span = owner.Memory.Span;
            var slice = span.Slice(0, length);
            CopyAsInt64Internal(list, start, slice);
            return new SlicedMemoryOwner<long>(owner, length);
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    private static IMemoryOwner<long> RentAsInt64Internal<TOdd>(IReadOnlyList64Mmf<TOdd> list, long start, int length)
        where TOdd : struct
    {
        ValidateRange(list.Count, start, length, nameof(length));
        var owner = MemoryPool<long>.Shared.Rent(length);
        try
        {
            var span = owner.Memory.Span;
            var slice = span.Slice(0, length);
            CopyAsInt64Internal(list, start, slice);
            return new SlicedMemoryOwner<long>(owner, length);
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    private static void ConvertRange<TOdd>(ReadOnlySpan<TOdd> source, Span<long> destination)
    {
        for (var i = 0; i < destination.Length; i++)
        {
            destination[i] = source[i];
        }
    }

    private static void ValidateRange(long count, long start, int length, string lengthParamName)
    {
        if (start < 0 || start > count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(lengthParamName);
        }

        var endExclusive = start + (long)length;
        if (endExclusive > count)
        {
            throw new ArgumentOutOfRangeException(lengthParamName);
        }
    }

    private sealed class SlicedMemoryOwner<T> : IMemoryOwner<T>
    {
        private IMemoryOwner<T>? _inner;
        private readonly int _length;

        public SlicedMemoryOwner(IMemoryOwner<T> inner, int length)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _length = length;
        }

        public Memory<T> Memory
        {
            get
            {
                var inner = _inner ?? throw new ObjectDisposedException(nameof(SlicedMemoryOwner<T>));
                return inner.Memory.Slice(0, _length);
            }
        }

        public void Dispose()
        {
            _inner?.Dispose();
            _inner = null;
        }
    }
}
