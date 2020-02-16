using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BruSoftware.ListMmf
{
    /// <summary>
    /// This base class is primarily (entirely) for DEBUG
    /// </summary>
    public class ListMmfBase : IDisposable
    {
        public string Name { get; }
        public static int s_nextInstanceId;
        private static readonly object s_lock = new object();
        private static readonly Dictionary<string, List<ListMmfIdentifier>> s_openListByName = new Dictionary<string, List<ListMmfIdentifier>>();
        private static readonly object s_dictLock = new object();

        protected readonly int _instanceId;

        public ListMmfIdentifier ListMmfIdentifier { get; }

        public ListMmfBase(string name)
        {
            Name = name;
            _instanceId = s_nextInstanceId++;
            ListMmfIdentifier = new ListMmfIdentifier(Name, _instanceId);
            AddMmfCreationToDictionary(ListMmfIdentifier);
        }

        private static void AddMmfCreationToDictionary(ListMmfIdentifier listMmfIdentifier)
        {
            lock (s_dictLock)
            {
                if (!s_openListByName.TryGetValue(listMmfIdentifier.Name, out var identifiers))
                {
                    identifiers = new List<ListMmfIdentifier>();
                    s_openListByName.Add(listMmfIdentifier.Name, identifiers);
                }
                identifiers.Add(listMmfIdentifier);

                //if (key.Contains("Timestamps.btdHdr"))
                //{
                //    s_logger.Debug($"Added {key} to dictionary, count={identifiers.Count}");
                //}
                //s_logger.Debug($"Added {mmfContainer.Identifier} to s_openListBySemaphoreUniqueName");
            }
        }
        private static void RemoveMmfCreationFromDictionary(ListMmfIdentifier listMmfIdentifier)
        {
            lock (s_dictLock)
            {
                if (!s_openListByName.TryGetValue(listMmfIdentifier.Name, out var identifiers))
                {
                    throw new ListMmfException($"Failed to find {listMmfIdentifier.Name} in dictionary");
                }
                var index = identifiers.FindIndex(x => x.Name == listMmfIdentifier.Name && x.InstanceId == listMmfIdentifier.InstanceId);
                if (index < 0)
                {
                    throw new ListMmfException($"Failed to find {listMmfIdentifier} in s_openListByName");
                }
                identifiers.RemoveAt(index);
                if (identifiers.Count == 0)
                {
                    s_openListByName.Remove(listMmfIdentifier.Name);
                }
            }
        }

        public static int GetOpenMmfContainersCount(FileStream fileStream = null, string mapName = null)
        {
            lock (s_dictLock)
            {
                if (string.IsNullOrEmpty(mapName) && fileStream == null)
                {
                    return 0;
                }
                var name = fileStream == null ? mapName : fileStream.Name;
                s_openListByName.TryGetValue(name, out var identifiers);
                return identifiers?.Count ?? -1;
            }
        }

        public static int GetOpenMmfContainersCountAll()
        {
            lock (s_dictLock)
            {
                var allValues = GetOpenMmfContainers();
                return allValues.Count;
            }
        }

        public static List<ListMmfIdentifier> GetOpenMmfContainers()
        {
            lock (s_dictLock)
            {
                var all = s_openListByName.Values.SelectMany(x => x).ToList();
                return all;
            }
        }
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                RemoveMmfCreationFromDictionary(ListMmfIdentifier);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
            return $" #{_instanceId}";
        }
    }
}
