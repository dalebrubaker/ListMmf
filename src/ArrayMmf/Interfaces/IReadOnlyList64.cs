using System.Collections.Generic;

namespace BruSoftware.ListMmf.Interfaces
{
    public interface IReadOnlyList64<T> : IEnumerable<T>
    {
        T this[long index] { get; }
        long Count { get; }
    }
}
