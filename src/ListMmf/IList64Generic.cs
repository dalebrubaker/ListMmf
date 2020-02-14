using System;
using System.Collections.Generic;
using System.Text;

namespace BruSoftware.ListMmf
{
    public partial interface IList64<T> : ICollection64<T>
    {
        /// <summary>
        /// The Item property provides methods to read and edit entries in the List.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        T this[long index] { get; set; }
    
        /// <summary>
        /// Returns the index of a particular item, if it is in the list.
        /// Returns -1 if the item isn't in the list.
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        long IndexOf(T item);
    
        /// <summary>
        /// Inserts value into the list at position index.
        /// index must be non-negative and less than or equal to the 
        /// number of elements in the list.  If index equals the number
        /// of items in the list, then value is appended to the end.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="item"></param>
        void Insert(long index, T item);
        
        /// <summary>
        /// Removes the item at position index.
        /// </summary>
        /// <param name="index"></param>
        void RemoveAt(long index);
    }
}
