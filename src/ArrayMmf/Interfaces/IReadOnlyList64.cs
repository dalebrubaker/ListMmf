using System.Collections.Generic;

namespace BruSoftware.ArrayMmf.Interfaces
{
    public interface IReadOnlyList64<T> : IEnumerable<T>
    {
        T this[long index] { get; }
        long Count { get; }
    }
}
