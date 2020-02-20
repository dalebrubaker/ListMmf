using System.Collections.Generic;

namespace BruSoftware.ListMmf
{
    public interface IReadOnlyCollection64<out T> : IEnumerable<T>
    {
        /// <summary>
        /// Number of items in the collections.
        /// </summary>
        long Count { get; }
    }
}
