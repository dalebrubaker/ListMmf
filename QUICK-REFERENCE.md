# ListMmf Quick Reference Card

## Type Selection Cheat Sheet

| Your Data | Recommended Type | Range | Why |
|-----------|-----------------|-------|-----|
| Prices (in cents) | `int` | ±$21M | Handles even extreme stock prices |
| Volume/Shares | `long` | ±9.2E+18 | Never overflow on volume |
| Percentages (×10000) | `int` | ±214K% | More than enough precision |
| Small categories | `byte` | 0-255 | Only if you're certain |
| Boolean flags | `byte` or BitArray | 0-1 | BitArray more space-efficient |
| Floating point | `double` | 15 digits | Always prefer double over float |
| Timestamps | `DateTime` or `UnixSeconds` | Full range | Use specialized types |

## Common Pitfalls

### ❌ DON'T: Use small types for production data
```csharp
var prices = new ListMmf<short>("prices.mmf", DataType.Int16);  // DANGER!
prices.Add(50_000);  // Silent wrap to -15536 - DATA CORRUPTED!
```

### ✅ DO: Use appropriate types
```csharp
var prices = new ListMmf<int>("prices.mmf", DataType.Int32);  // SAFE
prices.Add(50_000);  // Works perfectly
```

### ❌ DON'T: Forget the DataType parameter
```csharp
var list = new ListMmf<int>("data.mmf");  // COMPILER ERROR - missing DataType
```

### ✅ DO: Always specify DataType
```csharp
var list = new ListMmf<int>("data.mmf", DataType.Int32);  // Correct
```

### ❌ DON'T: Use unchecked casts
```csharp
int value = GetValue();
list.Add(unchecked((short)value));  // Silent corruption!
```

### ✅ DO: Use checked casts or appropriate types
```csharp
int value = GetValue();
try {
    list.Add(checked((short)value));  // Throws on overflow
} catch (OverflowException) {
    // Handle error
}

// OR better: use int instead of short
var list2 = new ListMmf<int>("data.mmf", DataType.Int32);
list2.Add(value);  // No cast needed
```

## Performance Tips

### Use Spans for Bulk Operations
```csharp
// Slow
double sum = 0;
for (long i = 0; i < list.Count; i++) {
    sum += list[i];  // Bounds check every time
}

// Fast
var span = list.AsSpan(0, (int)list.Count);
double sum = 0;
foreach (var value in span) {
    sum += value;  // No bounds checks, vectorized
}
```

### Pre-allocate Capacity
```csharp
// Slow - file grows multiple times
var list = new ListMmf<int>("data.mmf", DataType.Int32);
for (int i = 0; i < 1_000_000; i++) list.Add(i);

// Fast - one allocation
var list = new ListMmf<int>("data.mmf", DataType.Int32, capacityItems: 1_000_000);
for (int i = 0; i < 1_000_000; i++) list.Add(i);
```

### Use AddRange for Bulk Inserts
```csharp
// Slow - individual capacity checks
foreach (var item in items) {
    list.Add(item);
}

// Fast - single capacity check
list.AddRange(items);
```

## Common Patterns

### Basic Usage
```csharp
using BruSoftware.ListMmf;

using var list = new ListMmf<int>("data.mmf", DataType.Int32);
list.Add(42);
list.Add(100);
Console.WriteLine(list[0]);  // 42
```

### Inter-Process Sharing
```csharp
// Process A: Writer
using var writer = new ListMmf<long>("shared.mmf", DataType.Int64);
writer.Add(12345);

// Process B: Reader
using var reader = new ListMmf<long>("shared.mmf", DataType.Int64);
Console.WriteLine(reader[0]);  // 12345
```

### Error Handling
```csharp
try {
    using var list = new ListMmf<int>("data.mmf", DataType.Int32);
    list.Add(realtimeValue);
}
catch (OverflowException ex) {
    Logger.Error($"Value overflow: {ex.Message}");
    // Handle: alert, fallback, upgrade type, etc.
}
catch (IOException ex) when (ex.Message.Contains("already open")) {
    Logger.Warn("File locked by another process");
    // Retry or fail gracefully
}
```

### Time Series Data
```csharp
var series = new ListMmfTimeSeriesDateTime("market-data.mmf",
    TimeSeriesOrder.Ascending);

series.Add(DateTime.UtcNow);
series.Add(DateTime.UtcNow.AddMinutes(1));

// Binary search for time-based lookups
var index = series.BinarySearch(targetTime);
```

## File Extensions

| Extension | Type | Notes |
|-----------|------|-------|
| `.mmf` | Standard ListMmf | Recommended for standard types |
| `.bt` | SmallestInt/BitArray | Legacy/specialized types |
| `.lock` | Lock file | Auto-created, don't delete manually |

## When to Use What

| Scenario | Use This |
|----------|----------|
| Production real-time data | `ListMmf<int>` or `ListMmf<long>` |
| Need Python/NumPy export | `ListMmf<T>` with standard types |
| Storage critical (millions of files) | `SmallestInt64ListMmf` |
| Need auto-upgrade behavior | `SmallestInt64ListMmf` |
| Want predictable behavior | `ListMmf<T>` with checked casts |
| Ordered time-based data | `ListMmfTimeSeriesDateTime` |
| Boolean flags | `ListMmfBitArray` |
| Custom structures | `ListMmf<MyStruct>` (must be blittable) |

## Python Interop

### Read ListMmf in Python
```python
import numpy as np
import struct

def read_listmmf(filepath):
    with open(filepath, 'rb') as f:
        version = struct.unpack('<i', f.read(4))[0]
        datatype = struct.unpack('<i', f.read(4))[0]
        count = struct.unpack('<q', f.read(8))[0]

    dtype_map = {6: np.int32, 8: np.int64, 11: np.float64}
    return np.memmap(filepath, dtype=dtype_map[datatype],
                     mode='r', offset=16, shape=(count,))

# Zero-copy access
data = read_listmmf("prices.mmf")
```

### Export to Parquet
```python
import pandas as pd

data = read_listmmf("prices.mmf")
df = pd.DataFrame({'price': data})
df.to_parquet("prices.parquet", compression='snappy')
```

## Troubleshooting

| Error | Cause | Solution |
|-------|-------|----------|
| `IOException: already open` | Another writer exists | Use single writer pattern |
| `OverflowException` | Value exceeds type range | Use larger type or checked cast |
| `PlatformNotSupportedException: 64-bit required` | Running 32-bit process | Build for x64 or Any CPU |
| `ListMmfException: Count exceeds Capacity` | Internal corruption | Check disk space, file permissions |
| Data corruption (negative values) | unchecked cast overflow | Always use checked casts or larger types |

## Further Reading

- [BEST-PRACTICES.md](BEST-PRACTICES.md) - Detailed guidance on type selection, overflow handling, performance
- [README.md](README.md) - Full documentation and examples
- [Unit Tests](src/ListMmfTests/) - Comprehensive examples of all features
