﻿namespace BruSoftware.ListMmf
{
    public class ListMmfIdentifier
    {
        public string Name { get; }
        public int InstanceId { get; }

        public ListMmfIdentifier(string name, int instanceId)
        {
            Name = name;
            InstanceId = instanceId;
        }

        public override string ToString()
        {
            return $"#{InstanceId} {Name}";
        }
    }
}
