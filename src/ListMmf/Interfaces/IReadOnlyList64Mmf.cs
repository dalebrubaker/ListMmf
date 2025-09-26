// ReSharper disable once CheckNamespace
using System;

namespace BruSoftware.ListMmf;

/// <summary>
/// This class adds ReadUnchecked for much faster access to very large ListMmf lists
/// </summary>
/// <typeparam name="T"></typeparam>
public interface IReadOnlyList64Mmf<T> : IReadOnlyList64<T>
{
    /// <summary>
    /// This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
    /// e.g. are iterating (e.g. in a for loop)
    /// Benchmarking shows the compiler will optimize away this method
    /// </summary>
    /// <param name="index"></param>
    /// <returns></returns>
    T ReadUnchecked(long index);

    /// <summary>
    /// Provides the primary zero-copy slicing API for the memory-mapped list.
    /// Implementations should return a span view over the requested range without allocating
    /// whenever the underlying storage permits it.
    /// </summary>
    /// <param name="start">The zero-based starting index of the range.</param>
    /// <param name="length">The number of elements in the range.</param>
    /// <returns>A read-only span covering the requested range.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="start"/> or <paramref name="length"/> is invalid.</exception>
    ReadOnlySpan<T> AsSpan(long start, int length)
    {
        return GetRange(start, length);
    }

    /// <summary>
    /// Provides the primary zero-copy slicing API for the memory-mapped list.
    /// Implementations should return a span view from the requested start index to the end
    /// of the list without allocating whenever possible.
    /// </summary>
    /// <param name="start">The zero-based starting index.</param>
    /// <returns>A read-only span from <paramref name="start"/> to the end of the list.</returns>
    /// <exception cref="ArgumentOutOfRangeException">If <paramref name="start"/> is invalid.</exception>
    ReadOnlySpan<T> AsSpan(long start)
    {
        return GetRange(start);
    }

    /// <summary>
    /// Legacy alias retained for backward compatibility. Prefer <see cref="AsSpan(long,int)"/>.
    /// </summary>
    /// <remarks>This method will remain for existing callers but new code should use the AsSpan overloads.</remarks>
    /// <param name="start">The zero-based starting index of the range.</param>
    /// <param name="length">The number of elements in the range.</param>
    /// <returns>A read-only span covering the requested range.</returns>
    ReadOnlySpan<T> GetRange(long start, int length);

    /// <summary>
    /// Legacy alias retained for backward compatibility. Prefer <see cref="AsSpan(long)"/>.
    /// </summary>
    /// <remarks>This method will remain for existing callers but new code should use the AsSpan overloads.</remarks>
    /// <param name="start">The zero-based starting index.</param>
    /// <returns>A read-only span from <paramref name="start"/> to the end of the list.</returns>
    ReadOnlySpan<T> GetRange(long start);
}