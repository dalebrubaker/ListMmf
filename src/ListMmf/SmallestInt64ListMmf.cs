using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// ReSharper disable BuiltInTypeReferenceStyleForMemberAccess

namespace BruSoftware.ListMmf;

/// <summary>
/// Memory-mapped list that automatically selects the smallest integer type (Int24/Int40/Int48/Int56/Int32/Int64)
/// based on the range of values stored, and automatically upgrades storage when values exceed the current type's range.
/// </summary>
/// <remarks>
/// <para><strong>Automatic Type Selection and Upgrades:</strong></para>
/// <para>
/// SmallestInt dynamically chooses storage width based on value range and upgrades the file when needed:
/// </para>
/// <list type="bullet">
/// <item><description>Values 0-16M: Int24 (3 bytes) - UInt24 for unsigned</description></item>
/// <item><description>Values up to 1T: Int40 (5 bytes) - UInt40 for unsigned</description></item>
/// <item><description>Values up to 281T: Int48 (6 bytes) - UInt48 for unsigned</description></item>
/// <item><description>Values up to 72 quadrillion: Int56 (7 bytes) - UInt56 for unsigned</description></item>
/// <item><description>Larger values: Int32/Int64 (4/8 bytes)</description></item>
/// </list>
/// <para><strong>Trade-offs vs Standard ListMmf:</strong></para>
/// <list type="table">
/// <listheader>
/// <term>Feature</term>
/// <description>SmallestInt vs ListMmf&lt;T&gt;</description>
/// </listheader>
/// <item>
/// <term>Storage</term>
/// <description>5-10% smaller (uses odd-byte types like Int24)</description>
/// </item>
/// <item>
/// <term>Performance</term>
/// <description>5-8x SLOWER (bitwise conversions for odd-byte types)</description>
/// </item>
/// <item>
/// <term>Behavior</term>
/// <description>Auto-upgrades on overflow vs Throws OverflowException</description>
/// </item>
/// <item>
/// <term>Python Compat</term>
/// <description>NOT compatible (odd-byte types) vs Zero-copy compatible</description>
/// </item>
/// <item>
/// <term>Predictability</term>
/// <description>File type changes at runtime vs Fixed type</description>
/// </item>
/// </list>
/// <para><strong>When to Use SmallestInt:</strong></para>
/// <list type="number">
/// <item><description>Storage is critical (millions of files, limited disk space)</description></item>
/// <item><description>Data range is truly unknown (could be 100 or 1,000,000,000)</description></item>
/// <item><description>You need automatic upgrades for convenience</description></item>
/// <item><description>You're NOT exporting to Python/NumPy</description></item>
/// </list>
/// <para><strong>When to Use Standard ListMmf Instead:</strong></para>
/// <list type="number">
/// <item><description>You need Python/NumPy interoperability (numpy.memmap)</description></item>
/// <item><description>You want 15-30x faster read performance</description></item>
/// <item><description>You prefer predictable behavior (fail-fast on overflow)</description></item>
/// <item><description>Storage cost &lt;10% is acceptable (use Int32/Int64)</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // SmallestInt: Auto-upgrades storage type as needed
/// var data = new SmallestInt64ListMmf(DataType.Int24AsInt64, "data.bt");
/// data.Add(1_000);        // Stored as Int24 (3 bytes)
/// data.Add(10_000_000);   // Still Int24
/// data.Add(100_000_000);  // Auto-upgrades to Int32 (4 bytes)
///
/// // Compare with standard ListMmf: Fixed type, faster, Python-compatible
/// var data2 = new ListMmf&lt;int&gt;("data2.mmf", DataType.Int32);
/// data2.Add(1_000);       // Always Int32 (4 bytes)
/// data2.Add(100_000_000); // Still Int32 (no upgrade needed)
/// // If overflow: throws OverflowException instead of auto-upgrading
/// </code>
/// </example>
public class SmallestInt64ListMmf : IListMmf<long>, IReadOnlyList64Mmf<long>
{
    public static EventHandler<string>? MessageEvent;
    private readonly DataType _dataTypeIfNewFile;
    private readonly bool _isReadOnly;

    private readonly object _lock = new();
    private readonly string _name;
    private IProgressReport? _progress;
    private bool _isDisposed;
    internal Underlying? _underlying;

    public SmallestInt64ListMmf(DataType dataTypeIfNewFile, string path, long capacityItems = 0L,
        string name = "", IProgressReport? progress = null, bool isReadOnly = false)
    {
        Path = path;
        _dataTypeIfNewFile = dataTypeIfNewFile;
        _name = name;
        _progress = progress;
        _isReadOnly = isReadOnly;
        _underlying = new Underlying(dataTypeIfNewFile, path, capacityItems, isReadOnly);
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
                if (_underlying != null)
                {
                    _underlying.Capacity = value;
                }
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
                return _underlying?.DataType ?? DataType.AnyStruct;
            }
        }
    }

    public IEnumerator<long> GetEnumerator()
    {
        lock (_lock)
        {
            if (_underlying == null)
            {
                yield break;
            }
            var enumerator = _underlying.GetEnumerator();
            while (enumerator.MoveNext())
            {
                yield return enumerator.Current;
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public long this[long index]
    {
        get
        {
            lock (_lock)
            {
                if (_isDisposed || _underlying == null)
                {
                    // Can happen during shutdown
                    return 0;
                }
                return _underlying[index];
            }
        }
    }

    public void Add(long value)
    {
        lock (_lock)
        {
            if (_isDisposed || _underlying == null)
            {
                return;
            }
            UpgradeIfRequired(value);
            if (!_isDisposed && _underlying != null)
            {
                _underlying.Add(value);
            }
        }
    }

    public void AddRange(IEnumerable<long> collection)
    {
        lock (_lock)
        {
            if (_underlying == null)
            {
                return;
            }
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

    public void AddRange(ReadOnlySpan<long> span)
    {
        lock (_lock)
        {
            if (_underlying == null)
            {
                return;
            }

            if (span.IsEmpty)
            {
                return;
            }

            // Find min/max for potential upgrade
            var minValue = _underlying.MinValue;
            var maxValue = _underlying.MaxValue;
            for (var i = 0; i < span.Length; i++)
            {
                var value = span[i];
                if (value < minValue)
                {
                    minValue = value;
                }
                if (value > maxValue)
                {
                    maxValue = value;
                }
            }

            // Perform upgrade if needed
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

            _underlying.AddRange(span);
        }
    }

    public void SetLast(long value)
    {
        lock (_lock)
        {
            if (_underlying == null)
            {
                return;
            }
            UpgradeIfRequired(value);
            if (_underlying != null)
            {
                _underlying.SetLast(value);
            }
        }
    }

    public void Truncate(long newCount)
    {
        lock (_lock)
        {
            _underlying?.Truncate(newCount);
        }
    }

    public bool IsResetPointersDisallowed
    {
        get
        {
            lock (_lock)
            {
                return _underlying?.IsResetPointersDisallowed ?? false;
            }
        }
    }

    public void DisallowResetPointers()
    {
        lock (_lock)
        {
            _underlying?.DisallowResetPointers();
        }
    }

    public void TruncateBeginning(long newCount, IProgress<long>? progress = null)
    {
        lock (_lock)
        {
            _underlying?.TruncateBeginning(newCount, progress);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _isDisposed = true;
            _underlying?.Dispose();
            _underlying = null;
            _progress = null;
        }
    }

    public long ReadUnchecked(long index)
    {
        lock (_lock)
        {
            return _underlying?.ReadUnchecked(index) ?? 0;
        }
    }

    public ReadOnlySpan<long> AsSpan(long start, int length)
    {
        lock (_lock)
        {
            if (_underlying == null)
            {
                return ReadOnlySpan<long>.Empty;
            }
            return _underlying.AsSpan(start, length);
        }
    }

    public ReadOnlySpan<long> AsSpan(long start)
    {
        lock (_lock)
        {
            if (_underlying == null)
            {
                return ReadOnlySpan<long>.Empty;
            }
            var count = Count;
            if (start < 0 || start >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
            var length = (int)(count - start);
            return _underlying.AsSpan(start, length);
        }
    }

    public ReadOnlySpan<long> GetRange(long start, int length)
    {
        return AsSpan(start, length);
    }

    public ReadOnlySpan<long> GetRange(long start)
    {
        return AsSpan(start);
    }

    private void UpgradeIfRequired(long value)
    {
        if (_isDisposed)
        {
            return;
        }
        if (_underlying == null)
        {
            throw new ListMmfException("Why?");
        }
        // Handle empty sentinel where MinValue > MaxValue (AnyStruct/empty file)
        if (_underlying.MinValue > _underlying.MaxValue)
        {
            EnsureNotReadOnly();
            UpgradeUnderlying(value, value);
        }
        else if (value < _underlying.MinValue)
        {
            EnsureNotReadOnly();
            UpgradeUnderlyingNewMinValue(value);
        }
        else if (value > _underlying.MaxValue)
        {
            EnsureNotReadOnly();
            UpgradeUnderlyingNewMaxValue(value);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private void EnsureNotReadOnly()
    {
        if (_isReadOnly)
        {
            throw new NotSupportedException("Cannot upgrade a read-only SmallestInt64ListMmf. Open the file in write mode to allow automatic upgrades.");
        }
    }

    private void UpgradeUnderlyingNewMinValue(long minValueRequired)
    {
        lock (_lock)
        {
            if (!File.Exists(Path))
            {
                // No file, so no upgrade required
                _underlying = new Underlying(_dataTypeIfNewFile, Path);
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
                _underlying = new Underlying(_dataTypeIfNewFile, Path);
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

            var dataTypeNew = DataTypeUtils.GetSmallestInt64DataType(minValueRequired, maxValueRequired);

            SmallestInt64ListMmfOptimized.UpgradeOptimized(this, dataTypeNew, _name, _progress);

            // The UpgradeOptimized method disposed the _underlying, so we need to create a new underlying
            _underlying = new Underlying(dataTypeNew, Path);
        }
    }

    private void CreateNewUnderlying(long minValueRequired, long maxValueRequired)
    {
        // No file, so no upgrade required
        var dataType = _dataTypeIfNewFile;
        if (_dataTypeIfNewFile == DataType.AnyStruct)
        {
            dataType = DataTypeUtils.GetSmallestInt64DataType(minValueRequired, maxValueRequired);
        }
        else
        {
            var (minValue, maxValue) = DataTypeUtils.GetMinMaxValues(_dataTypeIfNewFile);
            if (minValueRequired < minValue || maxValueRequired > maxValue)
            {
                dataType = DataTypeUtils.GetSmallestInt64DataType(minValueRequired, maxValueRequired);
            }
        }
        _underlying = new Underlying(dataType, Path);
    }

    /// <summary>
    /// Gets the minimum and maximum values that can be stored in the specified DataType.
    /// </summary>
    /// <remarks>This method is deprecated. Use <see cref="DataTypeUtils.GetMinMaxValues"/> instead.</remarks>
    [Obsolete("Use DataTypeUtils.GetMinMaxValues instead")]
    public static (long minValue, long maxValue) GetMinMaxValues(DataType dataType)
    {
        return DataTypeUtils.GetMinMaxValues(dataType);
    }

    /// <summary>
    /// Determines the smallest integer DataType that can hold the specified range of values.
    /// </summary>
    /// <remarks>This method is deprecated. Use <see cref="DataTypeUtils.GetSmallestInt64DataType"/> instead.</remarks>
    [Obsolete("Use DataTypeUtils.GetSmallestInt64DataType instead")]
    public static DataType GetSmallestInt64DataType(long minValue, long maxValue)
    {
        return DataTypeUtils.GetSmallestInt64DataType(minValue, maxValue);
    }


    private static void OnMessage(string message)
    {
        var tmp = MessageEvent;
        tmp?.Invoke(null, message);
    }

    public override string ToString()
    {
        return $"{_name} - {Count:N0} of {DataType}";
    }

    /// <summary>
    /// Underlying creates the correct IListMmf for the given min/max values.
    /// It does NOT check arguments for min/max violations -- it relies on the outer class to do that.
    /// </summary>
    internal class Underlying : IListMmf<long>, IReadOnlyList64Mmf<long>
    {
        public readonly long MaxValue;
        public readonly long MinValue;
        private readonly bool _isReadOnly;
        private Action<long> _actionAdd = null!;
        private Action<IEnumerable<long>> _actionAddRange = null!;
        private Action<long> _actionSetLast = null!;
        private Func<object, long> _funcCastToLong = null!;
        private Func<IEnumerator>? _funcGetEnumerator = null!;
        private Func<long, long> _funcIndexer = null!;
        private Func<long, long> _funcReadUnchecked = null!;
        private Func<long, int, ReadOnlySpan<long>> _funcGetRange = null!;

        /// <summary>
        /// The underlying list
        /// </summary>
        private IListMmf _iListMmf = null!;

        public Underlying(DataType dataTypeIfNewFile, string path, long capacityItems = 0L, bool isReadOnly = false)
        {
            _isReadOnly = isReadOnly;
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
                case DataType.AnyStruct:
                    // minValue and maxValue are both 0. Don't even create a file. :)
                    SetEmptyArray();
                    MaxValue = long.MinValue;
                    MinValue = long.MaxValue;
                    break;
                case DataType.Bit:
                    MaxValue = 1;
                    MinValue = 0;
                    SetBitArray(path, capacityItems);
                    break;
                case DataType.SByte:
                    MaxValue = SByte.MaxValue;
                    MinValue = SByte.MinValue;
                    SetSByte(path, capacityItems);
                    break;
                case DataType.Byte:
                    MaxValue = Byte.MaxValue;
                    MinValue = Byte.MinValue;
                    SetByte(path, capacityItems);
                    break;
                case DataType.Int16:
                    MaxValue = Int16.MaxValue;
                    MinValue = Int16.MinValue;
                    SetInt16(path, capacityItems);
                    break;
                case DataType.UInt16:
                    MaxValue = UInt16.MaxValue;
                    MinValue = UInt16.MinValue;
                    SetUInt16(path, capacityItems);
                    break;
                case DataType.Int32:
                    MaxValue = Int32.MaxValue;
                    MinValue = Int32.MinValue;
                    SetInt32(path, capacityItems);
                    break;
                case DataType.UInt32:
                    MaxValue = UInt32.MaxValue;
                    MinValue = UInt32.MinValue;
                    SetUInt32(path, capacityItems);
                    break;
                case DataType.Int64:
                    MaxValue = Int64.MaxValue;
                    MinValue = Int64.MinValue;
                    SetInt64(path, capacityItems);
                    break;
                case DataType.UInt64:
                    MaxValue = Int64.MaxValue;
                    MinValue = Int64.MinValue;
                    SetInt64(path, capacityItems);
                    break;
                case DataType.UInt24AsInt64:
                    MaxValue = UInt24AsInt64.MaxValue;
                    MinValue = UInt24AsInt64.MinValue;
                    SetUInt24(path, capacityItems);
                    break;
                case DataType.UInt40AsInt64:
                    MaxValue = UInt40AsInt64.MaxValue;
                    MinValue = UInt40AsInt64.MinValue;
                    SetUInt40(path, capacityItems);
                    break;
                case DataType.UInt48AsInt64:
                    MaxValue = UInt48AsInt64.MaxValue;
                    MinValue = UInt48AsInt64.MinValue;
                    SetUInt48(path, capacityItems);
                    break;
                case DataType.UInt56AsInt64:
                    MaxValue = UInt56AsInt64.MaxValue;
                    MinValue = UInt56AsInt64.MinValue;
                    SetUInt56(path, capacityItems);
                    break;
                case DataType.Int24AsInt64:
                    MaxValue = Int24AsInt64.MaxValue;
                    MinValue = Int24AsInt64.MinValue;
                    SetInt24(path, capacityItems);
                    break;
                case DataType.Int40AsInt64:
                    MaxValue = Int40AsInt64.MaxValue;
                    MinValue = Int40AsInt64.MinValue;
                    SetInt40(path, capacityItems);
                    break;
                case DataType.Int48AsInt64:
                    MaxValue = Int48AsInt64.MaxValue;
                    MinValue = Int48AsInt64.MinValue;
                    SetInt48(path, capacityItems);
                    break;
                case DataType.Int56AsInt64:
                    MaxValue = Int56AsInt64.MaxValue;
                    MinValue = Int56AsInt64.MinValue;
                    SetInt56(path, capacityItems);
                    break;
                case DataType.UnixSeconds:
                    MaxValue = int.MaxValue;
                    MinValue = int.MinValue;
                    SetInt32(path, capacityItems);
                    break;
                case DataType.Single:
                case DataType.Double:
                case DataType.DateTime:
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported DataType: {dataType}");
            }
        }

        public void Dispose()
        {
            _iListMmf?.Dispose();
            _iListMmf = null!;
        }

        public long Count => _iListMmf?.Count ?? 0;

        public long Capacity
        {
            get => _iListMmf?.Capacity ?? 0;
            set => _iListMmf!.Capacity = value;
        }

        public void Truncate(long newCount)
        {
            _iListMmf!.Truncate(newCount);
        }

        public bool IsResetPointersDisallowed => _iListMmf?.IsResetPointersDisallowed ?? false;

        public void DisallowResetPointers()
        {
            _iListMmf?.DisallowResetPointers();
        }

        public void TruncateBeginning(long newCount, IProgress<long>? progress = null)
        {
            _iListMmf!.TruncateBeginning(newCount, progress);
        }

        public string Path => _iListMmf?.Path ?? string.Empty;
        public int WidthBits => _iListMmf?.WidthBits ?? 0;
        public int Version => _iListMmf?.Version ?? 0;
        public DataType DataType => _iListMmf?.DataType ?? DataType.AnyStruct;

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

        public void AddRange(ReadOnlySpan<long> span)
        {
            // Note: This iterates element-by-element rather than using bulk memory operations
            // because the delegate-based architecture doesn't expose span-aware bulk operations.
            // The performance benefit comes from the outer SmallestInt64ListMmf.AddRange(span)
            // which does min/max scanning and upgrade checks in a single pass before calling this.
            for (var i = 0; i < span.Length; i++)
            {
                _actionAdd(span[i]);
            }
        }

        public void SetLast(long value)
        {
            _actionSetLast(value);
        }

        public long ReadUnchecked(long index)
        {
            return _funcReadUnchecked(index);
        }

        public ReadOnlySpan<long> AsSpan(long start, int length)
        {
            return _funcGetRange(start, length);
        }

        public ReadOnlySpan<long> AsSpan(long start)
        {
            var count = Count;
            if (start < 0 || start >= count)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }
            var length = (int)(count - start);
            return _funcGetRange(start, length);
        }

        public ReadOnlySpan<long> GetRange(long start, int length)
        {
            return AsSpan(start, length);
        }

        public ReadOnlySpan<long> GetRange(long start)
        {
            return AsSpan(start);
        }

        private void SetEmptyArray()
        {
            _actionAdd = x => throw new ArgumentOutOfRangeException($"{x}", "Must upgrade from Empty.");
            _funcIndexer = x => 0;
            _funcReadUnchecked = x => 0;
            _funcGetRange = (start, length) => throw new ArgumentOutOfRangeException($"{start},{length}", "Must upgrade from Empty.");
            _actionAddRange = x => throw new ArgumentOutOfRangeException($"{x}", "Must upgrade from Empty.");
            _actionSetLast = x => throw new ArgumentOutOfRangeException($"{x}", "Must upgrade from Empty.");
            _funcGetEnumerator = null;
            _funcCastToLong = x => 0;
        }

        private void SetSByte(string path, long capacity)
        {
            var list = new ListMmf<sbyte>(path, DataType.SByte, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((sbyte)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i];
                }
                return result.AsSpan();
            };
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

        private void SetInt16(string path, long capacity)
        {
            var list = new ListMmf<short>(path, DataType.Int16, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((short)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i];
                }
                return result.AsSpan();
            };
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

        private void SetInt32(string path, long capacity)
        {
            var list = new ListMmf<int>(path, DataType.Int32, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((int)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i];
                }
                return result.AsSpan();
            };
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

        private void SetInt64(string path, long capacity)
        {
            var list = new ListMmf<long>(path, DataType.Int64, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) => list.AsSpan(start, length); // Direct access - no conversion needed
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

        private void SetBitArray(string path, long capacity)
        {
            var list = new ListMmfBitArray(path, capacity, isReadOnly: _isReadOnly);
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
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i] ? 1 : 0;
                }
                return result.AsSpan();
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

        private void SetByte(string path, long capacity)
        {
            var list = new ListMmf<byte>(path, DataType.Byte, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((byte)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i];
                }
                return result.AsSpan();
            };
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

        private void SetUInt16(string path, long capacity)
        {
            var list = new ListMmf<ushort>(path, DataType.UInt16, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((ushort)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i];
                }
                return result.AsSpan();
            };
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

        private void SetUInt24(string path, long capacity)
        {
            var list = new ListMmf<UInt24AsInt64>(path, DataType.UInt24AsInt64, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new UInt24AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i]; // Implicit conversion to long
                }
                return result.AsSpan();
            };
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

        private void SetInt24(string path, long capacity)
        {
            var list = new ListMmf<Int24AsInt64>(path, DataType.Int24AsInt64, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new Int24AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i]; // Implicit conversion to long
                }
                return result.AsSpan();
            };
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

        private void SetUInt32(string path, long capacity)
        {
            var list = new ListMmf<uint>(path, DataType.UInt32, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add((uint)x);
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i];
                }
                return result.AsSpan();
            };
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

        private void SetUInt40(string path, long capacity)
        {
            var list = new ListMmf<UInt40AsInt64>(path, DataType.UInt40AsInt64, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new UInt40AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i]; // Implicit conversion to long
                }
                return result.AsSpan();
            };
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

        private void SetInt40(string path, long capacity)
        {
            var list = new ListMmf<Int40AsInt64>(path, DataType.Int40AsInt64, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new Int40AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i]; // Implicit conversion to long
                }
                return result.AsSpan();
            };
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

        private void SetUInt48(string path, long capacity)
        {
            var list = new ListMmf<UInt48AsInt64>(path, DataType.UInt48AsInt64, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new UInt48AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i]; // Implicit conversion to long
                }
                return result.AsSpan();
            };
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

        private void SetInt48(string path, long capacity)
        {
            var list = new ListMmf<Int48AsInt64>(path, DataType.Int48AsInt64, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new Int48AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.AsSpan(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i]; // Implicit conversion to long
                }
                return result.AsSpan();
            };
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

        private void SetUInt56(string path, long capacity)
        {
            var list = new ListMmf<UInt56AsInt64>(path, DataType.UInt56AsInt64, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new UInt56AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.GetRange(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i]; // Implicit conversion to long
                }
                return result.AsSpan();
            };
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

        private void SetInt56(string path, long capacity)
        {
            var list = new ListMmf<Int56AsInt64>(path, DataType.Int56AsInt64, capacity, isReadOnly: _isReadOnly);
            _iListMmf = list;
            _actionAdd = x =>
            {
                list.Add(new Int56AsInt64(x));
            };
            _funcIndexer = x => list[x];
            _funcReadUnchecked = x => list.ReadUnchecked(x);
            _funcGetRange = (start, length) =>
            {
                var sourceSpan = list.GetRange(start, length);
                var result = new long[length];
                for (var i = 0; i < length; i++)
                {
                    result[i] = sourceSpan[i]; // Implicit conversion to long
                }
                return result.AsSpan();
            };
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
