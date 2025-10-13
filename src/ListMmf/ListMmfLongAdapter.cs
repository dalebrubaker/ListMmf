using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BruSoftware.ListMmf;

public sealed class ListMmfLongAdapter<T> : IListMmf<long>, IReadOnlyList64Mmf<long>
    where T : struct
{
    private const int ChunkSize = 4096;

    private readonly IListMmf<T> _list;
    private readonly IReadOnlyList64Mmf<T> _readOnly;
    private readonly DataType _dataType;
    private readonly string? _seriesName;
    private readonly long _minValue;
    private readonly long _maxValue;
    private readonly object _bufferGate = new();

    private long[]? _scratchBuffer;
    private bool _disposed;

    private long _observedMin = long.MaxValue;
    private long _observedMax = long.MinValue;
    private bool _observedInitialized;
    private double? _warningThreshold;
    private bool _warningTriggered;
    private Action<DataTypeUtilizationStatus>? _warningCallback;

    public ListMmfLongAdapter(IListMmf<T> list, DataType dataType, string? seriesName = null)
    {
        _list = list ?? throw new ArgumentNullException(nameof(list));
        _readOnly = list as IReadOnlyList64Mmf<T> ?? throw new ArgumentException("List must implement IReadOnlyList64Mmf<T>", nameof(list));
        if (!Int64Conversion<T>.IsSupported)
        {
            throw new NotSupportedException($"Type {typeof(T)} is not supported for ListMmfLongAdapter.");
        }
        _dataType = dataType;
        _seriesName = seriesName;
        (_minValue, _maxValue) = SmallestInt64ListMmf.GetMinMaxValues(dataType);
    }

    public long Count => _list.Count;

    public long Capacity
    {
        get => _list.Capacity;
        set => _list.Capacity = value;
    }

    public string Path => _list.Path;

    public int WidthBits => _list.WidthBits;

    public int Version => _list.Version;

    public DataType DataType => _list.DataType;

    public bool IsResetPointersDisallowed => _list.IsResetPointersDisallowed;

    public long this[long index]
    {
        get
        {
            EnsureNotDisposed();
            if (index < 0 || index >= _list.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            var value = _readOnly[index];
            return Int64Conversion<T>.ToInt64(value);
        }
    }

    public void Add(long value)
    {
        EnsureNotDisposed();
        EnsureWithinRange(value);
        var converted = Int64Conversion<T>.FromInt64(value);
        _list.Add(converted);
        UpdateObservedRange(value);
        MaybeFireWarning();
    }

    public void AddRange(IEnumerable<long> collection)
    {
        EnsureNotDisposed();
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
        EnsureWithinRange(value);
        var converted = Int64Conversion<T>.FromInt64(value);
        _list.SetLast(converted);
        UpdateObservedRange(value);
        MaybeFireWarning();
    }

    public void Truncate(long newCount)
    {
        EnsureNotDisposed();
        _list.Truncate(newCount);
        InvalidateObservedRange();
    }

    public void DisallowResetPointers()
    {
        EnsureNotDisposed();
        _list.DisallowResetPointers();
    }

    public void TruncateBeginning(long newCount, IProgress<long>? progress = null)
    {
        EnsureNotDisposed();
        _list.TruncateBeginning(newCount, progress);
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

        _list.Dispose();
    }

    public IEnumerator<long> GetEnumerator()
    {
        EnsureNotDisposed();
        for (long i = 0; i < Count; i++)
        {
            yield return Int64Conversion<T>.ToInt64(_readOnly.ReadUnchecked(i));
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public long ReadUnchecked(long index)
    {
        EnsureNotDisposed();
        var value = _readOnly.ReadUnchecked(index);
        return Int64Conversion<T>.ToInt64(value);
    }

    public ReadOnlySpan<long> AsSpan(long start, int length)
    {
        EnsureNotDisposed();
        ValidateRange(_list.Count, start, length, nameof(length));

        if (typeof(T) == typeof(long) && _readOnly is IReadOnlyList64Mmf<long> longList)
        {
            return longList.AsSpan(start, length);
        }

        lock (_bufferGate)
        {
            EnsureBufferCapacity(length);
            var destination = _scratchBuffer!.AsSpan(0, length);
            var source = _readOnly.AsSpan(start, length);
            Int64Conversion<T>.CopyToInt64(source, destination);
            return destination;
        }
    }

    public ReadOnlySpan<long> AsSpan(long start)
    {
        EnsureNotDisposed();
        var remaining = _list.Count - start;
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

        var typedBuffer = ArrayPool<T>.Shared.Rent(values.Length);
        try
        {
            var typedSpan = typedBuffer.AsSpan(0, values.Length);
            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i];
                EnsureWithinRange(value);
                typedSpan[i] = Int64Conversion<T>.FromInt64(value);
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

    private void EnsureWithinRange(long value)
    {
        if (value < _minValue || value > _maxValue)
        {
            var suggested = SmallestInt64ListMmf.GetSmallestInt64DataType(
                Math.Min(value, _observedInitialized ? _observedMin : value),
                Math.Max(value, _observedInitialized ? _observedMax : value));
            throw new DataTypeOverflowException(Path, _dataType, value, suggested, _minValue, _maxValue, _seriesName);
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
                var source = _readOnly.AsSpan(offset, length);
                Int64Conversion<T>.CopyToInt64(source, span[..length]);
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

    private void EnsureNotDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ListMmfLongAdapter<T>));
        }
    }

    public readonly struct DataTypeUtilizationStatus(double utilization, long observedMin, long observedMax, long allowedMin, long allowedMax, long count)
    {
        public double Utilization { get; } = utilization;
        public long ObservedMin { get; } = observedMin;
        public long ObservedMax { get; } = observedMax;
        public long AllowedMin { get; } = allowedMin;
        public long AllowedMax { get; } = allowedMax;
        public long Count { get; } = count;
    }
}
