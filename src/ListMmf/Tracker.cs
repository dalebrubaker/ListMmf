using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace BruSoftware.ListMmf;

/// <summary>
/// Maintain a dictionary of Id/String pairs.
/// One use for this class is as a static instance in an IDisposable class.
/// Register an InstanceId/Name pair in the ctor and de-register it in Dispose().
/// Then at Process Exit, ensure there are no remaining instances in this Tracker.
/// </summary>
public class Tracker
{
    private static int s_nextId;
    private readonly object _lock = new();
#if DEBUG
    private readonly Dictionary<int, TrackerId> _trackerIdsById = new();
#endif

    public TrackerId Register(string name, string stackTrace = null)
    {
        lock (_lock)
        {
            var id = s_nextId++;
#if DEBUG
            var trackerId = new TrackerId(id, name, stackTrace);
            _trackerIdsById.Add(trackerId.Id, trackerId);
            return trackerId;
#else
            var trackerId = new TrackerId(id);
            return trackerId;
#endif
        }
    }

    public void Deregister(TrackerId trackerId)
    {
#if DEBUG
        lock (_lock)
        {
            _trackerIdsById.Remove(trackerId.Id);
        }
#endif
    }

    public int GetOpenInstancesCount(string name)
    {
#if DEBUG
        lock (_lock)
        {
            var list = _trackerIdsById.Select(x => x.Value.Name == name);
            return list.Count();
        }
#else
        return 0;
#endif
    }

    public int GetOpenInstancesCountAll()
    {
#if DEBUG
        lock (_lock)
        {
            return _trackerIdsById.Count;
        }
#else
        return 0;
#endif
    }

    public List<TrackerId> GetOpenInstances()
    {
#if DEBUG
        lock (_lock)
        {
            var all = _trackerIdsById.Values.ToList();
            all.Sort();
            return all;
        }
#else
        return new List<TrackerId>();
#endif
    }
}