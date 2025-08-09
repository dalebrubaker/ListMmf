using System;

namespace BruSoftware.ListMmf;

public class ListMmfIdentifier
#if DEBUG
    : IComparable<ListMmfIdentifier>
#endif
{
#if DEBUG
    public string Name { get; }
    public int InstanceId { get; }

    public ListMmfIdentifier(string name, int instanceId)
    {
        Name = name;
        InstanceId = instanceId;
    }

    public int CompareTo(ListMmfIdentifier other)
    {
        return InstanceId.CompareTo(other.InstanceId);
    }

    public override string ToString()
    {
        return $"#{InstanceId} {Name}";
    }
#endif
}