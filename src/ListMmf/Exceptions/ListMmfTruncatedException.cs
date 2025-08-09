using System;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

[Serializable]
public class ListMmfTruncatedException : Exception
{
    public ListMmfTruncatedException()
    {
    }

    public ListMmfTruncatedException(string message)
        : base(message)
    {
    }

    public ListMmfTruncatedException(string message, Exception inner)
        : base(message, inner)
    {
    }
}