using System;

namespace BruSoftware.ListMmf
{
    /// <summary>
    /// Exception thrown when an operation requires int32 range support but the requested range exceeds int.MaxValue.
    /// This is typically thrown by Span-based operations which are limited to int32 lengths.
    /// </summary>
    public class ListMmfOnlyInt32SupportedException : NotSupportedException
    {
        public ListMmfOnlyInt32SupportedException()
            : base("Operation is limited to int32 range (maximum ~2.1 billion elements)")
        {
        }

        public ListMmfOnlyInt32SupportedException(string message) : base(message)
        {
        }

        public ListMmfOnlyInt32SupportedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public ListMmfOnlyInt32SupportedException(long requestedLength)
            : base($"Requested length {requestedLength:N0} exceeds int.MaxValue ({int.MaxValue:N0}). Span<T> operations are limited to int32 range.")
        {
        }
    }
}