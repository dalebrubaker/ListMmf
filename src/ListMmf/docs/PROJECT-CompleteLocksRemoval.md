# ListMmf Complete Lock Removal Project

## Executive Summary

Remove ALL locking from ListMmf to achieve maximum performance, leveraging BarsWriter's single-instance pattern and
adopting .NET's `List<T>` philosophy where thread safety is the caller's responsibility.

## Rationale

1. **No Read-Only Usage**: Analysis shows no production use of read-only access (except one header read that can be
   changed)
2. **Single Writer Pattern**: BarsWriterManager already ensures single-instance access via registration/deregistration
3. **.NET Consistency**: Match `List<T>` behavior - no synchronization, caller responsible for thread safety
4. **Maximum Performance**: Eliminate all lock overhead for critical binary search operations

## Implementation Strategy

### Phase 1: Remove Read-Only Support

1. **Eliminate MemoryMappedFileAccess.Read**
    - Constructor only accepts `ReadWrite`, remove the access parameters
    - Remove `IsReadOnly` checks and branches
    - Simplify `_funcGetCount` and `_funcRead` to single implementations

2. **Update BarsId.GetBarsWriterHeader()**
    - Change from `Read` to `ReadWrite` access
    - This is the only production read-only usage found

### Phase 2: Remove All Locking

1. **Delete all `lock (SyncRoot)` statements**
    - Remove from all property getters/setters
    - Remove from all methods
    - Keep `SyncRoot` object only if needed for backward compatibility

2. **Simplify method signatures**
    - Remove `UnsafeReadNoLock` variants - make them the default
    - Remove lock/no-lock distinction in method names

3. **Handle capacity expansion**
    - Let pointers become invalid during expansion (like `List<T>`)
    - Document that concurrent access during expansion is undefined behavior
    - Writer is responsible for ensuring no concurrent access during Add operations

### Phase 3: Optimize Critical Paths

1. **Inline critical methods**
   ```csharp
   [MethodImpl(MethodImplOptions.AggressiveInlining)]
   public T ReadUnchecked(long index) => Unsafe.Read<T>(_ptrArray + index * _width);
   ```

2. **Remove capacity checks in readers**
    - No more checking if writer expanded capacity
    - No more `ResetMmfAndView` calls during reads

3. **Direct pointer access**
    - Expose pointer arithmetic directly where beneficial
    - Eliminate method call overhead for tight loops

## Expected Behavior Changes

### Before (Current)

- Thread-safe reads with automatic capacity detection
- Readers block during writer operations
- Automatic pointer refresh on capacity changes

### After (Lock-Free)

- **NOT thread-safe** - caller must synchronize
- Undefined behavior if accessing during expansion
- Pointers may become invalid - crashes are possible
- Matches `List<T>` semantics exactly

## Risk Mitigation

1. **Clear documentation**: "This collection is not thread-safe"
2. **Aggressive pre-allocation**: Start with large capacity (1GB+) to minimize expansions
3. **Single-writer enforcement**: BarsWriterManager already provides this
4. **Graceful degradation**: Accept that readers may crash during expansion (rare event)

## Migration Path

1. **Update all ListMmf instantiations** to use ReadWrite only
2. **Remove Read access** from all code paths
3. **Test thoroughly** with production-size data files
4. **Monitor for crashes** during expansion events

## Success Metrics

- **Performance**: 15-30% improvement in binary search operations
- **Simplicity**: ~500 lines of code removed
- **Consistency**: Behavior matches .NET collections
- **Reliability**: No increase in crashes during normal operation

## Implementation Notes

- Start with `ListMmfBase.cs` as the core change
- Propagate changes to derived classes (`ListMmfTimeSeriesDateTimeSeconds.cs`, etc.)
- Update all tests to remove read-only scenarios
- Consider keeping a "legacy" locked version initially for rollback capability

## Caveats

- **Breaking Change**: Existing code expecting thread safety will fail
- **No Read-Only Monitoring**: Debugging tools can't safely read files during writes
- **Crash Potential**: Readers accessing during expansion will crash
- **No Rollback**: Once deployed, can't easily go back to locked version

## Conclusion

This approach fully embraces the "performance at all costs" philosophy, accepting that occasional crashes during
expansion are preferable to constant locking overhead. Given the single-writer pattern and aggressive pre-allocation,
expansion events should be extremely rare in production.

## Future Work

Look at the usages of @PROReadUnchecked etc. and see if we could get much better performance using GetRange and perhaps
a zero-copy Span<T> approach could yield significant performance gains for bulk operations.