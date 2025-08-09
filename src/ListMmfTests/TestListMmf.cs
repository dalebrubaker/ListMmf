using System;
using System.IO;
using BruSoftware.ListMmf;

namespace ListMmfTests;

/// <summary>
/// This class makes a file-backed Mmf writer n the current directory with a Guid string for path and mapName.
/// The file is Deleted upon Dispose()
/// </summary>
/// <typeparam name="T"></typeparam>
public class TestListMmf<T> : ListMmf<T> where T : struct
{
    /// <summary>
    /// </summary>
    /// <param name="path"></param>
    /// <param name="dataType"></param>
    /// <param name="capacityItems"></param>
    public TestListMmf(string path, DataType dataType, long capacityItems = 0)
        : base(path, dataType, capacityItems)
    {
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var name = Path;
            base.Dispose(true);
            try
            {
                File.Delete(name);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            GC.SuppressFinalize(this);
        }
    }
}