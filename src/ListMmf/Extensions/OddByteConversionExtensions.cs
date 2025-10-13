using System;
using System.Buffers;

namespace BruSoftware.ListMmf;

public static class OddByteConversionExtensions
{
    public static void CopyAsInt64<T>(this ListMmf<T> list, long start, Span<long> destination)
        where T : struct
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        ValidateRange(list.Count, start, destination.Length, nameof(destination));
        var source = list.AsSpan(start, destination.Length);
        Int64Conversion<T>.CopyToInt64(source, destination);
    }

    public static void CopyAsInt64<T>(this IReadOnlyList64Mmf<T> list, long start, Span<long> destination)
        where T : struct
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        ValidateRange(list.Count, start, destination.Length, nameof(destination));
        var source = list.AsSpan(start, destination.Length);
        Int64Conversion<T>.CopyToInt64(source, destination);
    }

    public static IMemoryOwner<long> RentAsInt64<T>(this ListMmf<T> list, long start, int length)
        where T : struct
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        return RentInternal(list.Count, start, length, () => list.AsSpan(start, length));
    }

    public static IMemoryOwner<long> RentAsInt64<T>(this IReadOnlyList64Mmf<T> list, long start, int length)
        where T : struct
    {
        if (list == null) throw new ArgumentNullException(nameof(list));
        return RentInternal(list.Count, start, length, () => list.AsSpan(start, length));
    }

    private static IMemoryOwner<long> RentInternal<T>(long count, long start, int length, Func<ReadOnlySpan<T>> sourceFactory)
        where T : struct
    {
        ValidateRange(count, start, length, nameof(length));
        var owner = MemoryPool<long>.Shared.Rent(length);
        try
        {
            var span = owner.Memory.Span.Slice(0, length);
            var source = sourceFactory();
            Int64Conversion<T>.CopyToInt64(source, span);
            return new SlicedMemoryOwner<long>(owner, length);
        }
        catch
        {
            owner.Dispose();
            throw;
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
