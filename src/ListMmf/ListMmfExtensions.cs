namespace BruSoftware.ListMmf;

public static class ListMmfExtensions
{
    public static IReadOnlyList64<T> ToReadOnlyList64<T>(this IReadOnlyList64Mmf<T> listMmf)
    {
        return new ReadOnlyList64MmfView<T>(listMmf, 0);
    }
}