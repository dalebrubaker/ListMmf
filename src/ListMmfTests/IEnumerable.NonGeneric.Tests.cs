// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace System.Collections.Tests
{
    /// <summary>
    /// Contains tests that ensure the correctness of any class that implements the nongeneric
    /// IEnumerable interface
    /// </summary>
    public abstract class IEnumerable_NonGeneric_Tests : TestBase
    {
        #region IEnumerable Helper Methods

        /// <summary>
        /// Creates an instance of an IEnumerable that can be used for testing.
        /// </summary>
        /// <param name="count">The number of unique items that the returned IEnumerable contains.</param>
        /// <returns>An instance of an IEnumerable that can be used for testing.</returns>
        protected abstract IEnumerable NonGenericIEnumerableFactory(int count);

        /// <summary>
        /// Modifies the given IEnumerable such that any enumerators for that IEnumerable will be
        /// invalidated.
        /// </summary>
        /// <param name="enumerable">An IEnumerable to modify</param>
        /// <returns>true if the enumerable was successfully modified. Else false.</returns>
        protected delegate bool ModifyEnumerable(IEnumerable enumerable);

        /// <summary>
        /// To be implemented in the concrete collections test classes. Returns a set of ModifyEnumerable delegates
        /// that modify the enumerable passed to them.
        /// </summary>
        protected abstract IEnumerable<ModifyEnumerable> GetModifyEnumerables(ModifyOperation operations);

        protected virtual ModifyOperation ModifyEnumeratorThrows => ModifyOperation.Add | ModifyOperation.Insert | ModifyOperation.Remove | ModifyOperation.Clear;

        protected virtual ModifyOperation ModifyEnumeratorAllowed => ModifyOperation.None;

        /// <summary>
        /// The Reset method is provided for COM interoperability. It does not necessarily need to be
        /// implemented; instead, the implementer can simply throw a NotSupportedException.
        ///
        /// If Reset is not implemented, this property must return False. The default value is true.
        /// </summary>
        protected virtual bool ResetImplemented => true;

        /// <summary>
        /// When calling Current of the enumerator before the first MoveNext, after the end of the collection,
        /// or after modification of the enumeration, the resulting behavior is undefined. Tests are included
        /// to cover two behavioral scenarios:
        ///   - Throwing an InvalidOperationException
        ///   - Returning an undefined value.
        ///
        /// If this property is set to true, the tests ensure that the exception is thrown. The default value is
        /// false.
        /// </summary>
        protected virtual bool Enumerator_Current_UndefinedOperation_Throws => false;

        /// <summary>
        /// Whether the collection can be serialized.
        /// </summary>
        protected virtual bool SupportsSerialization => true;

        /// <summary>
        /// Specifies whether this IEnumerable follows some sort of ordering pattern.
        /// </summary>
        protected virtual EnumerableOrder Order => EnumerableOrder.Sequential;

        /// <summary>
        /// An enum to allow specification of the order of the Enumerable. Used in validation for enumerables.
        /// </summary>
        protected enum EnumerableOrder
        {
            Unspecified,
            Sequential
        }

        #endregion
    }
}
