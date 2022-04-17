using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;

namespace BruSoftware.ListMmf
{
    /// <summary>
    ///     Wraps a smallest list that will hold the integer values underlying a given enum type
    /// </summary>
    public class SmallestEnumListMmf<T> : IListMmf<T>, IReadOnlyList64Mmf<T> where T : Enum
    {
        private readonly Type _enumType;
        private readonly SmallestInt64ListMmf _smallestInt64ListMmf;

        public SmallestEnumListMmf(Type enumType, string path, long capacity = 0, MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
        {
            _enumType = enumType;
            var enumValues = Enum.GetValues(enumType);
            var minValue = 0;
            var maxValue = 0;
            foreach (var enumValue in enumValues)
            {
                var value = (int)enumValue;
                if (value < minValue)
                {
                    minValue = value;
                }
                else if (value > maxValue)
                {
                    maxValue = value;
                }
            }
            var dataType = SmallestInt64ListMmf.GetSmallestInt64DataType(minValue, maxValue);
            _smallestInt64ListMmf = new SmallestInt64ListMmf(dataType, path, capacity, access);
        }

        public void Dispose()
        {
            _smallestInt64ListMmf.Dispose();
        }

        public long Count => _smallestInt64ListMmf.Count;

        public long Capacity
        {
            get => _smallestInt64ListMmf.Capacity;
            set => _smallestInt64ListMmf.Capacity = value;
        }

        public void Truncate(long newLength)
        {
            _smallestInt64ListMmf.Truncate(newLength);
        }

        public string Path => _smallestInt64ListMmf.Path;
        public int WidthBits => _smallestInt64ListMmf.WidthBits;
        public int Version => _smallestInt64ListMmf.Version;

        public DataType DataType => _smallestInt64ListMmf.DataType;

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            var en = _smallestInt64ListMmf.GetEnumerator();
            while (en.MoveNext())
            {
                T result;
                try
                {
                    var intValue = (int)en.Current;
                    result = (T)(object)intValue;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                yield return result;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _smallestInt64ListMmf.GetEnumerator();
        }

        public T this[long index]
        {
            get
            {
                var intValue = (int)_smallestInt64ListMmf[index];
                var result = (T)(object)intValue;
                return result;
            }
        }

        public void Add(T value)
        {
            var intValue = (int)(object)value;
            _smallestInt64ListMmf.Add(intValue);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            var list = new List<long>();
            foreach (var item in collection)
            {
                var intValue = (int)(object)item;
                list.Add(intValue);
            }
            _smallestInt64ListMmf.AddRange(list);
        }

        public void SetLast(T value)
        {
            var intValue = (int)(object)value;
            _smallestInt64ListMmf.SetLast(intValue);
        }

        public T ReadUnchecked(long index)
        {
            var intValue = (int)_smallestInt64ListMmf.ReadUnchecked(index);
            var result = (T)(object)intValue;
            return result;
        }
    }
}