# SmallestIntMmf Upgrade Optimization Project

## Objective
Eliminate "catatonic" upgrade behavior in SmallestInt64ListMmf, reducing downtime from minutes to seconds.

## Current Problem
- File moves to temporary location, becoming unavailable
- Value-by-value copying is extremely slow for millions of items
- Extended downtime during data type expansion (e.g., 1-byte to 2-byte integers)

## Current Process Flow
```csharp
File.Move(path, tmpPath);          // File unavailable!
for (var i = 0; i < count; i++)    // Millions of iterations
{
    var value = source.ReadUnchecked(i);
    destination.Add(value);         // Individual writes
}
```

## Improved Process Design
1. **Side-by-side upgrade**: Create `prices.bt.upgrading` alongside original
2. **Bulk copy operations**: Use `Buffer.MemoryCopy` for chunk-based copying
3. **Atomic swap**: Rename files when upgrade complete
4. **Crash recovery**: Clean up `.upgrading` files on startup
5. **Progress tracking**: Report upgrade progress to users

## Risk Assessment
- **Low Risk**: Original file remains available during upgrade
- **High Reward**: Reduces 30-second downtime to near-instantaneous
- **User experience**: Eliminates "frozen" application during upgrades

## Dependencies
- Independent of other projects
- Could benefit from lock removal for faster bulk operations

## Success Criteria
- Upgrade completes in < 5 seconds for typical data sizes
- Original file remains accessible during upgrade process
- Automatic recovery from interrupted upgrades
- No data loss or corruption during upgrade process

## Implementation Notes
- Trigger frequency: "probably once a day" per user description
- Acceptable brief downtime: User indicated 30 seconds is acceptable
- Price precision: Maintain integer storage for exact comparisons