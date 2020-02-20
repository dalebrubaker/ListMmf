using System;

namespace BruSoftware.ListMmf
{
    public interface IReadOnlyList64Disposable<T> : IReadOnlyCollection64<T>, IDisposable
    {
        T this[long index] { get; }
    }
}
