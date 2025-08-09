# ListMmf Performance Benchmarks

This project contains performance benchmarks comparing memory-mapped files (MMF) against direct file I/O for binary search operations on large timestamp datasets.

## Background

BruTrader's backtesting engine performs hundreds of thousands of order fill checks, using `LowerBound()` binary searches on timestamp files to quickly determine when price levels are reached. The current implementation uses Windows-only memory-mapped files for ultra-fast data access.

## Research Question

**Can cross-platform file I/O approaches achieve similar performance to memory-mapped files?**

This would enable iOS/Mac deployment while maintaining acceptable backtesting speeds (currently 1-2 hours vs days/weeks without MMF).

## Test Configuration

- **Dataset**: MES futures tick data (2.96 GB, 776 million Int32 timestamps)
- **Test**: 1,000 binary searches per benchmark run
- **Expected seeks per search**: ~29.5 (log₂ of dataset size)
- **File structure**: 16-byte header + sorted Int32 timestamp array
- **Platform**: Windows 11, Intel Core i7-14700, .NET 9.0

## Benchmark Results

| Implementation | Time (1000 searches) | Ratio vs MMF | Notes |
|----------------|---------------------|--------------|-------|
| **Memory-Mapped Files** | **312 µs** | **1.00x (baseline)** | Current production approach |
| **File I/O (Span<T>)** | 38,810 µs (38.8 ms) | **124.5x slower** | Direct seek + ReadExactly per comparison |
| **File I/O (BinaryReader)** | 38,924 µs (38.9 ms) | **124.9x slower** | Similar to Span approach |
| **Optimized Buffered I/O** | 79,567 µs (79.6 ms) | **255.2x slower** | 64KB buffer caching (failed optimization) |

## Key Findings

### 1. MMF Performance Advantage is Substantial
- Memory-mapped files are **~125x faster** than direct file I/O
- This translates to **backtesting time difference**:
  - With MMF: 1-2 hours
  - Without MMF: 5-10 days (still impractical)

### 2. Span<T> vs Initial Expectations
- Span<T> approach is indeed more efficient than initially measured
- However, still nowhere near MMF performance for practical use
- The ~125x performance gap makes cross-platform deployment challenging

### 3. Why Buffered Optimization Failed
The 64KB buffered approach performed **worse** than naive file I/O because:
- Binary search has a random access pattern that jumps around the file
- Large buffers get invalidated after just one read in most cases
- Buffer management overhead exceeds any caching benefits
- Sequential reading assumptions don't apply to binary search workloads

### 4. File I/O Implementation Details
```csharp
// Each binary search step requires:
file.Seek(headerOffset + (position * sizeof(int)), SeekOrigin.Begin);
file.ReadExactly(buffer); // 4 bytes
int value = MemoryMarshal.Read<int>(buffer);
```

With ~30 seeks per search × 1000 searches = ~30,000 individual file operations, the overhead accumulates significantly compared to MMF's direct memory access.

## Implications for Cross-Platform Strategy

### Current Status
- **Windows**: Production-ready with MMF (1-2 hour backtests)
- **Cross-platform**: Would require 5-10 day backtests (impractical)

### Potential Solutions
1. **Accept slower backtests** on non-Windows platforms
2. **Alternative architectures**:
   - Preload entire datasets into RAM
   - Use database solutions with better cross-platform performance
   - Implement custom memory mapping for macOS/Linux
3. **Hybrid approach**: Different performance tiers per platform

## Validation

All file I/O implementations have been validated to return identical results to the proven MMF implementation via comprehensive unit tests (`LowerBoundValidationTests.cs`).

## Running the Benchmarks

```bash
cd C:\GitDev\BruTrader22\csharp\ListMmf\src\ListMmfBenchmarks
dotnet run --configuration Release lowerbound
```

**Note**: Requires the test data file at `C:\BruTrader21Data\Data\Future\MES\MES#\1T\Timestamps.bt`

## Technical Implementation

### Memory-Mapped Files (Production)
```csharp
var mmfTimestamps = new ListMmfTimeSeriesDateTimeSeconds(filePath, TimeSeriesOrder.Ascending);
var index = mmfTimestamps.LowerBound(searchTime); // Direct memory access
```

### File I/O (Cross-platform)
```csharp
// Binary search with individual file seeks
private static long LowerBoundFile(FileStream file, int target, long count)
{
    const int itemSize = sizeof(int);
    long first = 0;
    Span<byte> buffer = stackalloc byte[itemSize];
    
    while (count > 0)
    {
        long step = count / 2;
        long pos = first + step;
        
        long fileOffset = HeaderSize + (pos * itemSize);
        file.Seek(fileOffset, SeekOrigin.Begin);
        file.ReadExactly(buffer);
        int value = MemoryMarshal.Read<int>(buffer);
        
        if (value < target)
        {
            first = pos + 1;
            count -= step + 1;
        }
        else
        {
            count = step;
        }
    }
    
    return first;
}
```

## Conclusion

While the file I/O implementation using Span<T> is efficient and cross-platform compatible, the **125x performance penalty** makes it impractical for maintaining current backtesting speeds. Memory-mapped files remain essential for the high-performance trading workloads that BruTrader is designed to handle.

The research confirms that **Windows-specific optimizations are necessary** for this use case, and cross-platform deployment would require accepting significantly longer backtesting times or architectural changes to the data access patterns.