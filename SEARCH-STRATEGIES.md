# Search Strategy Optimization for ListMmfTimeSeriesDateTimeSeconds

## Overview

ListMmf now supports multiple search strategies for `LowerBound`, `UpperBound`, and `BinarySearch` operations, with automatic detection and selection of the optimal algorithm based on your data distribution.

## Performance Gains

For **uniformly distributed time series data** (e.g., daily trades from 1996-2025):
- **Binary Search**: ~21-31 comparisons for 2M-2B items (O(log n))
- **Interpolation Search**: ~5-7 comparisons for 2M-2B items (O(log log n))
- **Expected Speedup**: 3-5x faster on uniform data

## Search Strategies

### `SearchStrategy.Auto` (Recommended Default)
Automatically detects if your data is uniformly distributed and chooses the optimal strategy:
- Uses **Interpolation Search** for uniform data (>10K items, <15% variance)
- Falls back to **Binary Search** for non-uniform or small datasets
- Caches uniformity detection result for subsequent searches
- **Best for**: General use when data characteristics are unknown or may vary

### `SearchStrategy.Binary`
Always uses standard binary search (O(log n)):
- Reliable for all data distributions
- Predictable performance
- **Best for**: Non-uniform data, small datasets (<10K), or when consistency is critical

### `SearchStrategy.Interpolation`
Always uses interpolation search (O(log log n) for uniform data):
- Significantly faster on uniformly distributed data
- May be slower than binary on non-uniform data
- **Best for**: Known uniform distributions (e.g., daily trades, regular sensor readings)

## API Usage

### Basic Usage (Default Auto Strategy)

```csharp
using var timestamps = new ListMmfTimeSeriesDateTimeSeconds(path, TimeSeriesOrder.Ascending);

// Auto strategy - recommended for most use cases
var index = timestamps.LowerBound(searchDate); // Uses Auto by default
```

### Explicit Strategy Selection

```csharp
// Force binary search (most reliable)
var index = timestamps.LowerBound(searchDate, SearchStrategy.Binary);

// Force interpolation search (fastest for uniform data)
var index = timestamps.LowerBound(searchDate, SearchStrategy.Interpolation);

// Explicit auto (same as default)
var index = timestamps.LowerBound(searchDate, SearchStrategy.Auto);
```

### All Supported Methods

```csharp
// LowerBound
var idx1 = timestamps.LowerBound(date);
var idx2 = timestamps.LowerBound(date, SearchStrategy.Interpolation);
var idx3 = timestamps.LowerBound(first, last, date, SearchStrategy.Binary);

// UpperBound
var idx4 = timestamps.UpperBound(date);
var idx5 = timestamps.UpperBound(date, SearchStrategy.Auto);
var idx6 = timestamps.UpperBound(first, last, date, SearchStrategy.Interpolation);

// BinarySearch
var idx7 = timestamps.BinarySearch(date);
var idx8 = timestamps.BinarySearch(date, index: 0, length: count, strategy: SearchStrategy.Binary);
```

## When to Use Each Strategy

### Use `Auto` (Default) When:
- You're unsure about data distribution
- Data characteristics may vary between files
- You want "set it and forget it" optimal performance
- Minimal overhead (one-time uniformity check)

### Use `Binary` When:
- You know data is non-uniform (e.g., event logs, irregular timestamps)
- You need guaranteed O(log n) worst-case performance
- Dataset is small (<10K items) where interpolation has no benefit
- You want zero auto-detection overhead

### Use `Interpolation` When:
- You **know** data is uniformly distributed
- Dataset is large (>10K items) with consistent intervals
- You want maximum speed for backtesting/analytics
- Examples: daily trades, hourly sensor readings, regular polling data

## Uniformity Detection

The `Auto` strategy detects uniformity by:
1. Sampling 20 strategic points across the dataset
2. Calculating average deviation from expected uniform distribution
3. If deviation < 15%, uses interpolation; otherwise uses binary
4. Result is cached - detection happens once per file instance

You can disable this by explicitly choosing `Binary` or `Interpolation`.

## Backtesting Recommendation

For **backtesting with daily trade data** (your primary use case):

```csharp
// Option 1: Use Auto (safest, still very fast)
var index = timestamps.LowerBound(searchDate); // Auto is default

// Option 2: Force Interpolation if you know data is uniform
var index = timestamps.LowerBound(searchDate, SearchStrategy.Interpolation);
```

**For 2M-2B timestamp files with uniform daily trades**, expect:
- Auto strategy: 5-7 comparisons per search (after one-time detection)
- Interpolation: 5-7 comparisons per search
- Binary: 21-31 comparisons per search

## Performance Testing

Run the included benchmarks to test on your actual data:

```bash
cd src/ListMmfBenchmarks
dotnet run -c Release --filter "*SearchStrategies*"
```

This will:
- Test all three strategies on uniform and non-uniform data
- Compare performance at 1M, 10M, and 100M item scales
- Show memory usage for each approach
- Validate that Auto correctly chooses the optimal strategy

## Implementation Details

- **Interpolation Search**: Estimates position based on value distribution
- **Hybrid Finish**: Switches to binary for last 8 elements (cache-friendly)
- **Overflow Protection**: Safe for all date ranges and file sizes
- **Fallback Safety**: Gracefully handles edge cases (duplicate values, empty ranges)

## Breaking Changes

**None** - This is a backward-compatible addition:
- Default behavior (no strategy parameter) uses `Auto`
- Existing code continues to work without modification
- New optional `strategy` parameter added to end of method signatures

## Questions?

- For performance issues, try `SearchStrategy.Interpolation` explicitly
- For reliability concerns, use `SearchStrategy.Binary`
- For best overall experience, stick with `SearchStrategy.Auto` (default)
