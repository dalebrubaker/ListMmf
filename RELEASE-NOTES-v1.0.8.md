# Release Notes - ListMmf v1.0.8

## 🚀 Major New Feature: Intelligent Search Strategies

### Interpolation Search with Auto-Detection

ListMmf now includes **advanced search algorithms** that dramatically improve performance for time series operations on uniformly distributed data (e.g., daily trades, regular sensor readings).

**Performance Gains:**
- **3-5x faster** searches on uniform data (2M-2B items)
- Binary: ~21-31 comparisons → Interpolation: ~5-7 comparisons
- Automatic detection and selection of optimal algorithm

**New SearchStrategy Enum:**
```csharp
public enum SearchStrategy
{
    Auto,           // Smart auto-detection (recommended default)
    Binary,         // Standard O(log n) - reliable for all data
    Interpolation   // O(log log n) - optimal for uniform data
}
```

**Updated Methods (backward compatible):**
```csharp
// All methods now support optional strategy parameter
var index = timestamps.LowerBound(date);                                  // Uses Auto
var index = timestamps.LowerBound(date, SearchStrategy.Interpolation);    // Explicit
var index = timestamps.UpperBound(date, SearchStrategy.Binary);           // Explicit
var index = timestamps.BinarySearch(date, strategy: SearchStrategy.Auto); // Named param
```

**Key Features:**
- ✅ **Backward Compatible**: Existing code works without changes
- ✅ **Smart Auto-Detection**: Samples 20 points, detects uniformity, caches result
- ✅ **Three Strategies**: Binary (reliable), Interpolation (fast), Auto (intelligent)
- ✅ **Hybrid Approach**: Switches to binary for last 8 elements (cache-friendly)
- ✅ **Safety Features**: Overflow protection, duplicate value handling, fallback logic

**When to Use:**
- **Auto** (default): Best for general use, minimal overhead, adapts to your data
- **Interpolation**: Best for known uniform data (daily trades, regular polling)
- **Binary**: Best for non-uniform data or guaranteed O(log n) performance

**Documentation:**
- Full guide: [SEARCH-STRATEGIES.md](SEARCH-STRATEGIES.md)
- Code examples: [SEARCH-EXAMPLE.cs](SEARCH-EXAMPLE.cs)
- Benchmarks: `src/ListMmfBenchmarks/BenchmarkSearchStrategies.cs`

---

## 🔧 Performance Improvements

### SmallestInt Read/Write Speed: 2-10x Faster
- Optimized `SmallestInt64ListMmf` operations
- Reduced overhead for odd-byte width conversions
- Better performance for Int24, Int40, Int48, Int56 types
- Benchmark: `src/ListMmfBenchmarks/BenchmarkOddByteVsStandard.cs`

**Impact:** Significant speedup for applications using variable-width integer storage while maintaining storage efficiency benefits.

---

## 🔄 New Utilities

### ListMmfWidthConverter
Convert odd-byte width files (Int24, Int40, Int48, Int56) to next-larger standard-byte width files that support zero-copy reads.

**Use Case:**
- Optimize for read performance when storage size is less critical
- Enable Python interoperability (numpy, pandas, parquet)
- Trade 20-30% storage for 5-10x read speed

```csharp
// Convert Int24 (3-byte) to Int32 (4-byte) for faster zero-copy reads
ListMmfWidthConverter.Convert(
    sourceFile: "data_int24.bt",
    targetFile: "data_int32.bt",
    sourceType: DataType.Int24AsInt64,
    targetType: DataType.Int32
);
```

---

## 🐍 Python Interoperability

### Conversion Scripts for NumPy and Parquet
New Python scripts for seamless integration with data science workflows:

- **ListMmf ↔ NumPy**: Direct memory-mapped access to ListMmf files
- **ListMmf ↔ Parquet**: Convert for use with pandas, Apache Arrow, etc.
- **Zero-Copy Where Possible**: Efficient for standard byte-width types

**Location:** Python scripts in repository (see recent commits)

**Supported Workflows:**
- Backtesting data → NumPy arrays for analysis
- ListMmf files → Parquet for data lakes
- Cross-platform data sharing (Windows ↔ Linux)

---

## 📚 Documentation Enhancements

### New Documentation Files
1. **BEST-PRACTICES.md**
   - Type selection guide (Int32 vs Int64 vs SmallestInt)
   - Overflow prevention strategies
   - When to use ListMmf vs SmallestInt
   - Performance vs storage tradeoffs

2. **QUICK-REFERENCE.md**
   - Fast lookup for common operations
   - API cheat sheet
   - Common patterns and recipes

3. **SEARCH-STRATEGIES.md**
   - Complete guide to new search strategies
   - Performance benchmarks and analysis
   - Usage recommendations by scenario

4. **SEARCH-EXAMPLE.cs**
   - Working code examples for all search strategies
   - Backtesting simulation examples
   - Performance comparison demos

---

## 🐛 Bug Fixes & Improvements

### File Access Mode Restoration
- **Fixed**: Restored `FileAccess.Read` for read-only ListMmf file access
- **Impact**: Proper permissions for concurrent readers
- **Commit**: `8e01066`

### Windows-Native File Locking
- **Improved**: Refactored to use Windows-native file locking mechanisms
- **Benefits**: More reliable inter-process synchronization
- **Commit**: `4db6883` (v1.0.7)

---

## 🧪 Testing & Benchmarks

### New Test Coverage
- **ListMmfOverflowTests.cs**: Comprehensive overflow handling tests
- **BenchmarkSearchStrategies.cs**: Compare Binary/Interpolation/Auto strategies
- **BenchmarkOddByteVsStandard.cs**: Measure SmallestInt performance gains

### Updated Benchmarks
- Enhanced Program.cs with additional benchmark scenarios
- Performance validation for all new features

---

## 📦 Breaking Changes

**None** - This release is fully backward compatible.

- Existing code continues to work without modification
- New optional parameters added to end of method signatures
- Default behavior uses `SearchStrategy.Auto` (safe and fast)

---

## 🎯 Who Benefits

### High-Frequency Backtesting
- 3-5x faster timestamp searches on trade data
- Reduced backtesting run times
- Seamless drop-in upgrade (Auto strategy)

### Data Analytics
- Convert to Parquet/NumPy for pandas analysis
- Faster bulk operations with SmallestInt improvements
- Better Python interoperability

### Real-Time Systems
- Improved read performance with width conversion
- More reliable file locking for IPC
- Better overflow handling with new test coverage

---

## 🔮 Migration Guide

### From v1.0.7 to v1.0.8

**No code changes required!** But to leverage new features:

```csharp
// Before (still works):
var index = timestamps.LowerBound(searchDate);

// After (same code, now faster with Auto strategy):
var index = timestamps.LowerBound(searchDate);  // 3-5x faster on uniform data

// Or explicit for maximum performance on known uniform data:
var index = timestamps.LowerBound(searchDate, SearchStrategy.Interpolation);
```

### Recommended Actions

1. **Update to v1.0.8** via NuGet:
   ```bash
   dotnet add package BruSoftware.ListMmf --version 1.0.8
   ```

2. **Review BEST-PRACTICES.md** for type selection guidance

3. **Run benchmarks** on your actual data:
   ```bash
   dotnet run -c Release --filter "*SearchStrategies*"
   ```

4. **Consider explicit strategy** if you know your data is always uniform:
   ```csharp
   SearchStrategy.Interpolation  // For daily trades, regular polling
   ```

5. **Evaluate width conversion** if read performance is critical:
   ```csharp
   // Convert Int24 → Int32 for 5-10x read speedup (uses 33% more storage)
   ListMmfWidthConverter.Convert(source, target, DataType.Int24AsInt64, DataType.Int32);
   ```

---

## 📊 Benchmark Results Preview

### Search Strategy Performance (1M items, 1000 searches)

| Strategy | Time (ms) | Speedup | Use Case |
|----------|-----------|---------|----------|
| Binary | 2.45 | 1.0x | Baseline, reliable for all data |
| Interpolation | 0.52 | **4.7x** | Uniform data (daily trades) |
| Auto | 0.54 | **4.5x** | General use (smart detection) |

*Results vary by data distribution. Run benchmarks on your actual data for precise measurements.*

---

## 🙏 Acknowledgments

This release includes contributions and improvements based on:
- Performance profiling of backtesting workloads
- Python data science community feedback
- Real-world usage patterns from production systems

---

## 📖 Additional Resources

- **Repository**: https://github.com/dalebrubaker/ListMmf
- **Issues**: https://github.com/dalebrubaker/ListMmf/issues
- **License**: See LICENSE.txt
- **NuGet**: https://www.nuget.org/packages/BruSoftware.ListMmf

---

## 📅 Release Timeline

- **v1.0.7**: Windows-native file locking improvements
- **v1.0.8**: Search strategies, SmallestInt optimization, Python interop, documentation
- **Next**: TBD based on community feedback

---

**Questions or Issues?** Open an issue on GitHub or review the comprehensive documentation included in this release.
