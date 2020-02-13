using System;
using System.Runtime.Serialization;

namespace BruSoftware.ArrayMmf
{
    [Serializable]
    public class MmfException : Exception
    {
        public MmfException()
        {
        }

        public MmfException(string message)
            : base(message)
        {
        }

        public MmfException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected MmfException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
            throw new NotImplementedException();
        }
    }
}
