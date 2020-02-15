using System;
using System.Runtime.Serialization;

namespace BruSoftware.ListMmf
{
    [Serializable]
    public class ListMmfException : Exception
    {
        public ListMmfException()
        {
        }

        public ListMmfException(string message)
            : base(message)
        {
        }

        public ListMmfException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected ListMmfException(SerializationInfo serializationInfo, StreamingContext streamingContext)
        {
            throw new NotImplementedException();
        }
    }
}
