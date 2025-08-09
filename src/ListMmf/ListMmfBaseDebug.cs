using System;

namespace BruSoftware.ListMmf;

/// <summary>
/// This base class is to assist debugging.
/// It helps determine what ListMmf files have not been disposed when they should have been.
/// </summary>
public class ListMmfBaseDebug : IDisposable
{
    public static readonly Tracker Tracker = new();

    protected ListMmfBaseDebug(string name)
    {
        TrackerId = Tracker.Register(name); // Faster
    }

    protected TrackerId TrackerId { get; }

    protected int InstanceId => TrackerId?.Id ?? 0;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            Tracker.Deregister(TrackerId);
        }
    }
}