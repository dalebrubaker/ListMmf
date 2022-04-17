// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf
{
    /// <summary>
    /// This class adds ReadUnchecked for much faster access to very large ListMmf lists
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IReadOnlyList64Mmf<out T> : IReadOnlyList64<T>
    {
        /// <summary>
        /// This is only safe to use when you have cached Count locally and you KNOW that you are in the range from 0 to Count
        ///     e.g. are iterating (e.g. in a for loop)
        /// Benchmarking shows the compiler will optimize away this method
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        T ReadUnchecked(long index);
    }
}