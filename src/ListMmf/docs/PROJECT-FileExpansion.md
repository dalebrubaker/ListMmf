# ListMmf File Expansion Safety Project

## Objective
Improve file expansion handling to prevent race conditions during capacity growth while maintaining performance.

## Current Problem
- `ResetMmfAndView()` invalidates memory pointers during expansion
- Binary searches using stale pointers during mid-operation expansion
- Potential for crashes or incorrect results during growth

## Technical Challenge
- Memory mapping invalidation cannot be made atomic
- Expansion triggers `_ptrArray` pointer changes
- Detection of stale pointers needs to be efficient

## Implementation Strategy
1. **Aggressive pre-allocation**: Minimize expansion frequency
2. **Expansion detection**: Version counter or pointer validation
3. **Safe expansion window**: Coordinate with BarsWriter operations
4. **Recovery mechanism**: Graceful handling of expansion during operations

## Risk Assessment
- **Medium Risk**: Complex coordination between expansion and operations
- **Medium Reward**: Eliminates a source of potential crashes
- **Data integrity critical**: Must not corrupt existing data

## Dependencies
- Should be done after lock removal to avoid conflicting changes
- Requires understanding of BarsWriter operation patterns

## Success Criteria
- No data corruption during expansion
- Expansion operations complete reliably
- Performance maintained during non-expansion operations
- Clear logging of expansion events