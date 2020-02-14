using System;
using System.Collections;

namespace BruSoftware.ListMmf
{
    public partial interface IList64 : ICollection64
    {

        /// <summary>
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        object this[long index] { get; set; }

        /// <summary>
        /// Adds an item to the list. The return value is the position the new element was inserted in.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        long Add(Object value);
    
        /// <summary>
        /// Returns whether the list contains a particular item.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        bool Contains(object value);
    
        /// <summary>
        /// Removes all items from the list.
        /// </summary>
        void Clear();

        bool IsReadOnly { get; }

        bool IsFixedSize { get; }

        
        /// <summary>
        /// From IList, but long.
        /// Returns the index of a particular item, if it is in the list.
        /// Returns -1 if the item isn't in the list.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        long IndexOf(object value);
    
        /// <summary>
        /// Inserts value into the list at position index.
        /// index must be non-negative and less than or equal to the 
        /// number of elements in the list.  If index equals the number
        /// of items in the list, then value is appended to the end.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="value"></param>
        void Insert(long index, object value);
    
        /// <summary>
        /// Removes an item from the list.
        /// </summary>
        /// <param name="value"></param>
        void Remove(object value);
    
        /// <summary>
        /// Removes the item at position index. 
        /// </summary>
        /// <param name="index"></param>
        void RemoveAt(long index);
    }
}