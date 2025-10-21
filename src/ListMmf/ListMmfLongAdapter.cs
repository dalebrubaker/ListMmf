using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace BruSoftware.ListMmf;

public sealed class ListMmfLongAdapter : IListMmfLongAdapter, IReadOnlyList64Mmf<long>
{
    private const int ChunkSize = 4096;

    private IAdapterCore _core;
    private DataType _dataType;
    private readonly string? _seriesName;
    private long _minValue;
    private long _maxValue;
    private readonly object _bufferGate = new();

    private long[]? _scratchBuffer;
    private bool _disposed;

    private long _observedMin = long.MaxValue;
    private long _observedMax = long.MinValue;
    private bool _observedInitialized;
    private double? _warningThreshold;
    private bool _warningTriggered;
    private Action<DataTypeUtilizationStatus>? _warningCallback;

    public static ListMmfLongAdapter Create<T>(IListMmf<T> list, DataType dataType, string? seriesName = null, bool isReadOnly = false)
        where T : struct
    {
        if (list == null) throw new ArgumentNullException(nameof(list));

        if (!Int64Conversion<T>.IsSupported)
        {
            throw new NotSupportedException($"Type {typeof(T)} is not supported for ListMmfLongAdapter.");
        }

        var core = new AdapterCore<T>(list, isReadOnly);
        return new ListMmfLongAdapter(core, dataType, seriesName);
    }

    private ListMmfLongAdapter(IAdapterCore core, DataType dataType, string? seriesName)
    {
        _core = core;
        _dataType = dataType;
        _seriesName = seriesName;
        (_minValue, _maxValue) = DataTypeUtils.GetMinMaxValues(dataType);
    }

    public long Count => _core.Count;

    public long Capacity
    {
        get => _core.Capacity;
        set => _core.Capacity = value;
    }

    public string Path => _core.Path;

    public int WidthBits => _core.WidthBits;

    public int Version => _core.Version;

    public DataType DataType => _core.DataType;

    public bool IsResetPointersDisallowed => _core.IsResetPointersDisallowed;

    public long this[long index]
    {
        get
        {
            EnsureNotDisposed();
            if (index < 0 || index >= _core.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return _core.ReadUnchecked(index);
        }
    }

    public void Add(long value)
    {
        EnsureNotDisposed();
        EnsureWritable();
        EnsureWithinRange(value, value);
        _core.Add(value);
        UpdateObservedRange(value);
        MaybeFireWarning();
    }

    public void AddRange(IEnumerable<long> collection)
    {
        EnsureNotDisposed();
        EnsureWritable();
        if (collection == null) throw new ArgumentNullException(nameof(collection));

        switch (collection)
        {
            case ArraySegment<long> segment:
                AddRange(segment.AsSpan());
                return;
            case long[] array:
                AddRange(array.AsSpan());
                return;
            case List<long> list:
                AddRange(CollectionsMarshal.AsSpan(list));
                return;
            case IReadOnlyList<long> readOnly:
            {
                var buffer = ArrayPool<long>.Shared.Rent(readOnly.Count);
                try
                {
                    for (var i = 0; i < readOnly.Count; i++)
                    {
                        buffer[i] = readOnly[i];
                    }
                    AddRange(buffer.AsSpan(0, readOnly.Count));
                }
                finally
                {
                    ArrayPool<long>.Shared.Return(buffer);
                }
                return;
            }
        }

        var rented = ArrayPool<long>.Shared.Rent(ChunkSize);
        try
        {
            var span = rented.AsSpan();
            var index = 0;
            foreach (var value in collection)
            {
                span[index++] = value;
                if (index == span.Length)
                {
                    AddRange(span);
                    index = 0;
                }
            }
            if (index > 0)
            {
                AddRange(span[..index]);
            }
        }
        finally
        {
            ArrayPool<long>.Shared.Return(rented);
        }
    }

    public void SetLast(long value)
    {
        EnsureNotDisposed();
        EnsureWritable();
        EnsureWithinRange(value, value);
        _core.SetLast(value);
        UpdateObservedRange(value);
        MaybeFireWarning();
    }

    public void Truncate(long newCount)
    {
        EnsureNotDisposed();
        _core.Truncate(newCount);
        InvalidateObservedRange();
    }

    public void DisallowResetPointers()
    {
        EnsureNotDisposed();
        _core.DisallowResetPointers();
    }

    public void TruncateBeginning(long newCount, IProgress<long>? progress = null)
    {
        EnsureNotDisposed();
        _core.TruncateBeginning(newCount, progress);
        InvalidateObservedRange();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_bufferGate)
        {
            if (_scratchBuffer != null)
            {
                ArrayPool<long>.Shared.Return(_scratchBuffer);
                _scratchBuffer = null;
            }
        }

        _core.Dispose();
    }

    public override string ToString()
    {
        return $"{Count:N0} of {DataType} at {Path}";
    }

    public IEnumerator<long> GetEnumerator()
    {
        EnsureNotDisposed();
        for (long i = 0; i < Count; i++)
        {
            yield return _core.ReadUnchecked(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public long ReadUnchecked(long index)
    {
        EnsureNotDisposed();
        return _core.ReadUnchecked(index);
    }

    public ReadOnlySpan<long> AsSpan(long start, int length)
    {
        EnsureNotDisposed();
        ValidateRange(_core.Count, start, length, nameof(length));

        // Try fast path for long
        if (_core.TryGetLongSpan(start, length, out var fastPath))
        {
            return fastPath;
        }

        lock (_bufferGate)
        {
            EnsureBufferCapacity(length);
            var destination = _scratchBuffer!.AsSpan(0, length);
            _core.CopyToLongSpan(start, length, destination);
            return destination;
        }
    }

    public ReadOnlySpan<long> AsSpan(long start)
    {
        EnsureNotDisposed();
        var remaining = _core.Count - start;
        if (remaining > int.MaxValue)
        {
            throw new ListMmfOnlyInt32SupportedException(remaining);
        }
        return AsSpan(start, (int)remaining);
    }

    public ReadOnlySpan<long> GetRange(long start, int length)
    {
        return AsSpan(start, length);
    }

    public ReadOnlySpan<long> GetRange(long start)
    {
        return AsSpan(start);
    }

    public DataTypeUtilizationStatus GetDataTypeUtilization()
    {
        EnsureNotDisposed();
        EnsureObservedRange();
        if (!_observedInitialized)
        {
            return new DataTypeUtilizationStatus(0, 0, 0, _minValue, _maxValue, Count);
        }

        var positiveRatio = _maxValue != 0 ? (double)_observedMax / _maxValue : 0;
        var negativeRatio = _minValue != 0 ? (double)_observedMin / _minValue : 0;
        var utilization = Math.Max(Math.Abs(positiveRatio), Math.Abs(negativeRatio));
        return new DataTypeUtilizationStatus(utilization, _observedMin, _observedMax, _minValue, _maxValue, Count);
    }

    public void ConfigureUtilizationWarning(double threshold, Action<DataTypeUtilizationStatus> callback)
    {
        if (threshold <= 0 || threshold > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold));
        }
        _warningThreshold = threshold;
        _warningCallback = callback ?? throw new ArgumentNullException(nameof(callback));
        _warningTriggered = false;
    }

    private void AddRange(ReadOnlySpan<long> values)
    {
        if (values.IsEmpty)
        {
            return;
        }

        var minValue = values[0];
        var maxValue = values[0];
        for (var i = 1; i < values.Length; i++)
        {
            var value = values[i];
            if (value < minValue)
            {
                minValue = value;
            }
            if (value > maxValue)
            {
                maxValue = value;
            }
        }

        EnsureWithinRange(minValue, maxValue);

        _core.AddRange(values);
        UpdateObservedRange(values);
        MaybeFireWarning();
    }

    private void EnsureBufferCapacity(int requiredLength)
    {
        if (_scratchBuffer != null && _scratchBuffer.Length >= requiredLength)
        {
            return;
        }

        if (_scratchBuffer != null)
        {
            ArrayPool<long>.Shared.Return(_scratchBuffer);
        }

        _scratchBuffer = ArrayPool<long>.Shared.Rent(requiredLength);
    }

    private void EnsureWithinRange(long minValue, long maxValue)
    {
        if (minValue >= _minValue && maxValue <= _maxValue)
        {
            return;
        }

        if (!_observedInitialized)
        {
            EnsureObservedRange();
        }

        var combinedMin = _observedInitialized ? Math.Min(minValue, _observedMin) : minValue;
        var combinedMax = _observedInitialized ? Math.Max(maxValue, _observedMax) : maxValue;

        if (combinedMin >= _minValue && combinedMax <= _maxValue)
        {
            return;
        }

        UpgradeToFitRange(combinedMin, combinedMax);

        if (combinedMin < _minValue || combinedMax > _maxValue)
        {
            var suggested = DataTypeUtils.GetSmallestInt64DataType(combinedMin, combinedMax);
            var attempted = combinedMax > _maxValue ? combinedMax : combinedMin;
            throw new DataTypeOverflowException(Path, _dataType, attempted, suggested, _minValue, _maxValue, _seriesName);
        }
    }

    private void UpgradeToFitRange(long minRequired, long maxRequired)
    {
        if (_core.IsReadOnly)
        {
            throw new NotSupportedException("Cannot upgrade a read-only ListMmfLongAdapter. Open the file in write mode to allow automatic upgrades.");
        }

        var targetDataType = DataTypeUtils.GetSmallestInt64DataType(minRequired, maxRequired);
        if (targetDataType == _dataType)
        {
            return;
        }

        UpgradeDataType(targetDataType, minRequired, maxRequired);
    }

    private void UpgradeDataType(DataType targetDataType, long minRequired, long maxRequired)
    {
        switch (targetDataType)
        {
            case DataType.SByte:
                UpgradeTo<sbyte>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.Byte:
                UpgradeTo<byte>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.Int16:
                UpgradeTo<short>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.UInt16:
                UpgradeTo<ushort>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.Int32:
                UpgradeTo<int>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.UInt32:
                UpgradeTo<uint>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.Int64:
                UpgradeTo<long>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.Int24AsInt64:
                UpgradeTo<Int24AsInt64>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.Int40AsInt64:
                UpgradeTo<Int40AsInt64>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.Int48AsInt64:
                UpgradeTo<Int48AsInt64>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.Int56AsInt64:
                UpgradeTo<Int56AsInt64>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.UInt24AsInt64:
                UpgradeTo<UInt24AsInt64>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.UInt40AsInt64:
                UpgradeTo<UInt40AsInt64>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.UInt48AsInt64:
                UpgradeTo<UInt48AsInt64>(targetDataType, minRequired, maxRequired);
                break;
            case DataType.UInt56AsInt64:
                UpgradeTo<UInt56AsInt64>(targetDataType, minRequired, maxRequired);
                break;
            default:
                throw new NotSupportedException($"Cannot upgrade {Path} to {targetDataType}.");
        }
    }

    private void UpgradeTo<TTarget>(DataType targetDataType, long minRequired, long maxRequired)
        where TTarget : struct
    {
        var sourceCore = _core;
        var path = sourceCore.Path;
        var upgradingPath = path + ".upgrading";
        var upgradeLockPath = upgradingPath + UtilsListMmf.LockFileExtension;
        var count = sourceCore.Count;
        var capacity = sourceCore.Capacity;
        var resetDisallowed = sourceCore.IsResetPointersDisallowed;

        if (File.Exists(upgradingPath))
        {
            File.Delete(upgradingPath);
        }
        if (File.Exists(upgradeLockPath))
        {
            File.Delete(upgradeLockPath);
        }

        var longBuffer = ArrayPool<long>.Shared.Rent(ChunkSize);
        var typedBuffer = ArrayPool<TTarget>.Shared.Rent(ChunkSize);

        try
        {
            using (var destination = new ListMmf<TTarget>(upgradingPath, targetDataType, Math.Max(capacity, count), isReadOnly: false))
            {
                if (count > 0)
                {
                    var remaining = count;
                    var offset = 0L;
                    while (remaining > 0)
                    {
                        var length = (int)Math.Min(remaining, longBuffer.Length);
                        var sourceSpan = longBuffer.AsSpan(0, length);
                        sourceCore.CopyToLongSpan(offset, length, sourceSpan);

                        var typedSpan = typedBuffer.AsSpan(0, length);
                        for (var i = 0; i < length; i++)
                        {
                            typedSpan[i] = Int64Conversion<TTarget>.FromInt64(sourceSpan[i]);
                        }

                        destination.AddRange(typedSpan[..length]);
                        offset += length;
                        remaining -= length;
                    }
                }

                if (destination.Capacity < capacity)
                {
                    destination.Capacity = capacity;
                }
            }

            sourceCore.Dispose();

            File.Delete(path);
            File.Move(upgradingPath, path);

            var newList = new ListMmf<TTarget>(path, targetDataType, isReadOnly: false);
            if (newList.Capacity < capacity)
            {
                newList.Capacity = capacity;
            }

            var newCore = new AdapterCore<TTarget>(newList, false);
            if (resetDisallowed)
            {
                newCore.DisallowResetPointers();
            }

            _core = newCore;
            _dataType = targetDataType;
            (_minValue, _maxValue) = DataTypeUtils.GetMinMaxValues(targetDataType);
            _observedInitialized = true;
            _observedMin = minRequired;
            _observedMax = maxRequired;
            _warningTriggered = false;
        }
        catch
        {
            try
            {
                if (File.Exists(upgradingPath))
                {
                    File.Delete(upgradingPath);
                }
            }
            catch
            {
                // ignore cleanup failures
            }
            throw;
        }
        finally
        {
            ArrayPool<long>.Shared.Return(longBuffer);
            ArrayPool<TTarget>.Shared.Return(typedBuffer);
        }
    }

    private void EnsureObservedRange()
    {
        if (_observedInitialized)
        {
            return;
        }

        if (Count == 0)
        {
            _observedMin = 0;
            _observedMax = 0;
            _observedInitialized = true;
            return;
        }

        var rented = ArrayPool<long>.Shared.Rent(ChunkSize);
        try
        {
            var span = rented.AsSpan();
            var remaining = Count;
            var offset = 0L;
            while (remaining > 0)
            {
                var length = (int)Math.Min(remaining, span.Length);
                _core.CopyToLongSpan(offset, length, span[..length]);
                UpdateObservedRange(span[..length]);
                offset += length;
                remaining -= length;
            }
            _observedInitialized = true;
        }
        finally
        {
            ArrayPool<long>.Shared.Return(rented);
        }
    }

    private void UpdateObservedRange(ReadOnlySpan<long> values)
    {
        for (var i = 0; i < values.Length; i++)
        {
            UpdateObservedRange(values[i]);
        }
    }

    private void UpdateObservedRange(long value)
    {
        if (!_observedInitialized)
        {
            _observedMin = Math.Min(_observedMin, value);
            _observedMax = Math.Max(_observedMax, value);
            return;
        }

        if (value < _observedMin)
        {
            _observedMin = value;
        }
        if (value > _observedMax)
        {
            _observedMax = value;
        }
    }

    private void MaybeFireWarning()
    {
        if (_warningThreshold is null || _warningCallback is null || _warningTriggered)
        {
            return;
        }

        var status = GetDataTypeUtilization();
        if (status.Utilization >= _warningThreshold)
        {
            _warningTriggered = true;
            _warningCallback(status);
        }
    }

    private void InvalidateObservedRange()
    {
        _observedInitialized = false;
        _observedMin = long.MaxValue;
        _observedMax = long.MinValue;
        _warningTriggered = false;
    }

    private static void ValidateRange(long count, long start, int length, string lengthParamName)
    {
        if (start < 0 || start > count)
        {
            throw new ArgumentOutOfRangeException(nameof(start));
        }

        if (length < 0)
        {
            throw new ArgumentOutOfRangeException(lengthParamName);
        }

        if (start + length > count)
        {
            throw new ArgumentOutOfRangeException(lengthParamName);
        }
    }

    private void EnsureWritable()
    {
        if (_core.IsReadOnly)
        {
            throw new NotSupportedException("Cannot modify a read-only ListMmfLongAdapter. Open the file in write mode to allow automatic upgrades.");
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ListMmfLongAdapter));
        }
    }

    // Internal interface to abstract over the generic type
    private interface IAdapterCore : IDisposable
    {
        long Count { get; }
        long Capacity { get; set; }
        string Path { get; }
        int WidthBits { get; }
        int Version { get; }
        DataType DataType { get; }
        bool IsReadOnly { get; }
        bool IsResetPointersDisallowed { get; }

        long ReadUnchecked(long index);
        void Add(long value);
        void AddRange(ReadOnlySpan<long> values);
        void SetLast(long value);
        void Truncate(long newCount);
        void DisallowResetPointers();
        void TruncateBeginning(long newCount, IProgress<long>? progress);

        bool TryGetLongSpan(long start, int length, out ReadOnlySpan<long> span);
        void CopyToLongSpan(long start, int length, Span<long> destination);
    }

    private sealed class AdapterCore<T> : IAdapterCore where T : struct
    {
        private readonly IListMmf<T> _list;
        private readonly IReadOnlyList64Mmf<T> _readOnly;
        private readonly bool _isReadOnly;

        public AdapterCore(IListMmf<T> list, bool isReadOnly)
        {
            _list = list;
            _readOnly = list as IReadOnlyList64Mmf<T> ?? throw new ArgumentException("List must implement IReadOnlyList64Mmf<T>", nameof(list));
            _isReadOnly = isReadOnly;
        }

        public long Count => _list.Count;
        public long Capacity
        {
            get => _list.Capacity;
            set
            {
                EnsureNotReadOnly();
                _list.Capacity = value;
            }
        }
        public string Path => _list.Path;
        public int WidthBits => _list.WidthBits;
        public int Version => _list.Version;
        public DataType DataType => _list.DataType;
        public bool IsReadOnly => _isReadOnly;
        public bool IsResetPointersDisallowed => _list.IsResetPointersDisallowed;

        public long ReadUnchecked(long index)
        {
            var value = _readOnly.ReadUnchecked(index);
            return Int64Conversion<T>.ToInt64(value);
        }

        public void Add(long value)
        {
            EnsureNotReadOnly();
            var converted = Int64Conversion<T>.FromInt64(value);
            _list.Add(converted);
        }

        public void AddRange(ReadOnlySpan<long> values)
        {
            EnsureNotReadOnly();
            var typedBuffer = ArrayPool<T>.Shared.Rent(values.Length);
            try
            {
                var typedSpan = typedBuffer.AsSpan(0, values.Length);
                for (var i = 0; i < values.Length; i++)
                {
                    typedSpan[i] = Int64Conversion<T>.FromInt64(values[i]);
                }

                if (_list is ListMmf<T> concrete)
                {
                    concrete.AddRange(typedSpan);
                }
                else
                {
                    for (var i = 0; i < typedSpan.Length; i++)
                    {
                        _list.Add(typedSpan[i]);
                    }
                }
            }
            finally
            {
                ArrayPool<T>.Shared.Return(typedBuffer);
            }
        }

        public void SetLast(long value)
        {
            EnsureNotReadOnly();
            var converted = Int64Conversion<T>.FromInt64(value);
            _list.SetLast(converted);
        }

        public void Truncate(long newCount)
        {
            EnsureNotReadOnly();
            _list.Truncate(newCount);
        }

        public void DisallowResetPointers()
        {
            EnsureNotReadOnly();
            _list.DisallowResetPointers();
        }

        public void TruncateBeginning(long newCount, IProgress<long>? progress)
        {
            EnsureNotReadOnly();
            _list.TruncateBeginning(newCount, progress);
        }

        public bool TryGetLongSpan(long start, int length, out ReadOnlySpan<long> span)
        {
            // Fast path for long
            if (typeof(T) == typeof(long) && _readOnly is IReadOnlyList64Mmf<long> longList)
            {
                span = longList.AsSpan(start, length);
                return true;
            }
            span = default;
            return false;
        }

        public void CopyToLongSpan(long start, int length, Span<long> destination)
        {
            var source = _readOnly.AsSpan(start, length);
            Int64Conversion<T>.CopyToInt64(source, destination);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void EnsureNotReadOnly()
        {
            if (_isReadOnly)
            {
                throw new NotSupportedException("Cannot modify a read-only ListMmfLongAdapter.");
            }
        }

        public void Dispose()
        {
            _list.Dispose();
        }
    }
}
