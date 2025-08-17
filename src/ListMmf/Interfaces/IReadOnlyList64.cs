using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

/// <summary>
/// Represents a read-only list with <see cref="long"/> indices.
/// </summary>
/// <typeparam name="T">The type of elements in the list.</typeparam>
public interface IReadOnlyList64<out T> : IEnumerable<T>
{
    /// <summary>
    /// Gets the element at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the element to retrieve.</param>
    T this[long index] { get; }

    /// <summary>
    /// Gets the number of elements in the list.
    /// </summary>
    long Count { get; }
}