using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;

// ReSharper disable BuiltInTypeReferenceStyleForMemberAccess

namespace BruSoftware.ListMmf;

/// <summary>
///     Wraps a smallest list that will accept integer values that fit into an Int64 (long) based on a given minValue and maxValue.
///     Includes checking of values written, with an automatic upgrade to a larger file size when required.
/// </summary>
public class SmallestInt64ListMmf : IListMmf<long>, IReadOnlyList64Mmf<long>
{
    public static EventHandler<string> MessageEvent;
    private readonly MemoryMappedFileAccess _access;
    private readonly DataType _dataTypeIfNewFile;

    private readonly object _lock;
    private readonly string _name;
    private readonly IProgressReport _progress;
    private bool _isDisposed;
    private Underlying _underlying;

    public SmallestInt64ListMmf(DataType dataTypeIfNewFile, string path, long capacityItems = 0L,
        MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite, string name = "", IProgressReport progress = null)
    {
        _lock = new object();
        Path = path;
        _dataTypeIfNewFile = dataTypeIfNewFile;
        _access = access;
        _name = name;
        _progress = progress;
        _underlying = new Underlying(dataTypeIfNewFile, path, capacityItems, access);
    }

    public string Path { get; }

    public long Count
    {
        get
        {
            lock (_lock)
            {
                return _underlying?.Count ?? 0;
            }
        }
    }

    public long Capacity
    {
        get
        {
            lock (_lock)
            {
                return _underlying?.Capacity ?? 0;
            }
        }
        set
        {
            lock (_lock)
            {
                _underlying.Capacity = value;
            }
        }
    }

    public int WidthBits
    {
        get
        {
            lock (_lock)
            {
                return _underlying?.WidthBits ?? -1;
            }
        }
    }

    public int Version
    {
        get
        {
            lock (_lock)
            {
                return _underlying?.Version ?? -1;
            }
        }
    }

    public DataType DataType
    {
        get
        {
            lock (_lock)
            {
                return _underlying?.DataType ?? DataType.Empty;
            }
        }
    }

    public IEnumerator<long> GetEnumerator()
    {
        lock (_lock)
        {
            return _underlying.GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        lock (_lock)
        {
            return _underlying.GetEnumerator();
        }
    }

    public long this[long index]
    {
        get
        {
            lock (_lock)
            {
                return _underlying[index];
            }
        }
    }

    public void Add(long value)
    {
        lock (_lock)
        {
            if (_isDisposed)
            {
                return;
            }
            UpgradeIfRequired(value);
            if (!_isDisposed)
            {
                _underlying.Add(value);
            }
        }
    }

    public void AddRange(IEnumerable<long> collection)
    {
        lock (_lock)
        {
            var minValue = _underlying.MinValue;
            var maxValue = _underlying.MaxValue;
            switch (collection)
            {
                case IReadOnlyList64<long> list:
                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (var i = 0; i < list.Count; i++)
                    {
                        var value = list[i];
                        if (value < _underlying.MinValue)
                        {
                            minValue = value;
                        }
                        if (value > _underlying.MaxValue)
                        {
                            maxValue = value;
                        }
                    }
                    break;
                case IList<long> list:
                    // ReSharper disable once ForCanBeConvertedToForeach
                    for (var i = 0; i < list.Count; i++)
                    {
                        var value = list[i];
                        if (value < _underlying.MinValue)
                        {
                            minValue = value;
                        }
                        if (value > _underlying.MaxValue)
                        {
                            maxValue = value;
                        }
                    }
                    break;
                case ICollection<long> c:

                    // ReSharper disable once PossibleMultipleEnumeration
                    foreach (var value in collection)
                    {
                        if (value < _underlying.MinValue)
                        {
                            minValue = value;
                        }
                        if (value > _underlying.MaxValue)
                        {
                            maxValue = value;
                        }
                    }
                    break;
                default:
                    using (var en = collection.GetEnumerator())
                    {
                        // Do inline Add
                        var value = en.Current;
                        Add(value);
                        while (en.MoveNext())
                        {
                            if (value < _underlying.MinValue)
                            {
                                minValue = value;
                            }
                            if (value > _underlying.MaxValue)
                            {
                                maxValue = value;
                            }
                        }
                    }
                    break;
            }
            if (minValue < _underlying.MinValue && maxValue > _underlying.MaxValue)
            {
                UpgradeUnderlying(minValue, maxValue);
            }
            else if (minValue < _underlying.MinValue)
            {
                UpgradeUnderlyingNewMinValue(minValue);
            }
            else if (maxValue > _underlying.MaxValue)
            {
                UpgradeUnderlyingNewMaxValue(maxValue);
            }

            // ReSharper disable once PossibleMultipleEnumeration
            _underlying.AddRange(collection);
        }
    }

    public void SetLast(long value)
    {
        lock (_lock)
        {
            UpgradeIfRequired(value);
            _underlying.SetLast(value);
        }
    }

    public void Truncate(long newLength)
    {
        lock (_lock)
        {
            _underlying.Truncate(newLength);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _isDisposed = true;
            _underlying?.Dispose();
            _underlying = null;
        }
    }

    public long ReadUnchecked(long index)
    {
        lock (_lock)
        {
            return _underlying.ReadUnchecked(index);
        }
    }

    private void UpgradeIfRequired(long value)
    {
        if (_isDisposed)
        {
            return;
        }
        Debug.Assert(_underlying != null);
        if (value < _underlying.MinValue && value > _underlying.MaxValue)
        {
            UpgradeUnderlying(value, value);
        }
        else if (value < _underlying.MinValue)
        {
            UpgradeUnderlyingNewMinValue(value);
        }
        else if (value > _underlying.MaxValue)
        {
            UpgradeUnderlyingNewMaxValue(value);
        }
    }

    private void UpgradeUnderlyingNewMinValue(long minValueRequired)
    {
        lock (_lock)
        {
            if (!File.Exists(Path))
            {
                // No file, so no upgrade required
                _underlying = new Underlying(_dataTypeIfNewFile, Path, 0L, _access);
                return;
            }

            // We need to calculate actual values in the file, e.g. if we need to switch from ushort to short
            var maxValueExisting = 0L;
            for (var i = 0; i < Count; i++)
            {
                var value = ReadUnchecked(i);
                if (value > maxValueExisting)
                {
                    maxValueExisting = value;
                }
            }
            UpgradeUnderlying(minValueRequired, maxValueExisting);
        }
    }

    private void UpgradeUnderlyingNewMaxValue(long maxValueRequired)
    {
        lock (_lock)
        {
            if (!File.Exists(Path))
            {
                // No file, so no upgrade required
                _underlying = new Underlying(_dataTypeIfNewFile, Path, 0L, _access);
                return;
            }

            // We need to calculate actual values in the file, e.g. if we need to switch from ushort to short
            var minValueExisting = 0L;
            for (var i = 0; i < Count; i++)
            {
                var value = ReadUnchecked(i);
                if (value < minValueExisting)
                {
                    minValueExisting = value;
                }
            }
            UpgradeUnderlying(minValueExisting, maxValueRequired);
        }
    }

    private void UpgradeUnderlying(long minValueRequired, long maxValueRequired)
    {
        lock (_lock)
        {
            if (!File.Exists(Path))
            {
                CreateNewUnderlying(minValueRequired, maxValueRequired);
                return;
            }

            // Must close the old one to be able to move it
            _underlying.Dispose();
            _underlying = null;
            var (_, dataTypeExisting, _) = UtilsListMmf.GetHeaderInfo(Path);
            var dataTypeNew = GetSmallestInt64DataType(minValueRequired, maxValueRequired);
            Upgrade(dataTypeNew, Path, dataTypeExisting, _name, _progress);
            _underlying = new Underlying(dataTypeExisting, Path, 0L, _access);
        }
    }

    private void CreateNewUnderlying(long minValueRequired, long maxValueRequired)
    {
        // No file, so no upgrade required
        var dataType = _dataTypeIfNewFile;
        if (_dataTypeIfNewFile == DataType.Empty)
        {
            dataType = GetSmallestInt64DataType(minValueRequired, maxValueRequired);
        }
        else
        {
            var (minValue, maxValue) = GetMinMaxValues(_dataTypeIfNewFile);
            if (minValueRequired < minValue || maxValueRequired > maxValue)
            {
                dataType = GetSmallestInt64DataType(minValueRequired, maxValueRequired);
            }
        }
        _underlying = new Underlying(dataType, Path, 0L, _access);
    }

    /// <summary>
    ///     Return true if a file exists at path and if it cannot accept the range from minValue-maxValue
    /// </summary>
    /// <param name="minValueRequired"></param>
    /// <param name="maxValueRequired"></param>
    /// <param name="path"></param>
    /// <param name="name">optional name for reporting progress</param>
    /// <param name="progress">Report progress</param>
    /// <returns><c>true</c> if an upgrade actually happened</returns>
    public static bool UpgradeIfRequired(long minValueRequired, long maxValueRequired, string path, string name = null, IProgressReport progress = null)
    {
        var fileExists = File.Exists(path);
        if (!fileExists)
        {
            // No upgrade needed
            return false;
        }
        var (_, dataTypeExisting, _) = UtilsListMmf.GetHeaderInfo(path);

        // Now recalculate actual existing values, because e.g. we might switch from ushort to short
        using (var smallest = new SmallestInt64ListMmf(dataTypeExisting, path, name: name, progress: progress))
        {
            var minValueExisting = 0L;
            var maxValueExisting = 0L;
            for (var i = 0; i < smallest.Count; i++)
            {
                var value = smallest.ReadUnchecked(i);
                if (value < minValueExisting)
                {
                    minValueExisting = value;
                }
                else if (value > maxValueExisting)
                {
                    maxValueExisting = value;
                }
            }
            if (minValueRequired >= minValueExisting && maxValueRequired <= maxValueExisting)
            {
                // No upgrade needed
                return false;
            }
        }
        var dataTypeNew = GetSmallestInt64DataType(minValueRequired, maxValueRequired);
        Upgrade(dataTypeNew, path, dataTypeExisting, name, progress);
        return true;
    }

    private static void Upgrade(DataType dataTypeNew, string path, DataType dataTypeExisting, string name, IProgressReport progress)
    {
        try
        {
            var tmpPath = path + ".UPGRADE";
            if (File.Exists(tmpPath))
            {
                // from prior crash
                File.Delete(tmpPath);
            }
            File.Move(path, tmpPath);
            using (var source = new SmallestInt64ListMmf(dataTypeExisting, tmpPath, 0L, MemoryMappedFileAccess.Read))
            {
                using var destination = new SmallestInt64ListMmf(dataTypeNew, path, source.Capacity);
                var message = $"Upgrading {name} {source.Count:N0} {source.DataType} values to {destination.DataType}";
                //var _ = Task.Run(() => MessageBox.Show(message, "Upgrading Data File"));
                OnMessage(message);
                var count = source.Count;
                progress?.Begin(count, $"Upgrading {name} to larger file.");
                for (var i = 0; i < count; i++)
                {
                    var value = source.ReadUnchecked(i);
                    destination.Add(value);
                    progress?.Update(i);
                }
                progress?.End(count);
            }
            File.Delete(tmpPath);
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            throw;
        }
    }

    public static (long minValue, long maxValue) GetMinMaxValues(DataType dataType)
    {
        switch (dataType)
        {
            case DataType.Empty:
                throw new NotSupportedException($"Unable to determine min/max values for {nameof(DataType)}.{dataType}");
            case DataType.Bit:
                return (0, 1);
            case DataType.SByte:
                return (SByte.MinValue, SByte.MaxValue);
            case DataType.Byte:
                return (Byte.MinValue, Byte.MaxValue);
            case DataType.Int16:
                return (Int16.MinValue, Int16.MaxValue);
            case DataType.UInt16:
                return (UInt16.MinValue, UInt16.MaxValue);
            case DataType.Int32:
                return (Int32.MinValue, Int32.MaxValue);
            case DataType.UInt32:
                return (UInt32.MinValue, UInt32.MaxValue);
            case DataType.Int64:
                return (Int64.MinValue, Int64.MaxValue);
            case DataType.UInt64:
                return (0, long.MaxValue);
            case DataType.Single:
            case DataType.Double:
            case DataType.DateTime:
            case DataType.UnixSeconds:
                throw new NotSupportedException($"{nameof(SmallestInt64ListMmf)} does not support {nameof(DataType)}.{dataType}");
            case DataType.Int24AsInt64:
                return (Int24AsInt64.MinValue, Int24AsInt64.MaxValue);
            case DataType.Int40AsInt64:
                return (Int40AsInt64.MinValue, Int40AsInt64.MaxValue);
            case DataType.Int48AsInt64:
                return (Int48AsInt64.MinValue, Int48AsInt64.MaxValue);
            case DataType.Int56AsInt64:
                return (Int56AsInt64.MinValue, Int56AsInt64.MaxValue);
            case DataType.UInt24AsInt64:
                return (UInt24AsInt64.MinValue, UInt24AsInt64.MaxValue);
            case DataType.UInt40AsInt64:
                return (UInt40AsInt64.MinValue, UInt40AsInt64.MaxValue);
            case DataType.UInt48AsInt64:
                return (UInt48AsInt64.MinValue, UInt48AsInt64.MaxValue);
            case DataType.UInt56AsInt64:
                return (UInt56AsInt64.MinValue, UInt56AsInt64.MaxValue);
            default:
                throw new ArgumentOutOfRangeException(nameof(dataType), dataType, null);
        }
    }

    public static DataType GetSmallestInt64DataType(long minValue, long maxValue)
    {
        if (minValue < 0)
        {
            // return signed class
            if (maxValue <= SByte.MaxValue && minValue >= SByte.MinValue)
            {
                return DataType.SByte;
            }
            if (maxValue <= Int16.MaxValue && minValue >= Int16.MinValue)
            {
                return DataType.Int16;
            }
            if (maxValue <= Int24AsInt64.MaxValue && minValue >= Int24AsInt64.MinValue)
            {
                return DataType.Int24AsInt64;
            }
            if (maxValue <= Int32.MaxValue && minValue >= Int32.MinValue)
            {
                return DataType.Int32;
            }
            if (maxValue <= Int40AsInt64.MaxValue && minValue >= Int40AsInt64.MinValue)
            {
                return DataType.Int40AsInt64;
            }
            if (maxValue <= Int48AsInt64.MaxValue && minValue >= Int48AsInt64.MinValue)
            {
                return DataType.Int48AsInt64;
            }
            if (maxValue <= Int56AsInt64.MaxValue && minValue >= Int56AsInt64.MinValue)
            {
                return DataType.Int56AsInt64;
            }
            if (maxValue <= Int64.MaxValue && minValue >= Int64.MinValue)
            {
                return DataType.Int64;
            }
            throw new NotSupportedException($"Unexpected. minValue={minValue} maxValue={maxValue}");
        }
        // return unsigned class
        if (maxValue == 0)
        {
            // minValue and maxValue are both 0. Don't even create a file.
            return DataType.Bit;
        }
        if (maxValue <= 1)
        {
            // Can fit in BitArray
            return DataType.Bit;
        }
        if (maxValue <= Byte.MaxValue)
        {
            return DataType.Byte;
        }
        if (maxValue <= UInt16.MaxValue)
        {
            return DataType.UInt16;
        }
        if (maxValue <= UInt24AsInt64.MaxValue)
        {
            return DataType.UInt24AsInt64;
        }
        if (maxValue <= UInt32.MaxValue)
        {
            return DataType.UInt32;
        }
        if (maxValue <= UInt40AsInt64.MaxValue)
        {
            return DataType.UInt40AsInt64;
        }
        if (maxValue <= UInt48AsInt64.MaxValue)
        {
            return DataType.UInt48AsInt64;
        }
        if (maxValue <= UInt56AsInt64.MaxValue)
        {
            return DataType.UInt56AsInt64;
        }
        if (maxValue <= Int64.MaxValue)
        {
            // Can fit in UInt64, but we can't go bigger than long because we're returning long, so just use long
            return DataType.Int64;
        }
        throw new NotSupportedException($"Unexpected. minValue={minValue} maxValue={maxValue}");
    }

    private static void OnMessage(string message)
    {
        var tmp = MessageEvent;
        tmp?.Invoke(null, message);
    }

    public override string ToString()
    {
        return $"{_name} has {Count} of {DataType}";
    }

    /// <summary>
    ///     Underlying creates the correct IListMmf for the given min/max values.
    ///     It does NOT check arguments for min/max violations -- it relies on the outer class to do that.
    /// </summary>
    private class Underlying : IListMmf<long>, IReadOnlyList64Mmf<long>
    {
        public readonly long MaxValue;
        public readonly long MinValue;
        private Action<long> _actionAdd;
        private Action<IEnumerable<long>> _actionAddRange;
        private Action<long> _actionSetLast;
        private Func<object, long> _funcCastToLong;
        private Func<IEnumerator> _funcGetEnumerator;
        private Func<long, long> _funcIndexer;
        private Func<long, long> _funcReadUnchecked;

        /// <summary>
        ///     The underlying list
        /// </summary>
        private IListMmf _iListMmf;

        public Underlying(DataType dataTypeIfNewFile, string path, long capacityItems = 0L,
            MemoryMappedFileAccess access = MemoryMappedFileAccess.ReadWrite)
        {
            DataType dataType;
            if (!File.Exists(path))
            {
                dataType = dataTypeIfNewFile;
            }
            else
            {
                (_, dataType, _) = UtilsListMmf.GetHeaderInfo(path);
            }
            switch (dataType)
            {
                case DataType.Empty:
                    // minValue and maxValue are both 0. Don't even create a file. :)
                    SetEmptyArray();
                    MaxValue = long.MinValue;
                    MinValue = long.MaxValue;
                    break;
                case DataType.Bit:
                    MaxValue = 1;
                    MinValue = 0;
                    SetBitArray(path, capacityItems, access);
                    break;
                case DataType.SByte:
                    MaxValue = SByte.MaxValue;
                    MinValue = SByte.MinValue;
                    SetSByte(path, capacityItems, access);
                    break;
                case DataType.Byte:
                    MaxValue = Byte.MaxValue;
                    MinValue = Byte.MinValue;
                    SetByte(path, capacityItems, access);
                    break;
                case DataType.Int16:
                    MaxValue = Int16.MaxValue;
                    MinValue = Int16.MinValue;
                    SetInt16(path, capacityItems, access);
                    break;
                case DataType.UInt16:
                    MaxValue = UInt16.MaxValue;
                    MinValue = UInt16.MinValue;
                    SetUInt16(path, capacityItems, access);
                    break;
                case DataType.Int32:
                    MaxValue = Int32.MaxValue;
                    MinValue = Int32.MinValue;
                    SetInt32(path, capacityItems, access);
                    break;
                case DataType.UInt32:
                    MaxValue = UInt32.MaxValue;
                    MinValue = UInt32.MinValue;
                    SetUInt32(path, capacityItems, access);
                    break;
                case DataType.Int64:
                    MaxValue = Int64.MaxValue;
                    MinValue = Int64.MinValue;
                    SetInt64(path, capacityItems, access);
                    break;
                case DataType.UInt64:
                    MaxValue = Int64.MaxValue;
                    MinValue = Int64.MinValue;
                    SetInt64(path, capacityItems, access);
                    break;
                case DataType.UInt24AsInt64:
                    MaxValue = UInt24AsInt64.MaxValue;
                    MinValue = UInt24AsInt64.MinValue;
                    SetUInt24(path, capacityItems, access);
                    break;
                case DataType.UInt40AsInt64:
                    MaxValue = UInt40AsInt64.MaxValue;
                    MinValue = UInt40AsInt64.MinValue;
                    SetUInt40(path, capacityItems, access);
                    break;
                case DataType.UInt48AsInt64:
                    MaxValue = UInt48AsInt64.MaxValue;
                    MinValue = UInt48AsInt64.MinValue;
                    SetUInt48(path, capacityItems, access);
                    break;
                case DataType.UInt56AsInt64:
                    MaxValue = UInt56AsInt64.MaxValue;
                    MinValue = UInt56AsInt64.MinValue;
                    SetUInt56(path, capacityItems, access);
                    break;
                case DataType.Int24AsInt64:
                    MaxValue = Int24AsInt64.MaxValue;
                    MinValue = Int24AsInt64.MinValue;
                    SetInt24(path, capacityItems, access);
                    break;
                case DataType.Int40AsInt64:
                    MaxValue = Int40AsInt64.MaxValue;
                    MinValue = Int40AsInt64.MinValue;
                    SetInt40(path, capacityItems, access);
                    break;
                case DataType.Int48AsInt64:
                    MaxValue = Int48AsInt64.MaxValue;
                    MinValue = Int48AsInt64.MinValue;
                    SetInt48(path, capacityItems, access);
                    break;
                case DataType.Int56AsInt64:
                    MaxValue = Int56AsInt64.MaxValue;
                    MinValue = Int56AsInt64.MinValue;
                    SetInt56(path, capacityItems, access);
                    break;
                case DataType.Single:
                case DataType.Double:
                case DataType.DateTime:
                case DataType.UnixSeconds:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public void Dispose()
        {
            _iListMmf?.Dispose();
            _iListMmf = null;
        }

        public long Count => _iListMmf?.Count ?? 0;

        public long Capacity
        {
            get => _iListMmf?.Capacity ?? 0;
            set => _iListMmf.Capacity = value;
        }

        public void Truncate(long newLength)
        {
            _iListMmf.Truncate(newLength);
        }

        public string Path => _iListMmf?.Path;
        public int WidthBits => _iListMmf?.WidthBits ?? 0;
        public int Version => _iListMmf?.Version ?? 0;
        public DataType DataType => _iListMmf?.DataType ?? DataType.Empty;

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<long> GetEnumerator()
        {
            if (_funcGetEnumerator == null)
            {
                yield return 0;
            }
            else
            {
                var en = _funcGetEnumerator.Invoke();
                ;
                while (en.MoveNext())
                {
                    var result = _funcCastToLong.Invoke(en.Current);
                    yield return result;
                }
            }
        }

        public long this[long index] => _funcIndexer(index);

        public void Add(long value)
        {
            _actionAdd(value);
        }

        public void AddRange(IEnumerable<long> collection)
        {
            _actionAddRange(collection);
        }

        public void SetLast(long value)
        {
            _actionSetLast(value);
        }

        public long ReadUnchecked(long index)
        {
            return _funcReadUnchecked(index);
        }

        private void SetEmptyArray()
        {
            _actionAdd = x => throw new ArgumentOutOfRangeException($"{x}", "Must upgrade from Empty.");
            _funcIndexer = x => 0;
            _funcReadUnchecked = x => 0;
            _actionAddRange = x => throw new ArgumentOutOfRangeException($"{x}", "Must upgrade from Empty.");
            _actionSetLast = x => throw new ArgumentOutOfRangeException($"{x}", "Must upgrade from Empty.");
            _funcGetEnumerator = null;
            _funcCastToLong = x => 0;
        }

        private void SetSByte(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<sbyte>(path, DataType.SByte, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((sbyte)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => (sbyte)y);
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast((sbyte)x);
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (sbyte)x;
        }

        private void SetInt16(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<short>(path, DataType.Int16, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((short)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => (short)y);
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast((short)x);
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (short)x;
        }

        private void SetInt32(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<int>(path, DataType.Int32, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((int)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => (int)y);
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast((int)x);
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (int)x;
        }

        private void SetInt64(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<long>(path, DataType.Int64, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => y);
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(x);
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (long)x;
        }

        private void SetBitArray(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmfBitArray(path, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(x != 0);
            };
            _funcIndexer = x =>
            {
                var result = list[x];
                return result ? 1 : 0;
            };
            _funcReadUnchecked = x =>
            {
                var result = list.ReadUnchecked(x);
                return result ? 1 : 0;
            };
            _actionAddRange = x =>
            {
                var range = x.Select(y => y != 0);
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(x != 0);
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x =>
            {
                var value = (bool)x;
                return value ? 1 : 0;
            };
        }

        private void SetByte(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<byte>(path, DataType.Byte, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((byte)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => (byte)y);
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast((byte)x);
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (byte)x;
        }

        private void SetUInt16(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<ushort>(path, DataType.UInt16, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((ushort)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => (ushort)y);
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast((ushort)x);
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (ushort)x;
        }

        private void SetUInt24(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<UInt24AsInt64>(path, DataType.UInt24AsInt64, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new UInt24AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => new UInt24AsInt64(y));
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(new UInt24AsInt64(x));
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (UInt24AsInt64)x;
        }

        private void SetInt24(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<Int24AsInt64>(path, DataType.Int24AsInt64, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new Int24AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => new Int24AsInt64(y));
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(new Int24AsInt64(x));
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (Int24AsInt64)x;
        }

        private void SetUInt32(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<uint>(path, DataType.UInt32, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((uint)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => (uint)y);
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast((uint)x);
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (uint)x;
        }

        private void SetUInt40(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<UInt40AsInt64>(path, DataType.UInt40AsInt64, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new UInt40AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => new UInt40AsInt64(y));
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(new UInt40AsInt64(x));
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (UInt40AsInt64)x;
        }

        private void SetInt40(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<Int40AsInt64>(path, DataType.Int40AsInt64, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new Int40AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => new Int40AsInt64(y));
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(new Int40AsInt64(x));
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (Int40AsInt64)x;
        }

        private void SetUInt48(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<UInt48AsInt64>(path, DataType.UInt48AsInt64, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new UInt48AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => new UInt48AsInt64(y));
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(new UInt48AsInt64(x));
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (UInt48AsInt64)x;
        }

        private void SetInt48(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<Int48AsInt64>(path, DataType.Int48AsInt64, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new Int48AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => new Int48AsInt64(y));
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(new Int48AsInt64(x));
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (Int48AsInt64)x;
        }

        private void SetUInt56(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<UInt56AsInt64>(path, DataType.UInt56AsInt64, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new UInt56AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => new UInt56AsInt64(y));
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(new UInt56AsInt64(x));
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (UInt56AsInt64)x;
        }

        private void SetInt56(string path, long capacity, MemoryMappedFileAccess access)
        {
            var list = new ListMmf<Int56AsInt64>(path, DataType.Int56AsInt64, capacity, access);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new Int56AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _actionAddRange = x =>
            {
                var range = x.Select(y => new Int56AsInt64(y));
                list.AddRange(range);
            };
            _actionSetLast = x =>
            {
                list.SetLast(new Int56AsInt64(x));
            };
            _funcGetEnumerator = list.GetEnumerator;
            _funcCastToLong = x => (Int56AsInt64)x;
        }
    }
}