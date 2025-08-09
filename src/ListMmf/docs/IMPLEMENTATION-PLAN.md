# ListMmf Performance Optimization Implementation Plan

## Overview
Three-phase optimization of ListMmf for high-performance trading data access, leveraging BarsWriter's single-instance pattern and addressing critical performance bottlenecks.

## Context & Requirements
- **Performance Critical**: Backtests complete in 1-2 hours vs days/weeks without MMF
- **Binary Search Heavy**: LowerBound() operations on timestamps for order fill detection
- **Integer Pricing**: Prices stored as `price/tickSize` integers for precision (e.g., MES tickSize=0.25)
- **Single Writer**: BarsWriter provides single point of control, eliminating multi-reader coordination
- **Cross-Platform**: MMF already works on Windows/macOS/Linux via .NET

## Phase 1: SmallestIntUpgrade Optimization (CURRENT)

### Problem Statement
Current "catatonic" upgrade process:
```csharp
File.Move(path, tmpPath);          // File becomes unavailable immediately
for (var i = 0; i < count; i++)    // Value-by-value copy - millions of iterations
{
    var value = source.ReadUnchecked(i);
    destination.Add(value);
}
```

### Target Architecture
1. **Side-by-side upgrade**: Write to `filename.bt.upgrading`
2. **Bulk operations**: Use `Buffer.MemoryCopy` for chunk copying
3. **Atomic swap**: Rename when complete
4. **Crash recovery**: Clean `.upgrading` files on startup

### Current Catatonic Implementation (Lines 452-491)
```csharp
File.Move(path, tmpPath);                    // FILE BECOMES UNAVAILABLE!
using (var source = new SmallestInt64ListMmf(dataTypeExisting, tmpPath, 0L, MemoryMappedFileAccess.Read))
{
    using var destination = new SmallestInt64ListMmf(dataTypeNew, path, source.Capacity);
    for (var i = 0; i < count; i++)          // MILLIONS OF ITERATIONS!
    {
        var value = source.ReadUnchecked(i);  // Individual read
        destination.Add(value);               // Individual write + capacity checks
    }
}
```

### Implementation Steps
1. ✅ **COMPLETED**: Analyze current SmallestInt64ListMmf upgrade logic (SmallestInt64ListMmf.cs:452-491)
2. Design new side-by-side upgrade process with `.upgrading` extension
3. Replace value-by-value copy with bulk buffer operations
4. Implement atomic file swap mechanism  
5. Add crash recovery for interrupted upgrades (cleanup `.upgrading` files)
6. Test with realistic data sizes and benchmark against current implementation

### Success Criteria
- Upgrade time < 5 seconds (from current minutes)
- Original file remains accessible during upgrade
- No data loss during interrupted upgrades
- Automatic cleanup of temporary files

