# BruSoftware.ListMmf

High-performance memory-mapped file implementation of .NET's `IList<T>` interface for inter-process communication and large data handling.

## Features

- **Full IList<T> Compatibility**: Implements standard .NET collection interfaces
- **Memory-Mapped Performance**: Ultra-fast data access using memory-mapped files
- **Inter-Process Communication**: Share lists between processes seamlessly
- **Large Data Support**: Handle datasets larger than available RAM
- **64-bit Optimized**: Lock-free operations for 8-byte and smaller data types
- **Persistent Storage**: Data persists across application restarts
- **Time Series Support**: Specialized implementations for DateTime-based series with advanced search strategies
- **Intelligent Search Algorithms**: Auto-detects data distribution for optimal search (3-5x faster on uniform data)
- **Variable-Width Storage**: Optimized storage for different integer sizes (Int24, Int40, Int48, Int56)
- **Bit Array Support**: Efficient storage for boolean arrays
- **SourceLink Enabled**: Debug into library source directly from consuming applications

## Installation

```bash
dotnet add package BruSoftware.ListMmf
```

## Quick Start

### Basic Usage

```csharp
using BruSoftware.ListMmf;

// Create or open a memory-mapped list with an appropriate type
// Use Int32 for most integers (±2.1B range), Int64 for larger/unknown ranges
var list = new ListMmf<int>("shared-list.mmf", DataType.Int32);

// Use it like any IList<T>
list.Add(42);
list.Add(100);
list.Add(255);

// Access elements
int value = list[0];  // 42
list[1] = 200;        // Update value

// Share between processes - another process can open the same list
using var sharedList = new ListMmf<int>("shared-list.mmf", DataType.Int32);
Console.WriteLine(sharedList.Count);  // 3
```

### ⚠️ Type Safety and Overflow Protection

**ListMmf uses fixed types and does NOT auto-upgrade like SmallestInt.** Choose appropriate types upfront:

```csharp
// ✅ GOOD: Use Int32 or Int64 for production data
var prices = new ListMmf<int>("prices.mmf", DataType.Int32);     // ±2.1B range
var volumes = new ListMmf<long>("volumes.mmf", DataType.Int64);  // ±9.2E+18 range

// ❌ AVOID: Small types risk overflow and data corruption
var prices = new ListMmf<short>("prices.mmf", DataType.Int16);   // Only ±32K!

// If you must cast, use checked() to throw on overflow instead of corrupting data:
int realtimeValue = GetFromDataFeed();
try {
    prices.Add(checked((short)realtimeValue));  // Throws OverflowException if too large
} catch (OverflowException) {
    Logger.Error($"Value {realtimeValue} exceeds type range");
}
```

**📘 See [BEST-PRACTICES.md](BEST-PRACTICES.md) for detailed guidance on type selection and overflow handling.**

### Zero-Copy Span Access

```csharp
// Inspect a window of data without allocating new arrays
ReadOnlySpan<int> recent = sharedList.AsSpan(start: 1, length: 2);
Console.WriteLine(recent[0]);
```

> [!NOTE]
> Legacy callers can continue to use `GetRange` but the method now forwards to `AsSpan` internally.
> Prefer the `AsSpan` overloads for new code so the zero-copy semantics are obvious at call sites.

### Time Series Data with Advanced Search Strategies

```csharp
// Optimized for DateTime series with ordered data
var timeSeries = new ListMmfTimeSeriesDateTime("market-data");

// Add timestamps
timeSeries.Add(DateTime.UtcNow);

// Efficient search with automatic strategy selection (NEW in v1.0.8)
// Auto-detects if data is uniformly distributed and uses optimal algorithm
var index = timeSeries.LowerBound(targetDateTime);  // 3-5x faster on uniform data

// Or choose explicit strategy for backtesting/analytics:
var index = timeSeries.LowerBound(targetDateTime, SearchStrategy.Interpolation);  // O(log log n)
var index = timeSeries.LowerBound(targetDateTime, SearchStrategy.Binary);         // O(log n)
var index = timeSeries.LowerBound(targetDateTime, SearchStrategy.Auto);           // Smart auto-detect
```

**Search Performance** (for 2M-2B items):
- **Binary**: ~21-31 comparisons (standard)
- **Interpolation**: ~5-7 comparisons (3-5x faster on uniform data like daily trades)
- **Auto**: Automatically chooses best strategy with one-time detection

See [SEARCH-STRATEGIES.md](SEARCH-STRATEGIES.md) for detailed usage guide.

### Variable-Width Integer Storage (SmallestInt)

```csharp
// SmallestInt automatically uses the smallest storage size based on your data range
var optimizedList = new SmallestInt64ListMmf(DataType.Int24AsInt64, "optimized-data.bt");

// Stores using minimal bytes (Int24, Int40, etc.) and auto-upgrades when needed
optimizedList.Add(1000);      // Stored as Int24 (3 bytes)
optimizedList.Add(10000000);  // Auto-upgrades to Int32 (4 bytes)
```

**When to use SmallestInt vs standard ListMmf:**
- **SmallestInt**: Saves storage (5-10%) but 5-8x slower, auto-upgrades, not Python-compatible
- **ListMmf**: Fast, Python-compatible, predictable, but no auto-upgrade (throws on overflow)

See [BEST-PRACTICES.md](BEST-PRACTICES.md#when-to-use-listmmf-vs-smallestint) for detailed comparison.

### Fast Int64 conversion for odd-byte files (no SmallestInt*)

Odd-byte structs such as `UInt24AsInt64` and `Int40AsInt64` expose zero-copy spans, but expanding them to full 64-bit integers previously required extra allocations or the SmallestInt wrappers. New extension methods keep conversions allocation-conscious:

```csharp
using BruSoftware.ListMmf;

using var list = new ListMmf<UInt40AsInt64>("ticks.u40.mmf", DataType.UInt40AsInt64);

// Reuse a caller-owned buffer when iterating through large files
var chunkSize = 1_024;
var buffer = GC.AllocateUninitializedArray<long>(chunkSize);
long position = 0;

while (position < list.Count)
{
    var toRead = (int)Math.Min(chunkSize, list.Count - position);
    list.CopyAsInt64(position, buffer.AsSpan(0, toRead));
    Process(buffer.AsSpan(0, toRead));
    position += toRead;
}

// Pool-backed helper returns IMemoryOwner<long> trimmed to your requested length
using var owner = list.RentAsInt64(start: 0, length: chunkSize);
var span = owner.Memory.Span;
Consume(span);
```

These helpers work with `ListMmf<T>` writers and `IReadOnlyList64Mmf<T>` readers for all supported odd-byte types (24/40/48/56-bit, signed and unsigned). They expand values to `long` without per-element allocations and are ideal when you need repeated analysis passes. For one-off whole-file conversions, continue to use `ListMmfWidthConverter`.

### Open any numeric file as `long`

BruTrader and other downstream tools can now work purely with `long` values even when the on-disk representation uses odd-byte widths. The new factory returns an allocation-conscious adapter that exposes `IListMmf<long>` and `IReadOnlyList64Mmf<long>` while delegating storage to the original type.

```csharp
using BruSoftware.ListMmf;
using System.IO.MemoryMappedFiles;

// Inspect values from a UInt24-backed file without rewriting it
using var bars = UtilsListMmf.OpenAsInt64("Closes.bt", MemoryMappedFileAccess.ReadWrite);

// Zero-copy reads reuse an internal pooled buffer for odd-byte widths
ReadOnlySpan<long> window = bars.AsSpan(start: bars.Count - 1_000, length: 1_000);

// Checked writes throw DataTypeOverflowException when the value no longer fits
try
{
    bars.Add(checked((long)1_000_000));
}
catch (DataTypeOverflowException ex)
{
    Console.WriteLine($"{ex.Message}\nUpgrade suggestion: {ex.SuggestedDataType}");
}

// Monitor capacity consumption (returns the larger of the positive/negative utilization ratios)
var status = ((IListMmfLongAdapter)bars).GetDataTypeUtilization();
Console.WriteLine($"{status.Utilization:P1} of {status.AllowedMax:N0} range in use");

// Optional: trigger a friendly warning when utilization crosses a threshold
((IListMmfLongAdapter)bars).ConfigureUtilizationWarning(0.90, info =>
{
    Console.WriteLine($"WARNING: {info.Utilization:P0} of {info.AllowedMax:N0} capacity consumed");
});
```

`UtilsListMmf.OpenAsInt64` automatically maps every supported `DataType` (including the odd-byte Int24/UInt24/Int40/UInt40/... variants) to its concrete `ListMmf<T>` and wraps it in the high-performance adapter. Writes remain O(1) with pooled buffers, and the adapter throws `DataTypeOverflowException` with upgrade guidance instead of silently truncating.

## Advanced Features

### Read-Only Views

```csharp
// Create read-only views for safe concurrent access
var readOnlyView = new ReadOnlyList64Mmf<double>("data", isReadOnly: true);

// Multiple readers can access simultaneously without locks
double sum = readOnlyView.Sum();
```

### Custom Data Types

```csharp
// Support for custom structs (must be blittable)
[StructLayout(LayoutKind.Sequential)]
public struct MarketTick
{
    public long Timestamp;
    public double Price;
    public int Volume;
}

var tickData = new ListMmf<MarketTick>("market-ticks");
```

### Performance Monitoring

```csharp
// Track performance metrics
var list = new ListMmf<long>("tracked-list");
list.ProgressReport += (sender, args) => 
{
    Console.WriteLine($"Operation: {args.Operation}, Items: {args.ItemsProcessed}");
};
```

## Architecture

### 64-bit Only Design

This library requires a 64-bit process to ensure atomic operations on 8-byte values without locking. This design choice enables:
- Lock-free reads and writes for primitive types
- Better performance for concurrent access
- Simplified memory management

### Memory-Mapped Files

The underlying storage uses Windows memory-mapped files, providing:
- Virtual memory management by the OS
- Automatic paging to/from disk
- Shared memory between processes
- Persistence across application restarts

### File Structure

Data files are stored with metadata headers containing:
- Data type information
- Element size
- Capacity and count
- Version information

## Performance

- **Reads**: Near-memory speed for cached pages
- **Writes**: Atomic operations for 8-byte and smaller types
- **Memory**: Only active pages consume RAM
- **Scaling**: Handles multi-GB datasets efficiently

## Thread Safety

- **Multiple Readers**: Thread-safe, no locking needed
- **Single Writer + Multiple Readers**: Supported pattern
- **Atomic Operations**: Lock-free for ≤8 byte types (int, long, double)
- **Multiple Writers**: Not supported (throws IOException on second writer)
- **Large Structures**: Types >8 bytes may require external synchronization

**Example:**
```csharp
// Process A: Writer (exclusive)
using var writer = new ListMmf<long>("shared.mmf", DataType.Int64);
writer.Add(12345);

// Process B & C: Readers (concurrent, lock-free)
using var reader1 = new ListMmf<long>("shared.mmf", DataType.Int64);
using var reader2 = new ListMmf<long>("shared.mmf", DataType.Int64);
Console.WriteLine(reader1.Count + reader2.Count);  // Safe
```

## Requirements

- .NET 9.0 or later
- 64-bit process
- Windows or Linux with memory-mapped file support

## License

Copyright © Dale A. Brubaker 2022-2025

Licensed under the terms in LICENSE.txt

## Contributing

Contributions are welcome! Please feel free to submit issues and pull requests on [GitHub](https://github.com/dalebrubaker/ListMmf).

## Support

For questions and support, please open an issue on the [GitHub repository](https://github.com/dalebrubaker/ListMmf/issues).