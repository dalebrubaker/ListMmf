# ListMmf Architecture Notes

These are internal design notes for maintainers. Consumer-facing NuGet documentation lives in the repository root `README.md`.

## Data Architecture

The system uses memory-mapped files (MMF) for ultra-fast data access:

- ListMmf - High-performance memory-mapped collections
- Variable-width data files optimized for different data types
- Tick-by-tick historical data with real-time updates
- Timestamps may be stored as Unix seconds for performance-sensitive series

List\<T\> using Memory Mapped Files (MMFs).

Support IList64\<T\>, similar to IList\<T\> but using Int64 values wherever
possible. (For example, copying into an array uses Int32 because the maximum
array length is Int32.MaxValue.)

This library can only be run in a 64-bit process, a design choice to avoid locking issues with values 8 bytes or smaller. (Reads and writes on a 64-bit processor are atomic when the size is 8 bytes or smaller.) Unsafe reads and writes are used, using System.Runtime.CompilerServices.Unsafe. Structures larger than 8 bytes could have threading issues, depending on your design choices.

## View

There is only one view of the entire file (which could be a persisted File or
could be a non-persisted Memory-based MMF).

## Concurrent reads during remap and Dispose

Growing or shrinking the file replaces the MMF and view, and the old view is
unmapped. Reading through an unmapped view is an `AccessViolationException`,
which kills the process, so readers must never hold a pointer into a view
that is being unmapped.

Within one instance there is one writer thread, and any number of threads may
call `Count`, the indexer, `ReadUnchecked`, `Version` and `DataType` at the
same time, including during a remap or `Dispose`. These reads take no lock:

- `ListMmfBase<T>` publishes an immutable `MmfMapping` (base, Count and array
  pointers) in one field. Readers load it once per call and use only it, so
  the indexer's Count and item come from the same mapping.
- Each read marks its thread's `MmfReaders.Slot` with the list it is reading,
  loads the mapping, reads, and clears the slot. Nothing shared is written, so
  a read costs a few nanoseconds and readers don't contend with each other.
- A remap publishes `MmfMapping.Remapping`, and `Dispose` publishes `null`.
  Then `MmfReaders.WaitForReaders` calls
  `Interlocked.MemoryBarrierProcessWide()` and waits for every slot marked
  with this list to clear. After the barrier, each reader either has a mark
  the writer can see (so the writer waits for it) or loads the mapping after
  the publish (so it gets the replacement). Only then is the old view
  unmapped.
- Readers that see `Remapping` clear their slot and spin until the new
  mapping is published, so `Count` never reads 0 in the middle of a remap.
  After `Dispose`, `Count` returns 0 and item reads throw
  `ObjectDisposedException` (the indexer throws `ListMmfTruncatedException`
  because it sees Count 0).
- `SyncRoot` now only serializes remap and `Dispose` against each other.

Spans from `AsSpan` live outside any read section. Don't use one after the
list has been remapped (grown or shrunk) or disposed.

## Header

The first headerReserveBytes (if any) are reserved by use by the creator of the
ListMmf instance. ListMmf requires this to be evenly divisible by 8 (in case alignment
matters).

The next 8 bytes are a long value of the array Count.

So the actual list values are an array starting at headerReserveBytes + 8 into
the file.

The List\<T\> Version field is NOT included. Slowing every write down seems a
very poor trade-off for the very minimal benefit of throwing an “Enumeration
modified” error during enumeration.

## SmallestInt and SmallestEnum

The Smallest... classes support storage of integers in minimal bit-widths, from a bit array (1-bit width) to byte sizes from 1 to 8 and both unsigned and unsigned (except 8-byte unsigned). Internally each list element is an Int64, so the change only affects disk usage, not memory usage. But this also dramatically affects read/write performance, as there are dramatically fewer bytes to transfer in so many cases.

A "smallest" file can start out at a default size (SmallestInt64ListMmf.WidthBits), then the file will be automatically upgraded when a value is added that is too large or too small to fit in that width. For example, the Dow Jones Industrial Average (DJIA) currently fits in a UInt16(unsigned 2-byte). When it hits 65,536 the file would be upgraded to a 3-byte size, UInt24AsInt64. The file will have to increase to 4 bytes when the DJIA hits 16,777,215. 

Many times floating-precision number can be handled internally as integers. For example S&P Futures have a tick size of 0.25, so the integer can be stored as the price divided by the tick size (being careful about the conversion). This approach provides all the inherent speed and accuracy benefits of integers compared to floating point numbers.

## Long-facing adapter

`UtilsListMmf.OpenAsInt64(path, access)` exposes any numeric file (standard or odd-byte width) as an `IListMmf<long>` by creating a `ListMmf<T>` underneath and wrapping it in `ListMmfLongAdapter<T>`. The adapter:

* Uses the new `Int64Conversion<T>` helpers to expand odd-byte structs (Int24/UInt24/Int40/…) into pooled `long` spans without per-call allocations.
* Performs checked writes and throws `DataTypeOverflowException` with the suggested upgrade width instead of silently truncating.
* Provides `GetDataTypeUtilization()` and `ConfigureUtilizationWarning()` so callers can log when the stored values approach the limits of the current `DataType`.

This enables downstream services to keep their APIs in terms of `long` while preserving the compact on-disk representation that SmallestInt created.

## Interesting References

<https://devblogs.microsoft.com/oldnewthing/20151218-00/?p=92672>

<https://stackoverflow.com/questions/11146352/why-memory-mapped-files-are-always-mapped-at-page-boundaries>

<https://stackoverflow.com/questions/49339804/memorymappedviewaccessor-performance-workaround>

<https://stackoverflow.com/questions/7956167/how-can-i-quickly-read-bytes-from-a-memory-mapped-file-in-net>

<https://stackoverflow.com/questions/23835932/atomic-unlocked-access-to-64bit-blocks-of-memory-mapped-files-in-net>
