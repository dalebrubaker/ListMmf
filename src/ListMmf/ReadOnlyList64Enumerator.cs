using System;
using System.Collections;
using System.Collections.Generic;

namespace BruSoftware.ListMmf;

/// <summary>
/// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
/// </summary>
/// <typeparam name="T"></typeparam>
[Serializable]
public struct ReadOnlyList64Enumerator<T> : IEnumerator<T> where T : struct
{
    private readonly IReadOnlyList64<T> _list;
    private long _index;

    public ReadOnlyList64Enumerator(IReadOnlyList64<T> list)
    {
        _list = list;
        _index = 0;
        Current = default;
    }

    /// <summary>
    /// This is a buffered read-only list. No checking is made that writes were not made during the enumeration.
    /// </summary>
    /// <returns></returns>
    public bool MoveNext()
    {
        var localList = _list;
        if ((uint)_index < (uint)localList.Count)
        {
            Current = localList[_index];
            _index++;
            return true;
        }
        return MoveNextRare();
    }

    private bool MoveNextRare()
    {
        _index = _list.Count + 1;
        Current = default;
        return false;
    }

    public T Current { get; private set; }

    object IEnumerator.Current
    {
        get
        {
            if (_index == 0 || _index == _list.Count + 1)
            {
                throw new InvalidOperationException("Enum Op Cant Happen");
            }
            return Current;
        }
    }

    void IEnumerator.Reset()
    {
        _index = 0;
        Current = default;
    }

    public void Dispose()
    {
    }
}