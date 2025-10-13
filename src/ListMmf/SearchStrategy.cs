namespace BruSoftware.ListMmf;

/// <summary>
/// Specifies the search algorithm strategy for binary search operations like LowerBound, UpperBound, and BinarySearch.
/// </summary>
public enum SearchStrategy
{
    /// <summary>
    /// Automatically detect if data is uniformly distributed and choose the best strategy.
    /// Uses interpolation search for uniform data (O(log log n)), binary search otherwise (O(log n)).
    /// Recommended for general use - adds minimal overhead and adapts to your data.
    /// </summary>
    Auto,

    /// <summary>
    /// Always use standard binary search (O(log n)).
    /// Reliable for all data distributions. Good default if auto-detection overhead is a concern.
    /// </summary>
    Binary,

    /// <summary>
    /// Always use interpolation search (O(log log n) for uniform data).
    /// Best for uniformly distributed data like daily trades over time.
    /// Can be slower than binary search for non-uniform data.
    /// </summary>
    Interpolation
}
