using System.Threading;

// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

/// <summary>
/// Provides progress reporting and cancellation support for long-running operations.
/// </summary>
public interface IProgressReport
{
    /// <summary>
    /// Gets a value indicating whether the user has cancelled the operation
    /// </summary>
    bool IsCancelled { get; }

    /// <summary>
    /// PercentDone is based on (countDone -  baseIndex) / (countTotal - baseIndex)
    /// </summary>
    /// <param name="countTotal">The total number of items for reporting progress, but we report the values from baseIndex up to countTotal</param>
    /// <param name="taskDescription">optional</param>
    /// <param name="baseIndex">The starting point for the reporting Must be smaller than countTotal. Default 0</param>
    void Begin(long countTotal, string taskDescription = "", long baseIndex = 0);

    /// <summary>
    /// Returns <c>true</c> if the user has cancelled. indexDone is 1 less than the count done.
    /// </summary>
    /// <param name="indexDone"></param>
    /// <returns></returns>
    bool Update(long indexDone);

    /// <summary>
    /// Notifies that the progress has ended
    /// </summary>
    /// <param name="countFinal">the final count</param>
    /// <param name="stopMessage">optional</param>
    void End(long countFinal, string stopMessage = "");

    /// <summary>
    /// Log a message to the progress display (e.g. status text below the progress bar).
    /// </summary>
    void LogMessage(string message) { }

    /// <summary>
    /// Update the task description text shown in the progress display.
    /// </summary>
    void UpdateDescription(string description) { }

    /// <summary>
    /// Cancellation token that is signaled when the user cancels via the progress dialog.
    /// Default returns <see cref="CancellationToken.None"/> for implementations that don't support it.
    /// </summary>
    CancellationToken Token => CancellationToken.None;
}