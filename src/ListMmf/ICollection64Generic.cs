using System.Collections.Generic;

namespace BruSoftware.ListMmf
{
    public interface ICollection64<T> : IEnumerable<T>
    {
        /// <summary>
        /// Number of items in the collections.         
        /// </summary>
        long Count { get; }

        bool IsReadOnly { get; }

        void Add(T item);

        void Clear();

        bool Contains(T item);

        /// <summary>
        /// CopyTo copies a collection into an Array, starting at a particular index into the array.
        /// </summary>
        /// <param name="array"></param>
        /// <param name="arrayIndex"></param>
        void CopyTo(T[] array, int arrayIndex);

        //void CopyTo(int sourceIndex, T[] destinationArray, int destinationIndex, int count);

        bool Remove(T item);
    }
}
