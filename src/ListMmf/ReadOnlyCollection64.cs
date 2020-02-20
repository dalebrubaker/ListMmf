using System;
using System.Collections;
using System.Collections.Generic;

namespace BruSoftware.ListMmf
{
    public class ReadOnlyCollection64<T> : IList64<T>, IList64, IReadOnlyList64<T>
    {
        private readonly IList64<T> _list;

        public ReadOnlyCollection64(IList64<T> list)
        {
            _list = list ?? throw new ArgumentNullException(nameof(list));
        }

        public long Count => _list.Count;

        public T this[long index] => _list[index];

        public bool Contains(T value)
        {
            return _list.Contains(value);
        }

        public void CopyTo(T[] array, int index)
        {
            _list.CopyTo(array, index);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public long IndexOf(T value)
        {
            return _list.IndexOf(value);
        }

        protected IList64<T> Items => _list;

        bool ICollection64<T>.IsReadOnly => true;

        T IList64<T>.this[long index]
        {
            get => _list[index];
            set => throw new NotSupportedException();
        }

        void ICollection64<T>.Add(T value)
        {
            throw new NotSupportedException();
        }

        void ICollection64<T>.Clear()
        {
            throw new NotSupportedException();
        }

        void IList64<T>.Insert(long index, T value)
        {
            throw new NotSupportedException();
        }

        bool ICollection64<T>.Remove(T value)
        {
            return false;
        }

        void IList64<T>.RemoveAt(long index)
        {
            throw new NotSupportedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)_list).GetEnumerator();
        }

        bool ICollection64.IsSynchronized => false;

        object ICollection64.SyncRoot => _list is ICollection64 coll ? coll.SyncRoot : this;

        void ICollection64.CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (array.Rank != 1)
            {
                throw new ArgumentException(nameof(array), "Multiple Dimensions Rank is not supported.");
            }

            if (array.GetLowerBound(0) != 0)
            {
                throw new ArgumentException(nameof(array), "Only 0 Lower Bound is supported.");
            }

            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "index cannont be negative.");
            }

            if (array.Length - index < Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Array is too small to fit starting at index.");
            }

            if (array is T[] items)
            {
                _list.CopyTo(items, index);
            }
            else
            {
                //
                // Catch the obvious case assignment will fail.
                // We can't find all possible problems by doing the check though.
                // For example, if the element type of the Array is derived from T,
                // we can't figure out if we can successfully copy the element beforehand.
                //
                Type targetType = array.GetType().GetElementType();
                Type sourceType = typeof(T);
                if (!(targetType.IsAssignableFrom(sourceType) || sourceType.IsAssignableFrom(targetType)))
                {
                    throw new ArgumentException(nameof(array), "Invalid array type.");
                }

                //
                // We can't cast array of value type to object[], so we don't support
                // widening of primitive types here.
                //
                object[] objects = array as object[];
                if (objects == null)
                {
                    throw new ArgumentException(nameof(array), "Invalid array type.");
                }

                var count = _list.Count;
                try
                {
                    for (var i = 0L; i < count; i++)
                    {
                        objects[index++] = _list[i];
                    }
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException(nameof(array), "Invalid array type.");
                }
            }
        }

        bool IList64.IsFixedSize => true;

        bool IList64.IsReadOnly => true;

        object IList64.this[long index]
        {
            get => _list[index];
            set => throw new NotSupportedException();
        }

        long IList64.Add(object value)
        {
            throw new NotSupportedException();
            return -1;
        }

        void IList64.Clear()
        {
            throw new NotSupportedException();
        }

        private static bool IsCompatibleObject(object value)
        {
            // Non-null values are fine.  Only accept nulls if T is a class or Nullable<U>.
            // Note that default(T) is not equal to null for value types except when T is Nullable<U>.
            return value is T || value == null && default(T) == null;
        }

        bool IList64.Contains(object value)
        {
            if (IsCompatibleObject(value))
            {
                return Contains((T)value);
            }
            return false;
        }

        long IList64.IndexOf(object value)
        {
            if (IsCompatibleObject(value))
            {
                return IndexOf((T)value);
            }
            return -1;
        }

        void IList64.Insert(long index, object value)
        {
            throw new NotSupportedException();
        }

        void IList64.Remove(object value)
        {
            throw new NotSupportedException();
        }

        void IList64.RemoveAt(long index)
        {
            throw new NotSupportedException();
        }
    }
}
