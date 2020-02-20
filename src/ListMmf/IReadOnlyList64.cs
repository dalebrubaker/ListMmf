namespace BruSoftware.ListMmf
{
    public interface IReadOnlyList64<T> : IReadOnlyCollection64<T>
    {
        T this[long index] { get; }
    }
}
