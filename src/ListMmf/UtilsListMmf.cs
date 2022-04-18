using System;
using System.IO;
using System.IO.MemoryMappedFiles;

namespace BruSoftware.ListMmf
{
    /// <summary>
    ///     Some utility helpers
    /// </summary>
    public static class UtilsListMmf
    {
        // We use unspecified/local, NOT Utc internally. It is just too slow to continually convert to local time
        private static readonly DateTime s_unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);

        public static FileStream CreateFileStreamFromPath(string path, MemoryMappedFileAccess access)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ListMmfException("A path is required.");
            }
            var fileMode = access == MemoryMappedFileAccess.ReadWrite ? FileMode.OpenOrCreate : FileMode.Open;
            var fileAccess = access == MemoryMappedFileAccess.ReadWrite ? FileAccess.ReadWrite : FileAccess.Read;
            var fileShare = access == MemoryMappedFileAccess.ReadWrite ? FileShare.Read : FileShare.ReadWrite;
            if (access == MemoryMappedFileAccess.Read && !File.Exists(path))
            {
                throw new ListMmfException($"Attempt to read non-existing file: {path}");
            }
            if (access == MemoryMappedFileAccess.ReadWrite)
            {
                // Create the directory if needed
                var directory = Path.GetDirectoryName(path);
                if (!Directory.Exists(directory) && !string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
            var result = new FileStream(path, fileMode, fileAccess, fileShare);
            return result;
        }

        public static (int version, DataType dataType, long count) GetHeaderInfo(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return (-1, DataType.Empty, 0);
                }
                using var fs = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var br = new BinaryReader(fs);
                var version = br.ReadInt32();
                var dataType = (DataType)br.ReadInt32();
                var count = br.ReadInt64();
                return (version, dataType, count);
            }
            catch (Exception e)
            {
                var msg = e.Message;
                throw;
            }
        }

        /// <summary>
        ///     Convert UNIX DateTime to Windows DateTime. Using int instead of long becsuse we are storing 4-bytes seconds.
        /// </summary>
        /// <param name="unixSeconds">time in seconds since Epoch</param>
        public static DateTime FromUnixSecondsToDateTime(this int unixSeconds)
        {
            if (unixSeconds == 0 || unixSeconds == int.MinValue)
            {
                return DateTime.MinValue;
            }
            if (unixSeconds == int.MaxValue)
            {
                return DateTime.MaxValue;
            }
            return s_unixEpoch.AddSeconds(unixSeconds);
        }

        /// <summary>
        ///     Convert Windows DateTime to UNIX seconds. Using int instead of long becuse we are storing 4-bytes seconds.
        /// </summary>
        public static int ToUnixSeconds(this DateTime dateTime)
        {
            if (dateTime == DateTime.MinValue)
            {
                return 0;
            }
            var longResult = (long)(dateTime - s_unixEpoch).TotalSeconds;
            if (longResult > int.MaxValue)
            {
                // Stay within range rather than go negative, e.g. from GetUpperBound on a DateTime.MaxValue
                return int.MaxValue;
            }
            if (longResult < int.MinValue)
            {
                return int.MinValue;
            }
            var result = (int)longResult;
            //var check = result.FromUnixSecondsToDateTime();
            return result;
        }
    }
}