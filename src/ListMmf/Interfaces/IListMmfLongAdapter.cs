using System;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

/// <summary>
/// Represents an adapter that wraps a memory-mapped list of any supported numeric type
/// and exposes it as a list of long values with data type utilization monitoring.
/// </summary>
/// <typeparam name="T">The underlying storage type (must be a supported numeric struct type).</typeparam>
public interface IListMmfLongAdapter<T> : IListMmf<long>
    where T : struct
{
    /// <summary>
    /// Provides zero-copy access to a range of elements as a span.
    /// </summary>
    ReadOnlySpan<long> AsSpan(long start, int length);

    /// <summary>
    /// Provides zero-copy access from a starting index to the end as a span.
    /// </summary>
    ReadOnlySpan<long> AsSpan(long start);

    /// <summary>
    /// Gets the current data type utilization statistics based on observed min/max values.
    /// This method scans the entire list if the observed range has not been initialized.
    /// </summary>
    /// <returns>A status object containing utilization ratio and observed/allowed ranges.</returns>
    ListMmfLongAdapter<T>.DataTypeUtilizationStatus GetDataTypeUtilization();

    /// <summary>
    /// Configures a warning callback that fires once when utilization exceeds the specified threshold.
    /// The warning will only fire once until the observed range is invalidated (e.g., by truncation).
    /// </summary>
    /// <param name="threshold">The utilization threshold (0.0 to 1.0) at which to trigger the warning.</param>
    /// <param name="callback">The callback to invoke when the threshold is exceeded.</param>
    /// <exception cref="ArgumentOutOfRangeException">If threshold is not between 0 and 1.</exception>
    /// <exception cref="ArgumentNullException">If callback is null.</exception>
    void ConfigureUtilizationWarning(double threshold, Action<ListMmfLongAdapter<T>.DataTypeUtilizationStatus> callback);
}
