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
    /// Gets a read-only span representing a range of elements starting at the specified index.
    /// This provides zero-copy access to the underlying memory-mapped data when possible,
    /// or efficient bulk conversion for wrapper implementations.
    /// </summary>
    /// <param name="start">The zero-based starting index of the range</param>
    /// <param name="length">The number of elements in the range</param>
    /// <returns>A ReadOnlySpan containing the specified range of elements</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start or length is invalid</exception>
    ReadOnlySpan<T> GetRange(long start, int length);

    /// <summary>
    /// Gets a read-only span from the specified start index to the end of the list.
    /// This provides zero-copy access to the underlying memory-mapped data when possible,
    /// or efficient bulk conversion for wrapper implementations.
    /// </summary>
    /// <param name="start">The zero-based starting index</param>
    /// <returns>A ReadOnlySpan from start to the end of the list</returns>
    /// <exception cref="ArgumentOutOfRangeException">If start is invalid</exception>
    ReadOnlySpan<T> GetRange(long start);
}