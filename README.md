# BruSoftware.ListMmf

High-performance memory-mapped file implementation of .NET's `IList<T>` interface for inter-process communication and large data handling.

## Features

- **Full IList<T> Compatibility**: Implements standard .NET collection interfaces
- **Memory-Mapped Performance**: Ultra-fast data access using memory-mapped files
- **Inter-Process Communication**: Share lists between processes seamlessly
- **Large Data Support**: Handle datasets larger than available RAM
- **64-bit Optimized**: Lock-free operations for 8-byte and smaller data types
- **Persistent Storage**: Data persists across application restarts
- **Time Series Support**: Specialized implementations for DateTime-based series
- **Variable-Width Storage**: Optimized storage for different integer sizes (Int24, Int40, Int48, Int56)
- **Bit Array Support**: Efficient storage for boolean arrays

## Installation

```bash
dotnet add package BruSoftware.ListMmf
```

## Quick Start

### Basic Usage

```csharp
using BruSoftware.ListMmf;

// Create or open a memory-mapped list
var list = new ListMmf<int>("shared-list");

// Use it like any IList<T>
list.Add(42);
list.Add(100);
list.Add(255);

// Access elements
int value = list[0];  // 42
list[1] = 200;        // Update value

// Share between processes - another process can open the same list
var sharedList = new ListMmf<int>("shared-list");
Console.WriteLine(sharedList.Count);  // 3
```

### Time Series Data

```csharp
// Optimized for DateTime series with ordered data
var timeSeries = new ListMmfTimeSeriesDateTime("market-data");

// Add timestamps
timeSeries.Add(DateTime.UtcNow);

// Efficient binary search for time-based lookups
var index = timeSeries.BinarySearch(targetDateTime);
```

### Variable-Width Integer Storage

```csharp
// Automatically uses the smallest storage size based on your data range
var optimizedList = new SmallestInt64ListMmfOptimized("optimized-data");

// Stores using minimal bytes (Int24, Int40, etc.) based on value range
optimizedList.Add(1000);      // Might use Int24
optimizedList.Add(10000000);  // Might upgrade to Int40
```

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

- Thread-safe for multiple readers
- Single writer with multiple readers supported
- No locking for 8-byte atomic operations
- Larger structures may require external synchronization

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