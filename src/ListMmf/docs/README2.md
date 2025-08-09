# ListMmf Library Summary

## Overview

ListMmf is a .NET library that provides `List<T>`-like functionality backed by Memory Mapped Files (MMF). It enables efficient storage and access to large collections of data that persist on disk while providing fast random access through memory mapping.

## Key Features

### 1. **Memory Mapped File Storage**
- Stores data in memory-mapped files for efficient access to large datasets
- Supports files larger than available RAM
- Provides atomic operations for structures ≤ 8 bytes (requires 64-bit process)
- Thread-safe operations with locking mechanisms

### 2. **Multiple Data Types Support**
- **Basic Types**: `byte`, `sbyte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double`
- **Special Types**: 
  - `DateTime` (stored as ticks)
  - Unix seconds (stored as int)
  - BitArray (efficient bit storage)
- **Odd-byte Types**: 24, 40, 48, and 56-bit integers (both signed and unsigned)

### 3. **List-like Operations**
- Add single items or ranges
- Random access by index
- Enumeration support
- Count and Capacity management
- SetLast() for updating the last item

### 4. **Time Series Support**
- Specialized classes for sorted DateTime collections
- Binary search capabilities
- Upper/lower bound searches
- Enforces ascending order (with or without duplicates)

### 5. **Automatic Type Optimization**
- `SmallestInt64ListMmf` automatically selects the smallest data type that can hold your integer values
- Automatic upgrades when values exceed current type limits
- Supports values from single bits to 64-bit integers

### 6. **Reader/Writer Pattern**
- Single writer, multiple readers concurrency model
- Readers automatically adjust when writer expands capacity
- Real-time data sharing between processes

## Core Classes

### `ListMmf<T>`
The main generic class for memory-mapped lists.

```csharp
public class ListMmf<T> : IListMmf<T>, IReadOnlyList64Mmf<T> where T : struct
```

**Constructor:**
```csharp
ListMmf(string path, DataType dataType, long capacityItems = 0, 
        MemoryMappedFileAccess access = MemoryMappedFileAccess.Read)
```

**Key Methods:**
- `void Add(T value)` - Adds item to end
- `void AddRange(IEnumerable<T> collection)` - Adds multiple items
- `T this[long index]` - Gets item at index (setter is private)
- `void SetLast(T value)` - Updates the last item
- `void Truncate(long newLength)` - Reduces list size
- `T ReadUnchecked(long index)` - Fast unchecked read for loops

**Properties:**
- `long Count` - Number of items
- `long Capacity` - Current capacity (auto-grows)
- `string Path` - File path
- `int WidthBits` - Bits per item
- `DataType DataType` - Type of data stored

### `ListMmfBitArray`
Specialized implementation for bit arrays.

```csharp
public class ListMmfBitArray : IListMmf<bool>, IReadOnlyList64Mmf<bool>
```

**Additional Methods:**
- `ListMmfBitArray And(ListMmfBitArray value)`
- `ListMmfBitArray Or(ListMmfBitArray value)`
- `ListMmfBitArray Xor(ListMmfBitArray value)`
- `ListMmfBitArray Not()`
- `long GetCardinality()` - Count of set bits

### `ListMmfTimeSeriesDateTime`
For time-ordered DateTime collections.

```csharp
public class ListMmfTimeSeriesDateTime : IListMmf<DateTime>, IReadOnlyList64Mmf<DateTime>
```

**Additional Methods:**
- `long BinarySearch(DateTime value, long index = 0, long length = long.MaxValue)`
- `long GetUpperBound(DateTime value, long index = 0, long length = long.MaxValue)`
- `long GetLowerBound(DateTime value, long index = 0, long length = long.MaxValue)`
- `long GetIndex(DateTime value, long index, long length)`

**Time Series Order Options:**
- `TimeSeriesOrder.None` - No ordering enforced
- `TimeSeriesOrder.Ascending` - Strictly ascending (no duplicates)
- `TimeSeriesOrder.AscendingOrEqual` - Ascending with duplicates allowed

### `SmallestInt64ListMmf`
Automatically manages the smallest integer type for your data.

```csharp
public class SmallestInt64ListMmf : IListMmf<long>, IReadOnlyList64Mmf<long>
```

**Features:**
- Automatically upgrades from smaller to larger types as needed
- Supports from BitArray (1 bit) to Int64
- Transparent to the user - always works with `long` values

### `SmallestEnumListMmf<T>`
Optimized storage for enum types.

```csharp
public class SmallestEnumListMmf<T> : IListMmf<T>, IReadOnlyList64Mmf<T> where T : Enum
```

## Interfaces

### `IListMmf<T>`
```csharp
public interface IListMmf<T> : IListMmf, IEnumerable<T>
{
    T this[long index] { get; }
    void Add(T value);
    void AddRange(IEnumerable<T> collection);
    void SetLast(T value);
}
```

### `IReadOnlyList64<T>`
```csharp
public interface IReadOnlyList64<T> : IEnumerable<T>
{
    T this[long index] { get; }
    long Count { get; }
}
```

### `IReadOnlyList64Mmf<T>`
```csharp
public interface IReadOnlyList64Mmf<T> : IReadOnlyList64<T>
{
    T ReadUnchecked(long index);
}
```

## Usage Examples

### Basic List Operations
```csharp
// Create a writer
using (var list = new ListMmf<long>("data.mmf", DataType.Int64, 
                                    access: MemoryMappedFileAccess.ReadWrite))
{
    // Add items
    list.Add(42);
    list.AddRange(new[] { 1, 2, 3, 4, 5 });
    
    // Read items
    var value = list[0]; // 42
    var count = list.Count; // 6
    
    // Update last item
    list.SetLast(100);
}

// Create a reader in another process
using (var reader = new ListMmf<long>("data.mmf", DataType.Int64))
{
    for (long i = 0; i < reader.Count; i++)
    {
        Console.WriteLine(reader[i]);
    }
}
```

### Time Series
```csharp
using (var timeSeries = new ListMmfTimeSeriesDateTime("timeseries.mmf", 
                                                      TimeSeriesOrder.Ascending,
                                                      access: MemoryMappedFileAccess.ReadWrite))
{
    timeSeries.Add(DateTime.Now);
    timeSeries.Add(DateTime.Now.AddHours(1));
    
    // Binary search
    var index = timeSeries.BinarySearch(DateTime.Now.AddMinutes(30));
    if (index < 0)
    {
        index = ~index; // Get insertion point
    }
}
```

### Automatic Type Selection
```csharp
using (var list = new SmallestInt64ListMmf(DataType.Empty, "auto.mmf"))
{
    list.Add(1);    // Uses BitArray
    list.Add(100);  // Automatically upgrades to Byte
    list.Add(1000); // Automatically upgrades to UInt16
    list.Add(-1);   // Automatically upgrades to Int16
}
```

## Performance Characteristics

- **Random Access**: O(1) - Direct memory access through pointers
- **Sequential Access**: Optimized with `ReadUnchecked()` for tight loops
- **Add Operations**: O(1) amortized (may trigger capacity growth)
- **File Growth**: Doubles capacity or adds 1GB (whichever is smaller)
- **Binary Search**: O(log n) for time series operations

## Important Notes

1. **64-bit Process Required**: The library requires a 64-bit process for atomic operations
2. **Thread Safety**: Operations are thread-safe through locking
3. **Single Writer**: Only one writer is allowed per file at a time
4. **File Persistence**: Data persists on disk between application runs
5. **Memory Efficiency**: Uses memory mapping, so large files don't consume equivalent RAM
6. **Page Alignment**: Files are aligned to 4KB pages for optimal performance

## Supported Platforms

- .NET Standard 2.0
- .NET Standard 2.1
- Windows-specific features in test projects