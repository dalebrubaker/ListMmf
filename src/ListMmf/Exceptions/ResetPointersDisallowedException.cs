using System;

namespace BruSoftware.ListMmf.Exceptions;

/// <summary>
/// Exception thrown when ResetPointers is called after the ListMmf has been locked via DisallowResetPointers().
/// This prevents AccessViolationException when attempting to reset pointers after file capacity is locked.
/// </summary>
public class ResetPointersDisallowedException : ListMmfException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResetPointersDisallowedException"/> class with a default message.
    /// </summary>
    public ResetPointersDisallowedException()
        : base("ResetPointers is not allowed after the ListMmf has been locked.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetPointersDisallowedException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public ResetPointersDisallowedException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ResetPointersDisallowedException"/> class with a specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public ResetPointersDisallowedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}