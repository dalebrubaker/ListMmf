// ReSharper disable once CheckNamespace
namespace BruSoftware.ListMmf;

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
}