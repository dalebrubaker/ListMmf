using System;
using System.Collections.Generic;
using System.Text;

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
