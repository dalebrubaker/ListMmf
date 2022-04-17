using System;
using System.Runtime.Serialization;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf
{
    [Serializable]
    public class ReadWriteNotAvailableException : Exception
    {
        public ReadWriteNotAvailableException()
        {
        }

        public ReadWriteNotAvailableException(string message)
            : base(message)
        {
        }

        public ReadWriteNotAvailableException(string message, Exception inner)
            : base(message, inner)
        {
        }

        // Ensure Exception is Serializable
        protected ReadWriteNotAvailableException(SerializationInfo info, StreamingContext ctxt)
            : base(info, ctxt)
        {
        }
    }
}