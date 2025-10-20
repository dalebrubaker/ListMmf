using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;

namespace BruSoftware.ListMmf;

/// <summary>
/// Wraps a smallest list that will hold the integer values underlying a given enum type
/// </summary>
public class SmallestEnumListMmf<T> : IListMmf<T>, IReadOnlyList64Mmf<T> where T : Enum
{
    private readonly Type _enumType;
    private readonly SmallestInt64ListMmf _smallestInt64ListMmf;

    public SmallestEnumListMmf(Type enumType, string path, long capacity = 0, bool isReadOnly = false)
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
        var dataType = DataTypeUtils.GetSmallestInt64DataType(minValue, maxValue);
        _smallestInt64ListMmf = new SmallestInt64ListMmf(dataType, path, capacity, isReadOnly: isReadOnly);
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

    public void Truncate(long newCount)
    {
        _smallestInt64ListMmf.Truncate(newCount);
    }

    public bool IsResetPointersDisallowed => _smallestInt64ListMmf?.IsResetPointersDisallowed ?? false;

    public void DisallowResetPointers()
    {
        _smallestInt64ListMmf?.DisallowResetPointers();
    }

    public void TruncateBeginning(long newCount, IProgress<long>? progress = null)
    {
        _smallestInt64ListMmf.TruncateBeginning(newCount, progress);
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

    public ReadOnlySpan<T> AsSpan(long start, int length)
    {
        // Get the underlying long data in bulk and convert to enum
        var longSpan = _smallestInt64ListMmf.AsSpan(start, length);
        var result = new T[length];
        for (int i = 0; i < length; i++)
        {
            var intValue = (int)longSpan[i];
            result[i] = (T)(object)intValue;
        }
        return result;
    }

    public ReadOnlySpan<T> AsSpan(long start)
    {
        var count = Count;
        if (start < 0 || start >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }
        var length = (int)(count - start);
        return AsSpan(start, length);
    }

    public ReadOnlySpan<T> GetRange(long start, int length)
    {
        return AsSpan(start, length);
    }

    public ReadOnlySpan<T> GetRange(long start)
    {
        return AsSpan(start);
    }

    public override string ToString()
    {
        return $"{_enumType} over {_smallestInt64ListMmf}";
    }
}