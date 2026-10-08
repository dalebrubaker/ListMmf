# Release Notes

## 1.4.1

- fix: `Truncate` now only reduces `Count`, as documented on `IListMmf.Truncate`; it no longer changes `Capacity` or remaps the file.
  - Previously `Truncate` called `ResetCapacity`, which threw `ResetPointersDisallowedException` after `DisallowResetPointers()` and could invalidate pointers/spans held by readers.
  - Applies to `ListMmfBase<T>` and `ListMmfBitArray` (including `ListMmfBitArray.TruncateBeginning`).
  - `ListMmfBitArray.Truncate` clears the discarded bits so they cannot reappear when the array grows again.
  - Space is still reclaimed by `TrimExcess()`/`Capacity`, and by `Dispose()` when pointers are not locked.

Internal: Added regression tests for truncating with locked pointers (list and bit array) and for dispose-time trimming.

## 1.4.0

- fix: guard memory-mapped pointer access during capacity growth and disposal
  - Serializes count, version, data type, and array pointer access through `SyncRoot`.
  - Clears stale pointers before resetting or disposing views to avoid dereferencing invalid memory.
  - Adds regression coverage for concurrent count/index reads while capacity grows.
- fix: allocate tracker IDs with `Interlocked.Increment` to avoid races.
- feat: add default `IProgressReport` members:
  - `LogMessage(string message)`
  - `UpdateDescription(string description)`
  - `CancellationToken Token`
  - Existing implementations do not need changes because the new members provide defaults.
- build: update SourceLink and logging dependencies, refresh test/benchmark tooling, and simplify platform configuration to `AnyCPU`.
- docs: consolidate internal design notes and update project documentation.

Internal: Added regression tests for stale-pointer access during capacity expansion.

## 1.1.1

- fix: prevent potential hang in interpolation search loops for `ListMmfTimeSeriesDateTimeSeconds`
  - Ensures progress when interpolation lands on `pos == high` by decrementing `high` (and symmetric guard in upper bound).
  - Affected methods: `InterpolationLowerBound`, `InterpolationUpperBound`.
  - Symptom: Rare infinite loop (observed as a computational hang) during `LowerBound`/`UpperBound` when searching near the end of a large, uniformly increasing dataset.
  - Impact: No API changes; correctness preserved. Performance characteristics unchanged aside from eliminating the hang.

Internal: Added regression tests to cover last-element interpolation edge cases.
