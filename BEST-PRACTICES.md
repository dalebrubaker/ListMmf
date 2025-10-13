# ListMmf Best Practices Guide

## Choosing the Right Type

### Production Data: Use Standard, Generous Types

**✅ Recommended:**
```csharp
// Prices (in cents): Use Int32 (supports up to $21M per share)
var prices = new ListMmf<int>("prices.mmf", DataType.Int32);

// Volume: Use Int64 (supports up to 9.2 quintillion shares)
var volume = new ListMmf<long>("volume.mmf", DataType.Int64);

// Timestamps: Use DateTime or Unix seconds
var timestamps = new ListMmfTimeSeriesDateTime("timestamps.mmf");
```

**❌ Avoid for Production:**
```csharp
// Short (Int16): Only ±32K range - too small for most real data
var prices = new ListMmf<short>("prices.mmf", DataType.Int16);  // DON'T

// Byte: Only 0-255 range - extremely limited
var data = new ListMmf<byte>("data.mmf", DataType.Byte);  // DON'T (unless you're sure)
```

### Type Size Reference

| Type | Range | Use Case | Storage Cost |
|------|-------|----------|--------------|
| `byte` | 0 to 255 | Small categorical data | 1 byte |
| `short` | ±32K | Limited range data only | 2 bytes |
| `int` | ±2.1B | **Recommended for most integers** | 4 bytes |
| `long` | ±9.2E+18 | **Recommended for large/unknown range** | 8 bytes |
| `float` | ~7 digits precision | Scientific data | 4 bytes |
| `double` | ~15 digits precision | **Recommended for floating point** | 8 bytes |

**Storage cost is negligible:** Going from `int` to `long` only doubles storage. For 1 million values, that's 4MB vs 8MB - trivial compared to the risk of overflow.

## Overflow Protection

### The Problem: Silent Data Corruption

```csharp
// WITHOUT overflow protection - DANGEROUS!
var prices = new ListMmf<short>("prices.mmf", DataType.Int16);

// This compiles and runs, but CORRUPTS DATA:
int realtimePrice = 50_000;  // From data feed
prices.Add((short)realtimePrice);  // Silently wraps to -15536! ❌

// Your backtesting now sees NEGATIVE PRICES!
```

### Solution 1: Use Appropriate Types (Best)

```csharp
// No casting needed, no overflow possible
var prices = new ListMmf<int>("prices.mmf", DataType.Int32);

int realtimePrice = 50_000;
prices.Add(realtimePrice);  // ✅ Works perfectly, no cast needed
```

### Solution 2: Use Checked Casts (When You Must Cast)

```csharp
var prices = new ListMmf<short>("prices.mmf", DataType.Int16);

int realtimePrice = GetPriceFromFeed();

try
{
    // checked() throws OverflowException instead of corrupting data
    prices.Add(checked((short)realtimePrice));
}
catch (OverflowException ex)
{
    Logger.Error($"Price {realtimePrice} exceeds Int16 range (±32K)");

    // Handle gracefully:
    // - Alert operators
    // - Use fallback value
    // - Switch to larger type
    // - Reject the data
}
```

### Solution 3: Project-Wide Checked Arithmetic

Add to your `.csproj`:
```xml
<PropertyGroup>
    <CheckForOverflowUnderflow>true</CheckForOverflowUnderflow>
</PropertyGroup>
```

This enables checked arithmetic globally, catching overflows at compile-time where possible and runtime elsewhere.

## Python Interoperability

### Zero-Copy Reading with NumPy

ListMmf files with standard types can be read directly in Python with zero copying:

```python
import numpy as np
import struct

def read_listmmf(filepath):
    """Read ListMmf file into numpy array (zero-copy via memmap)."""
    with open(filepath, 'rb') as f:
        version = struct.unpack('<i', f.read(4))[0]
        datatype = struct.unpack('<i', f.read(4))[0]
        count = struct.unpack('<q', f.read(8))[0]

    # Map DataType enum to numpy dtype
    dtype_map = {
        6: np.int32,    # Int32
        8: np.int64,    # Int64
        11: np.float64, # Double
        12: np.int64,   # DateTime (as ticks)
    }

    # Zero-copy memory map
    return np.memmap(filepath, dtype=dtype_map[datatype],
                     mode='r', offset=16, shape=(count,))

# Usage
prices = read_listmmf("prices.mmf")  # Zero-copy!
import pandas as pd
df = pd.DataFrame({'price': prices})
df.to_parquet("prices.parquet")  # Export to Parquet
```

### Supported Types for Python Zero-Copy

✅ **Native numpy types (zero-copy compatible):**
- Int8, Int16, Int32, Int64
- UInt8, UInt16, UInt32, UInt64
- Float32 (Single), Float64 (Double)

❌ **NOT compatible (require custom unpacking):**
- Int24, Int40, Int48, Int56 (odd-byte types)
- UInt24, UInt40, UInt48, UInt56
- BitArray

**Recommendation:** If you need Python interop, use standard Int32/Int64/Double types.

## Performance Optimization

### Use AsSpan() for Bulk Operations

```csharp
// ❌ Slow: Element-by-element with bounds checking
double sum = 0;
for (long i = 0; i < prices.Count; i++)
{
    sum += prices[i];  // Bounds check on every access
}

// ✅ Fast: Zero-copy span with no allocations
ReadOnlySpan<int> priceSpan = prices.AsSpan(0, (int)prices.Count);
double sum = 0;
foreach (int price in priceSpan)
{
    sum += price;  // Direct memory access, no bounds checks in loop
}

// ✅ Even faster: Use LINQ on spans (vectorized)
double average = priceSpan.ToArray().Average();  // One allocation, then vectorized ops
```

### Bulk Writes with AddRange()

```csharp
// ❌ Slow: Individual Add() calls
for (int i = 0; i < 1000; i++)
{
    list.Add(values[i]);  // Capacity check on every call
}

// ✅ Fast: Single AddRange() call
list.AddRange(values);  // One capacity check, bulk copy
```

### Pre-allocate Capacity

```csharp
// ❌ Slow: File grows incrementally
var list = new ListMmf<int>("data.mmf", DataType.Int32);
for (int i = 0; i < 1_000_000; i++)
{
    list.Add(i);  // Multiple file grow operations
}

// ✅ Fast: Pre-allocate expected size
var list = new ListMmf<int>("data.mmf", DataType.Int32, capacityItems: 1_000_000);
for (int i = 0; i < 1_000_000; i++)
{
    list.Add(i);  // No growth needed
}
```

## Thread Safety

### Supported Patterns

**✅ Single Writer + Multiple Readers:**
```csharp
// Process A: Writer
using var writer = new ListMmf<long>("shared.mmf", DataType.Int64);
writer.Add(12345);

// Process B: Reader
using var reader = new ListMmf<long>("shared.mmf", DataType.Int64);
Console.WriteLine(reader.Count);  // Safe concurrent read
```

**✅ Read-Only Access:**
```csharp
// Multiple processes can read simultaneously
var reader1 = new ListMmf<double>("data.mmf", DataType.Double);
var reader2 = new ListMmf<double>("data.mmf", DataType.Double);
// Both can read without locks
```

**❌ Multiple Writers:**
```csharp
// DON'T: Multiple writers will corrupt data
using var writer1 = new ListMmf<int>("data.mmf", DataType.Int32);
using var writer2 = new ListMmf<int>("data.mmf", DataType.Int32);  // FAILS with IOException
```

### Atomic Operations

**Lock-free for ≤8 byte types:**
- `byte`, `short`, `int`, `long`
- `float`, `double`
- Single operations (Add, Read) are atomic on x64

**Require external synchronization:**
- Structs >8 bytes
- Multi-operation transactions

## When to Use ListMmf vs SmallestInt

### Use ListMmf When:

1. **You need Python interoperability** (numpy, pandas, PyTorch)
2. **You want predictable behavior** (no automatic type upgrades)
3. **You prefer fail-fast semantics** (overflow throws exception)
4. **You're using standard types** (int, long, double)
5. **Storage savings < 10%** (not worth the complexity)

### Use SmallestInt When:

1. **Storage is critical** (millions of files, limited disk)
2. **Data range is truly unknown** (could be 100 or 1,000,000)
3. **You want automatic upgrades** (convenience over predictability)
4. **You're NOT exporting to Python** (odd-byte types incompatible)
5. **You accept the performance cost** (5-8x slower than standard types)

### Example Decision:

**Your Data:** OHLCV market data for 10,000 symbols
- **Range:** Prices $0.01 to $100,000 per share (in cents: 1 to 10,000,000)
- **Type needed:** Int32 (supports up to $21M)
- **Savings with SmallestInt:** ~9.4 GiB (from your stats)
- **Total dataset:** 170 GiB

**Recommendation:** Use **ListMmf<int>**
- Cost: +9.4 GiB (5.5% increase) = negligible on modern drives
- Benefit: 15-30x faster reads, Python compatible, predictable behavior
- If you need Python snapshots, the extra 9.4 GiB is well worth it

## Error Handling

### Production-Ready Pattern

```csharp
public class MarketDataWriter
{
    private readonly ListMmf<int> _prices;
    private readonly ILogger _logger;

    public void AddPrice(int priceInCents)
    {
        try
        {
            _prices.Add(priceInCents);
        }
        catch (OverflowException ex)
        {
            _logger.LogError(ex,
                "Price {Price} exceeds Int32 range (±2.1B). " +
                "Consider upgrading to Int64 or check data source.",
                priceInCents);

            // Don't crash - handle gracefully:
            // Option 1: Clamp to max value
            _prices.Add(int.MaxValue);

            // Option 2: Alert and skip
            AlertOperators("Data type overflow detected");
            return;

            // Option 3: Upgrade file type (requires downtime)
            // MigrateToLargerType();
        }
        catch (IOException ex) when (ex.Message.Contains("already open"))
        {
            _logger.LogWarning("File locked by another process, retrying...");
            Thread.Sleep(100);
            // Retry logic
        }
    }
}
```

## Migration from SmallestInt to Standard Types

If you decide to move from SmallestInt to ListMmf for better performance:

```csharp
public static void MigrateToStandardType(string sourceFile, string destFile)
{
    // Read from SmallestInt (handles all odd-byte types)
    using var source = new SmallestInt64ListMmf(DataType.Empty, sourceFile);

    // Write to standard Int32 or Int64
    using var dest = new ListMmf<int>(destFile, DataType.Int32, source.Count);

    // Bulk copy using spans (fast)
    const int chunkSize = 10_000;
    for (long i = 0; i < source.Count; i += chunkSize)
    {
        var remaining = Math.Min(chunkSize, source.Count - i);
        var chunk = source.AsSpan(i, (int)remaining);

        // Convert long to int with overflow check
        var intChunk = new int[remaining];
        for (int j = 0; j < remaining; j++)
        {
            intChunk[j] = checked((int)chunk[j]);
        }

        dest.AddRange(intChunk);
    }
}
```

## Summary Checklist

Before deploying ListMmf to production:

- [ ] ✅ Using Int32 or Int64 for numeric data (not short/byte)
- [ ] ✅ Using Double for floating-point data (not float)
- [ ] ✅ Handling OverflowException in production code
- [ ] ✅ Using checked casts if downcasting from larger types
- [ ] ✅ Pre-allocating capacity for known dataset sizes
- [ ] ✅ Using AsSpan() for bulk read operations
- [ ] ✅ Using AddRange() for bulk write operations
- [ ] ✅ Single writer pattern enforced (no multiple writers)
- [ ] ✅ Standard types if Python interop needed
- [ ] ✅ Proper Dispose() or using statements for cleanup
