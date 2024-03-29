using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

public interface IListMmf<T> : IListMmf, IEnumerable<T>
{
    T this[long index] { get; }

    /// <summary>
    ///     Adds the given object to the end of this list. The size of the list is
    ///     increased by one. If required, the capacity of the list is doubled
    ///     before adding the new element.
    /// </summary>
    /// <param name="value"></param>
    void Add(T value);

    /// <summary>
    ///     Adds the items of the given collection to the end of this array.
    ///     If required, the capacity of this array is increased before adding the new items.
    /// </summary>
    /// <param name="collection"></param>
    /// <exception cref="ListMmfException">if list won't fit</exception>
    void AddRange(IEnumerable<T> collection);

    /// <summary>
    ///     Sets the last item (Count - 1) to value
    ///     This class only allows writing to the last item in the list, or Add() or AddRange() to append.
    /// </summary>
    /// <param name="value"></param>
    void SetLast(T value);
}

public interface IListMmf : IDisposable
{
    long Count { get; }

    /// <summary>
    ///     The capacity of this ListMmf (number of items). Setting this can increase or decrease the size of the file.
    /// </summary>
    long Capacity { get; set; }

    /// <summary>
    ///     The file path
    /// </summary>
    string Path { get; }

    /// <summary>
    ///     The size of this fiile (number of bytes times 8, or 1 for BitArray)
    /// </summary>
    int WidthBits { get; }

    /// <summary>
    ///     The version, first 4 bytes of each ListMmf file
    /// </summary>
    int Version { get; }

    /// <summary>
    ///     The data type, second 4 bytes of each ListMmf file
    /// </summary>
    DataType DataType { get; }

    /// <summary>
    ///     Truncate Length to newCapacity elements.
    ///     If no other writer or reader is accessing the file, this also reduces Capacity, the size of the file.
    ///     This method is only allowed for the Writer, not the Reader.
    /// </summary>
    /// <param name="newLength"></param>
    /// <exception cref="NotSupportedException">The array is read-only.</exception>
    void Truncate(long newLength);
}