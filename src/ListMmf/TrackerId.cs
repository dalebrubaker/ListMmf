using System;

namespace BruSoftware.ListMmf;

public class TrackerId
#if DEBUG
    : IComparable<TrackerId>
#endif
{
    public TrackerId(int id)
    {
        Id = id;
    }

    public int Id { get; }

#if DEBUG
    public string? Name { get; }
    public string? StackTrace { get; }

    public TrackerId(int id, string name, string? stackTrace) : this(id)
    {
        Name = name;
        StackTrace = stackTrace;
    }

    public int CompareTo(TrackerId? other)
    {
        return Id.CompareTo(other?.Id ?? 0);
    }

    public override string ToString()
    {
        return $"#{Id} {Name}";
    }
#endif
}