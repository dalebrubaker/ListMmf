using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

public interface IReadOnlyList64<out T> : IEnumerable<T>
{
    T this[long index] { get; }
    long Count { get; }
}