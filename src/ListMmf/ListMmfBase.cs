using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace BruSoftware.ListMmf
{
    /// <summary>
    /// This base class is primarily (entirely) for DEBUG
    /// </summary>
    public class ListMmf : IDisposable
    {
#if DEBUG
        public static int s_nextInstanceId;
        private static readonly Dictionary<string, List<ListMmfIdentifier>> s_openListByName = new Dictionary<string, List<ListMmfIdentifier>>();
        private static readonly object s_dictLock = new object();

        protected readonly int _instanceId;

        public ListMmfIdentifier ListMmfIdentifier { get; }
#endif

        public ListMmf(string name)
        {
#if DEBUG
            _instanceId = s_nextInstanceId++;
            ListMmfIdentifier = new ListMmfIdentifier(name, _instanceId);
            AddListMmfCreationToDictionary(ListMmfIdentifier);
#endif
        }

#if DEBUG
        private static void AddListMmfCreationToDictionary(ListMmfIdentifier listMmfIdentifier)
        {
            lock (s_dictLock)
            {
                if (!s_openListByName.TryGetValue(listMmfIdentifier.Name, out var identifiers))
                {
                    identifiers = new List<ListMmfIdentifier>();
                    s_openListByName.Add(listMmfIdentifier.Name, identifiers);
                }
                identifiers.Add(listMmfIdentifier);
            }
        }

        private static void RemoveListMmfCreationFromDictionary(ListMmfIdentifier listMmfIdentifier)
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
                    throw new ListMmfException($"Failed to find {listMmfIdentifier} in dictionary");
                }
                identifiers.RemoveAt(index);
                if (identifiers.Count == 0)
                {
                    s_openListByName.Remove(listMmfIdentifier.Name);
                }
            }
        }

        public static int GetOpenInstancesCount(FileStream fileStream = null, string mapName = null)
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

        public static int GetOpenInstancesCountAll()
        {
            lock (s_dictLock)
            {
                var allValues = GetOpenInstances();
                return allValues.Count;
            }
        }

        public static List<ListMmfIdentifier> GetOpenInstances()
        {
            lock (s_dictLock)
            {
                var all = s_openListByName.Values.SelectMany(x => x).ToList();
                return all;
            }
        }
#endif

        public static bool IsAnyoneReading(string pathOrMapName)
        {
            var semaphoreUniqueName = GetSemaphoreUniqueName(pathOrMapName, true);
            var exists = SemaphoreNameExists(semaphoreUniqueName);
            return exists;
        }

        public static bool IsAnyoneWriting(string pathOrMapName)
        {
            var semaphoreUniqueName = GetSemaphoreUniqueName(pathOrMapName, false);
            var exists = SemaphoreNameExists(semaphoreUniqueName);
            return exists;
        } 
        
        public static bool SemaphoreNameExists(string semapahoreName)
        {
            var exists = Semaphore.TryOpenExisting(semapahoreName, out var semaphore);
            if (exists)
            {
                // We didn't really want to open one.
                semaphore.Dispose();
            }
            return exists;
        }

        protected static string GetSemaphoreUniqueName(string pathOrMapName, bool isReadOnly)
        {
            var cleanName = pathOrMapName.RemoveCharFromString(Path.DirectorySeparatorChar);
            cleanName = cleanName.RemoveCharFromString(',');
            cleanName = cleanName.RemoveCharFromString(' ');
            var prefix = isReadOnly ? "R-" : "W-";
            var result = $"Global\\{prefix}{cleanName}";
            if (result.Length > 260)
            {
                throw new ListMmfException($"Too long semaphore name, exceeds 260: {result}");
            }
            return result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
#if DEBUG
                RemoveListMmfCreationFromDictionary(ListMmfIdentifier);
#endif
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public override string ToString()
        {
#if DEBUG
            return $" #{_instanceId}";
#else
            return "";
#endif
        }
    }
}
