using System;
using System.Collections.Generic;

namespace BruSoftware.ListMmf
{
    public interface IListMmf<T> : IList64<T>, IDisposable where T : struct
    {
    }
}
