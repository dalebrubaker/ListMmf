using System;
using System.Collections.Generic;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

public interface IListMmf<T> : IListMmf, IEnumerable<T>
{
    T this[long index] { get; }

    /// <summary>
    /// Adds the given object to the end of this list. The size of the list is
    /// increased by one. If required, the capacity of the list is doubled
    /// before adding the new element.
    /// </summary>
    /// <param name="value"></param>
    void Add(T value);

    /// <summary>
    /// Adds the items of the given collection to the end of this array.
    /// If required, the capacity of this array is increased before adding the new items.
    /// </summary>
    /// <param name="collection"></param>
    /// <exception cref="ListMmfException">if list won't fit</exception>
    void AddRange(IEnumerable<T> collection);

    /// <summary>
    /// Sets the last item (Count - 1) to value
    /// This class only allows writing to the last item in the list, or Add() or AddRange() to append.
    /// </summary>
    /// <param name="value"></param>
    void SetLast(T value);
}

public interface IListMmf : IDisposable
{
    long Count { get; }

    /// <summary>
    /// The capacity of this ListMmf (number of items). Setting this can increase or decrease the size of the file.
    /// </summary>
    long Capacity { get; set; }

    /// <summary>
    /// The file path
    /// </summary>
    string Path { get; }

    /// <summary>
    /// The size of this fiile (number of bytes times 8, or 1 for BitArray)
    /// </summary>
    int WidthBits { get; }

    /// <summary>
    /// The version, first 4 bytes of each ListMmf file
    /// </summary>
    int Version { get; }

    /// <summary>
    /// The data type, second 4 bytes of each ListMmf file
    /// </summary>
    DataType DataType { get; }

    /// <summary>
    /// Truncate Count to newCount elements.
    /// This method only reduces Count, not Capacity
    /// </summary>
    /// <param name="newCount"></param>
    /// <exception cref="NotSupportedException">The array is read-only.</exception>
    void Truncate(long newCount);

    /// <summary>
    /// Gets a value indicating whether ResetPointers has been disallowed to prevent AccessViolationException.
    /// </summary>
    bool IsResetPointersDisallowed { get; }

    /// <summary>
    /// Disallows future calls to ResetPointers() to prevent AccessViolationException.
    /// This should be called when the file capacity is locked and should no longer grow.
    /// </summary>
    void DisallowResetPointers();

    /// <summary>
    /// Remove elements from the beginning of the file by moving data to the left, then reset Count to newLength.
    /// Capacity is not changed
    /// </summary>
    /// <param name="newCount"></param>
    /// <param name="progress"></param>
    void TruncateBeginning(long newCount, IProgress<long> progress = null);
}