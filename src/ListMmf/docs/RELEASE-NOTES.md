# Release Notes

## 1.5.0

- perf: `Count`, the indexer, `ReadUnchecked`, `Version` and `DataType` no longer take `SyncRoot`.
  - Readers use an atomically published mapping snapshot. A remap or `Dispose` unmaps the old view only after every thread reading through it has finished (see "Concurrent reads during remap and Dispose" in ARCHITECTURE.md).
  - Still safe to read from other threads while the writer grows/shrinks the file or disposes the list. `Count` still returns 0 after `Dispose`.
  - The `ListMmf<T>`, `ListMmfTimeSeriesDateTime` and `ListMmfTimeSeriesDateTimeSeconds` indexers read Count and the item from the same mapping, in one pass.
- feat: `ListMmfBase<T>` adds protected `TryReadInBounds`, `TryReadHeader` and `TryWriteHeader` for subclasses.
- fix: the `ListMmf<T>`, `ListMmfTimeSeriesDateTime` and `ListMmfTimeSeriesDateTimeSeconds` indexer bounds checks compared against `(uint)Count`, which rejected valid indexes once a list held more than `uint.MaxValue` items.
- fix: `ListMmfBitArray.Length`/`Count` after `Dispose` read through a stale pointer; they now return 0.

Internal: Added tests for concurrent reads while the writer remaps repeatedly and while the list is disposed, and a `BenchmarkCountRead` benchmark (`dotnet run -c Release --project src/ListMmfBenchmarks -- count`).

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
