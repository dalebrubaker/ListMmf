using System;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

[Serializable]
public class OutOfOrderException : Exception
{
    public OutOfOrderException()
    {
    }

    public OutOfOrderException(string message)
        : base(message)
    {
    }

    public OutOfOrderException(string message, Exception inner)
        : base(message, inner)
    {
    }
}