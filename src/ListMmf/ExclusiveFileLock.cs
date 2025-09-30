using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace BruSoftware.ListMmf;

/// <summary>
/// Cross-platform exclusive file lock.
/// Windows: Uses native FileShare.None locking on the data file (no lock files).
/// macOS/Linux: Uses a sibling lock file ("&lt;data&gt;.lock") with atomic acquire via FileMode.CreateNew.
/// - Stale lock detection on macOS/Linux using PID and process start time
/// </summary>
public sealed class ExclusiveFileLock : IDisposable
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private FileStream _lockStream;
    private FileStream _dataLockStream;

    private ExclusiveFileLock(string dataFilePath)
    {
        DataFilePath = dataFilePath;
        LockFilePath = IsWindows ? null : dataFilePath + ".lock";
    }

    public string DataFilePath { get; }
    public string LockFilePath { get; }
    public Guid LockId { get; private set; }
    public int OwnerPid { get; private set; }
    public DateTimeOffset OwnerPidStartTimeUtc { get; private set; }

    public static async Task<ExclusiveFileLock> AcquireAsync(
        string dataFilePath,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        bool alsoLockDataFile = false,
        CancellationToken cancellationToken = default)
    {
        timeout ??= TimeSpan.FromSeconds(15);
        pollInterval ??= TimeSpan.FromMilliseconds(200);

        var locker = new ExclusiveFileLock(dataFilePath);
        locker.LockId = Guid.NewGuid();

        var me = Process.GetCurrentProcess();
        locker.OwnerPid = me.Id;
        DateTimeOffset startTimeUtc;
        try
        {
            startTimeUtc = me.StartTime.ToUniversalTime();
        }
        catch
        {
            startTimeUtc = DateTimeOffset.UtcNow;
        }
        locker.OwnerPidStartTimeUtc = startTimeUtc;

        if (IsWindows)
        {
            return await AcquireWindowsAsync(locker, timeout.Value, pollInterval.Value, alsoLockDataFile, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            return await AcquireUnixAsync(locker, timeout.Value, pollInterval.Value, alsoLockDataFile, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<ExclusiveFileLock> AcquireWindowsAsync(
        ExclusiveFileLock locker,
        TimeSpan timeout,
        TimeSpan pollInterval,
        bool alsoLockDataFile,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                locker._dataLockStream = new FileStream(
                    locker.DataFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.Read);

                return locker;
            }
            catch (IOException)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    throw new TimeoutException($"Timed out waiting for lock: {locker.DataFilePath}");
                }
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task<ExclusiveFileLock> AcquireUnixAsync(
        ExclusiveFileLock locker,
        TimeSpan timeout,
        TimeSpan pollInterval,
        bool alsoLockDataFile,
        CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                locker._lockStream = new FileStream(
                    locker.LockFilePath,
                    FileMode.CreateNew,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite);

                await locker.WriteMetadataAsync(cancellationToken).ConfigureAwait(false);

                if (alsoLockDataFile)
                {
                    locker._dataLockStream = new FileStream(
                        locker.DataFilePath,
                        FileMode.OpenOrCreate,
                        FileAccess.Read,
                        FileShare.Read);
                }

                return locker;
            }
            catch (IOException)
            {
                try
                {
                    var existing = new FileStream(
                        locker.LockFilePath,
                        FileMode.Open,
                        FileAccess.ReadWrite,
                        FileShare.ReadWrite);

                    var meta = await ReadMetadataAsync(existing, cancellationToken).ConfigureAwait(false);
                    var stale = IsStale(meta);

                    if (!stale)
                    {
                        existing.Dispose();
                        if (DateTime.UtcNow >= deadline)
                        {
                            throw new TimeoutException($"Timed out waiting for lock: {locker.LockFilePath}");
                        }
                        await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    existing.SetLength(0);
                    await WriteMetadataAsync(existing, locker, cancellationToken).ConfigureAwait(false);
                    locker._lockStream = existing;

                    if (alsoLockDataFile)
                    {
                        locker._dataLockStream = new FileStream(
                            locker.DataFilePath,
                            FileMode.OpenOrCreate,
                            FileAccess.Read,
                            FileShare.Read);
                    }

                    return locker;
                }
                catch (FileNotFoundException)
                {
                }
            }

            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"Timed out waiting for lock: {locker.LockFilePath}");
            }

            await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        try
        {
            _dataLockStream?.Dispose();
            _dataLockStream = null;
        }
        catch { }

        if (!IsWindows)
        {
            try
            {
                _lockStream?.Dispose();
                _lockStream = null;
            }
            catch { }

            try
            {
                if (File.Exists(LockFilePath))
                {
                    File.Delete(LockFilePath);
                }
            }
            catch { }
        }
    }

    private async Task WriteMetadataAsync(CancellationToken ct)
    {
        if (_lockStream == null)
        {
            throw new InvalidOperationException("Lock stream is null");
        }
        await WriteMetadataAsync(_lockStream, this, ct).ConfigureAwait(false);
    }

    private static async Task WriteMetadataAsync(FileStream stream, ExclusiveFileLock owner, CancellationToken ct)
    {
        var meta = new LockMetadata
        {
            Pid = owner.OwnerPid,
            PidStartTimeUtc = owner.OwnerPidStartTimeUtc,
            TimestampUtc = DateTimeOffset.UtcNow,
            Hostname = GetHostNameSafe(),
            User = Environment.UserName,
            LockId = owner.LockId,
            DataFilePath = owner.DataFilePath
        };

        stream.Position = 0;
        stream.SetLength(0);
        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = false });
        var bytes = Encoding.UTF8.GetBytes(json);
        await stream.WriteAsync(bytes.AsMemory(0, bytes.Length), ct).ConfigureAwait(false);
        stream.Flush(true);
    }

    private static async Task<LockMetadata> ReadMetadataAsync(FileStream stream, CancellationToken ct)
    {
        try
        {
            stream.Position = 0;
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
            var json = Encoding.UTF8.GetString(ms.ToArray());
            return JsonSerializer.Deserialize<LockMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsStale(LockMetadata meta)
    {
        if (meta == null)
        {
            return true;
        }
        try
        {
            var proc = Process.GetProcessById(meta.Pid);
            DateTimeOffset startUtc;
            try
            {
                startUtc = proc.StartTime.ToUniversalTime();
            }
            catch
            {
                return true;
            }
            return startUtc != meta.PidStartTimeUtc || proc.HasExited;
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch
        {
            return DateTimeOffset.UtcNow - meta.TimestampUtc > TimeSpan.FromDays(1);
        }
    }


    private static string GetHostNameSafe()
    {
        try { return Dns.GetHostName(); }
        catch { return Environment.MachineName; }
    }

    private sealed class LockMetadata
    {
        public int Pid { get; set; }
        public DateTimeOffset PidStartTimeUtc { get; set; }
        public DateTimeOffset TimestampUtc { get; set; }
        public string Hostname { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public Guid LockId { get; set; }
        public string DataFilePath { get; set; } = string.Empty;
    }
}