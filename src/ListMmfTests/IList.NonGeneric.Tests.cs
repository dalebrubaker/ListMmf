// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;

namespace System.Collections.Tests
{
    /// <summary>
    /// Contains tests that ensure the correctness of any class that implements the nongeneric
    /// IList interface
    /// </summary>
    public abstract class IList_NonGeneric_Tests : ICollection_NonGeneric_Tests
    {
        #region IList Helper methods

        /// <summary>
        /// Creates an instance of an IList that can be used for testing.
        /// </summary>
        /// <returns>An instance of an IList that can be used for testing.</returns>
        protected abstract IList NonGenericIListFactory();

        /// <summary>
        /// Creates an instance of an IList that can be used for testing.
        /// </summary>
        /// <param name="count">The number of unique items that the returned IList contains.</param>
        /// <returns>An instance of an IList that can be used for testing.</returns>
        protected virtual IList NonGenericIListFactory(int count)
        {
            IList collection = NonGenericIListFactory();
            AddToCollection(collection, count);
            return collection;
        }

        protected virtual void AddToCollection(IList collection, int numberOfItemsToAdd)
        {
            int seed = 9600;
            while (collection.Count < numberOfItemsToAdd)
            {
                object toAdd = CreateT(seed++);
                while (collection.Contains(toAdd) || InvalidValues.Contains(toAdd))
                    toAdd = CreateT(seed++);
                collection.Add(toAdd);
            }
        }

        /// <summary>
        /// Creates an object that is dependent on the seed given. The object may be either
        /// a value type or a reference type, chosen based on the value of the seed.
        /// </summary>
        protected virtual object CreateT(int seed)
        {
            if (seed % 2 == 0)
            {
                int stringLength = seed % 10 + 5;
                Random rand = new Random(seed);
                byte[] bytes = new byte[stringLength];
                rand.NextBytes(bytes);
                return Convert.ToBase64String(bytes);
            }
            else
            {
                Random rand = new Random(seed);
                return rand.Next();
            }
        }

        protected virtual bool ExpectedFixedSize => false;

        protected virtual Type IList_NonGeneric_Item_InvalidIndex_ThrowType => typeof(ArgumentOutOfRangeException);

        protected virtual bool IList_NonGeneric_RemoveNonExistent_Throws => false;

        /// <summary>
        /// When calling Current of the enumerator after the end of the list and list is extended by new items.
        /// Tests are included to cover two behavioral scenarios:
        ///   - Throwing an InvalidOperationException
        ///   - Returning an undefined value.
        ///
        /// If this property is set to true, the tests ensure that the exception is thrown. The default value is
        /// the same as Enumerator_Current_UndefinedOperation_Throws.
        /// </summary>
        protected virtual bool IList_CurrentAfterAdd_Throws => Enumerator_Current_UndefinedOperation_Throws;

        #endregion

        #region ICollection Helper Methods

        protected override ICollection NonGenericICollectionFactory() => NonGenericIListFactory();

        protected override ICollection NonGenericICollectionFactory(int count) => NonGenericIListFactory(count);

        /// <summary>
        /// Returns a set of ModifyEnumerable delegates that modify the enumerable passed to them.
        /// </summary>
        protected override IEnumerable<ModifyEnumerable> GetModifyEnumerables(ModifyOperation operations)
        {
            if ((operations & ModifyOperation.Add) == ModifyOperation.Add)
            {
                yield return enumerable =>
                {
                    IList casted = (IList)enumerable;
                    if (!casted.IsFixedSize && !casted.IsReadOnly)
                    {
                        casted.Add(CreateT(2344));
                        return true;
                    }
                    return false;
                };
            }
            if ((operations & ModifyOperation.Insert) == ModifyOperation.Insert)
            {
                yield return enumerable =>
                {
                    IList casted = (IList)enumerable;
                    if (!casted.IsFixedSize && !casted.IsReadOnly)
                    {
                        casted.Insert(0, CreateT(12));
                        return true;
                    }
                    return false;
                };

                yield return enumerable =>
                {
                    IList casted = (IList)enumerable;
                    if (casted.Count > 0 && !casted.IsReadOnly)
                    {
                        casted[0] = CreateT(12);
                        return true;
                    }
                    return false;
                };
            }
            if ((operations & ModifyOperation.Remove) == ModifyOperation.Remove)
            {
                yield return enumerable =>
                {
                    IList casted = (IList)enumerable;
                    if (casted.Count > 0 && !casted.IsFixedSize && !casted.IsReadOnly)
                    {
                        casted.Remove(casted[0]);
                        return true;
                    }
                    return false;
                };
                yield return enumerable =>
                {
                    IList casted = (IList)enumerable;
                    if (casted.Count > 0 && !casted.IsFixedSize && !casted.IsReadOnly)
                    {
                        casted.RemoveAt(0);
                        return true;
                    }
                    return false;
                };
            }
            if ((operations & ModifyOperation.Clear) == ModifyOperation.Clear)
            {
                yield return enumerable =>
                {
                    IList casted = (IList)enumerable;
                    if (casted.Count > 0 && !casted.IsFixedSize && !casted.IsReadOnly)
                    {
                        casted.Clear();
                        return true;
                    }
                    return false;
                };
            }
        }

        protected override void AddToCollection(ICollection collection, int numberOfItemsToAdd) => AddToCollection((IList)collection, numberOfItemsToAdd);

        #endregion
    }
}
