# BruSoftware.ListMmf

High-performance, memory-mapped, `List<T>`-style collections for .NET. ListMmf is designed for large persisted datasets and single-writer/multiple-reader inter-process sharing.

The NuGet package uses this file as its package README.

## Installation

```bash
dotnet add package BruSoftware.ListMmf
```

## Requirements

- .NET 9.0 or later
- 64-bit process
- Windows or Linux with `System.IO.MemoryMappedFiles` support
- One writer per data file; multiple read-only readers are supported

## Key Features

- Persistent collections backed by memory-mapped files
- `long` indexing and `long Count` through `IReadOnlyList64<T>`
- Append-oriented writes with `Add`, `AddRange`, `SetLast`, and truncation support
- Zero-copy `ReadOnlySpan<T>` access for compatible storage types
- Fixed-width primitive, floating point, `DateTime`, Unix-seconds, bit, and odd-byte integer storage
- Compact integer storage through `SmallestInt64ListMmf`
- Time-series lists with `BinarySearch`, `LowerBound`, and `UpperBound`
- Optimized interpolation search for Unix-seconds time series through `SearchStrategy`
- Numeric adapters that expose compact on-disk files as `IListMmf<long>`

## Quick Start

```csharp
using BruSoftware.ListMmf;

var path = "shared-list.mmf";

using var writer = new ListMmf<int>(path, DataType.Int32);
writer.Add(42);
writer.AddRange(new[] { 100, 255 });
writer.SetLast(256);

Console.WriteLine(writer[0]);        // 42
Console.WriteLine(writer.Count);     // 3

ReadOnlySpan<int> recent = writer.AsSpan(start: 1, length: 2);
Console.WriteLine(recent[0]);        // 100

using var reader = new ListMmf<int>(path, DataType.Int32, isReadOnly: true);
Console.WriteLine(reader.Count);     // 3
```

`ListMmf<T>` intentionally exposes a get-only indexer. Mutations are append-oriented; use `Add`, `AddRange`, `SetLast`, `Truncate`, or `TruncateBeginning`.

## Type Selection

`ListMmf<T>` stores values using the exact `T` and `DataType` you choose. It does not auto-upgrade when values exceed the chosen type's range.

```csharp
using BruSoftware.ListMmf;

using var prices = new ListMmf<int>("prices.mmf", DataType.Int32);
prices.Add(10_050); // Store $100.50 as cents.

int realtimeValue = GetPriceFromFeed();
prices.Add(realtimeValue);

using var smallPrices = new ListMmf<short>("small-prices.mmf", DataType.Int16);
smallPrices.Add(checked((short)realtimeValue)); // Throw instead of silently truncating.
```

For production data, prefer `Int32` or `Int64` unless you have hard range guarantees. Use `checked` casts when narrowing values before calling `Add`.

See [BEST-PRACTICES.md](https://github.com/dalebrubaker/ListMmf/blob/main/BEST-PRACTICES.md) for more guidance on type selection and overflow handling.

## Core Types

### `ListMmf<T>`

Main generic memory-mapped list for struct storage.

```csharp
using var list = new ListMmf<long>(
    path: "values.mmf",
    dataType: DataType.Int64,
    capacityItems: 1_000_000);

list.Add(123);
list.AddRange(new long[] { 456, 789 });

long count = list.Count;
long value = list[0];
ReadOnlySpan<long> window = list.AsSpan(0, 2);
```

Useful members:

- `Count`, `Capacity`, `Path`, `WidthBits`, `DataType`
- `Add(T value)`
- `AddRange(IEnumerable<T> collection)`
- `AddRange(ReadOnlySpan<T> span)`
- `SetLast(T value)`
- `ReadUnchecked(long index)`
- `AsSpan(long start, int length)`
- `Truncate(long newCount)`
- `TruncateBeginning(long newCount, IProgress<long>? progress = null)`

### `ListMmfBitArray`

Specialized bit storage for boolean values.

```csharp
using var flags = new ListMmfBitArray("flags.mmf");
flags.Add(true);
flags.Add(false);

long setBits = flags.GetCardinality();
```

It also supports `And`, `Or`, `Xor`, and `Not` operations between bit arrays.

### `ListMmfTimeSeriesDateTime`

Stores ordered `DateTime` values as ticks.

```csharp
var start = DateTime.UtcNow;

using var times = new ListMmfTimeSeriesDateTime(
    "times.ticks.mmf",
    TimeSeriesOrder.AscendingOrEqual);

times.AddRange(new[]
{
    start,
    start.AddSeconds(1),
    start.AddSeconds(2)
});

long lower = times.LowerBound(start.AddMilliseconds(1500));
long upper = times.UpperBound(start.AddSeconds(1));
long found = times.BinarySearch(start.AddSeconds(2));
```

### `ListMmfTimeSeriesDateTimeSeconds`

Stores ordered `DateTime` values as Unix seconds (`int`) and supports search strategy selection.

```csharp
var start = DateTime.UtcNow;

using var seconds = new ListMmfTimeSeriesDateTimeSeconds(
    "times.seconds.mmf",
    TimeSeriesOrder.AscendingOrEqual);

seconds.AddRange(new[]
{
    start,
    start.AddSeconds(1),
    start.AddSeconds(2)
});

long auto = seconds.LowerBound(start.AddMilliseconds(1500));
long binary = seconds.LowerBound(start.AddMilliseconds(1500), SearchStrategy.Binary);
long interpolation = seconds.LowerBound(start.AddMilliseconds(1500), SearchStrategy.Interpolation);
```

`SearchStrategy.Auto` detects sufficiently uniform data and uses interpolation search when it is likely to help.

See [SEARCH-STRATEGIES.md](https://github.com/dalebrubaker/ListMmf/blob/main/SEARCH-STRATEGIES.md) for more detail.

### `SmallestInt64ListMmf`

Stores `long` values using the smallest supported integer `DataType` that can hold the observed range. It can upgrade the backing file when a new value no longer fits.

```csharp
using var compact = new SmallestInt64ListMmf(
    DataType.Int24AsInt64,
    "compact-values.mmf");

compact.Add(1_000);
compact.Add(10_000_000); // Upgrades if the current width cannot hold the value.
```

Use this when compact storage matters more than predictable fixed-width layout. Use standard `ListMmf<T>` when you need stable file format, best speed, or simple interop.

### `SmallestEnumListMmf<T>`

Stores enum values using compact integer backing storage.

```csharp
using var states = new SmallestEnumListMmf<MyState>(
    typeof(MyState),
    "states.mmf");

states.Add(MyState.Active);
```

## Odd-Byte Integer Storage

ListMmf supports 24, 40, 48, and 56-bit signed and unsigned integer structs:

- `Int24AsInt64`, `Int40AsInt64`, `Int48AsInt64`, `Int56AsInt64`
- `UInt24AsInt64`, `UInt40AsInt64`, `UInt48AsInt64`, `UInt56AsInt64`

Use `CopyAsInt64` when you own the destination buffer, or `RentAsInt64` when temporary pooled storage is convenient.

```csharp
using BruSoftware.ListMmf;

using var list = new ListMmf<UInt40AsInt64>(
    "ticks.u40.mmf",
    DataType.UInt40AsInt64);

list.Add(new UInt40AsInt64(1_000_000));

var buffer = GC.AllocateUninitializedArray<long>((int)list.Count);
list.CopyAsInt64(0, buffer);

using var owner = list.RentAsInt64(start: 0, length: (int)list.Count);
ReadOnlySpan<long> values = owner.Memory.Span;
```

## Open Any Numeric File As `long`

`UtilsListMmf.OpenAsInt64` opens standard and odd-byte numeric files behind an `IListMmf<long>` adapter. Writes are checked, so values that no longer fit the current on-disk width throw `DataTypeOverflowException` with upgrade guidance.

```csharp
using BruSoftware.ListMmf;
using System.IO.MemoryMappedFiles;

using var values = UtilsListMmf.OpenAsInt64(
    "Closes.bt",
    MemoryMappedFileAccess.ReadWrite,
    seriesName: "Closes");

long last = values[values.Count - 1];
values.Add(last + 1);

var adapter = (IListMmfLongAdapter)values;
var status = adapter.GetDataTypeUtilization();
Console.WriteLine($"{status.Utilization:P1} of {status.AllowedMax:N0} range in use");
```

## Interfaces

```csharp
public interface IReadOnlyList64<out T> : IEnumerable<T>
{
    T this[long index] { get; }
    long Count { get; }
}

public interface IReadOnlyList64Mmf<T> : IReadOnlyList64<T>
{
    T ReadUnchecked(long index);
    ReadOnlySpan<T> AsSpan(long start, int length);
    ReadOnlySpan<T> AsSpan(long start);
    ReadOnlySpan<T> GetRange(long start, int length);
    ReadOnlySpan<T> GetRange(long start);
}

public interface IListMmf<T> : IListMmf, IEnumerable<T>
{
    T this[long index] { get; }
    void Add(T value);
    void AddRange(IEnumerable<T> collection);
    void AddRange(ReadOnlySpan<T> span);
    void SetLast(T value);
}
```

## File Model

Each ListMmf file begins with a small header containing:

- `Version`
- `DataType`
- `Count`

The list payload starts after the header and any parent-reserved header bytes used by specialized list types. Capacity grows by remapping the file when needed; readers see `Count` updated only after appended data is written.

## Concurrency

- One writer may open a file for mutation.
- Multiple read-only readers may open the same file with `isReadOnly: true`.
- Values 8 bytes or smaller are written with atomic processor-sized operations on 64-bit systems.
- Structs larger than 8 bytes may require external synchronization if readers observe concurrent writer updates.
- Multiple writers are rejected by the file locking layer.

## Performance Notes

- Random access is direct pointer-based memory access.
- `AsSpan` avoids allocations for compatible storage types.
- `ReadUnchecked` is intended for tight loops where the caller has already cached and validated `Count`.
- Capacity growth remaps the file. For long-lived spans or pointer-sensitive consumers, avoid using spans across calls that may grow capacity.
- Time-series search is `O(log n)` with binary search and can be faster on uniform Unix-seconds data with interpolation search.

## Additional Documentation

- [Best Practices](https://github.com/dalebrubaker/ListMmf/blob/main/BEST-PRACTICES.md)
- [Search Strategies](https://github.com/dalebrubaker/ListMmf/blob/main/SEARCH-STRATEGIES.md)
- [Quick Reference](https://github.com/dalebrubaker/ListMmf/blob/main/QUICK-REFERENCE.md)
- [Architecture Notes](https://github.com/dalebrubaker/ListMmf/blob/main/src/ListMmf/docs/ARCHITECTURE.md)

## License

Licensed under the terms in [LICENSE.txt](https://github.com/dalebrubaker/ListMmf/blob/main/LICENSE.txt).

Copyright (c) Dale A. Brubaker 2022-2025.
