ListMmf
=

Data Architecture
The system uses memory-mapped files (MMF) for ultra-fast data access:

ListMmf - High-performance memory-mapped collections
Variable-width data files optimized for different data types
Tick-by-tick historical data with real-time updates
All timestamps stored as Unix seconds for performance@

List\<T\> using Memory Mapped Files (MMFs).

Support IList64\<T\>, similar to IList\<T\> but using Int64 values wherever
possible. (For example, copying into an array uses Int32 because the maximum
array length is Int32.MaxValue.)

This library can only be run in a 64-bit process, a design choice to avoid locking issues with values 8 bytes or smaller. (Reads and writes on a 64-bit processor are atomic when the size is 8 bytes or smaller.) Unsafe reads and writes are used, using System.Runtime.CompilerServices.Unsafe. Structures larger than 8 bytes could have threading issues, depending on your design choices.

BruTrader Data Structure
-

BruTrader stores market data in a hierarchical directory structure using memory-mapped files for high-performance access. The data is organized as follows:

### Root Directory: C:\BruTrader21Data\data

The root contains directories for each instrument type (from InstrumentType enum):
- `Future/` - Futures contracts
- `Index/` - Index instruments
- `Stock/` - Stock data

### Symbol Organization

Under each instrument type directory, data is organized by symbol. For example, under `Future/`:
- `ES/` - E-mini S&P 500
- `MES/` - Micro E-mini S&P 500
- `M2K/` - Micro Russell 2000
- `M6A/`, `M6B/`, `M6E/` - Currency futures
- etc.

### Contract Organization

Under each symbol directory (e.g., `MES/`), contracts are organized by:
- `MES#/` - Continuous contract (automatically rolls to front month)
- `MES&/` - Back-adjusted continuous contract
- `MES201906/`, `MES201909/`, etc. - Individual contract months

### Bar Type Organization

Under each contract directory, data is organized by bar type:
- `1T/` - Tick data (trades only)
- `1M/` - 1-minute bars
- `1D/` - Daily bars

### Data Files

Each bar type directory contains binary data files in ListMmf format:

**For minute and daily bars (1M, 1D):**
- `Bars.hdr` - Header file (256 bytes)
- `Opens.bt` - Open prices
- `Highs.bt` - High prices
- `Lows.bt` - Low prices
- `Closes.bt` - Close prices
- `Volumes.bt` - Volume data
- `Timestamps.bt` - Unix timestamps
- `AskVolumes.bt` - Ask volume data
- `BidVolumes.bt` - Bid volume data
- `NumTrades.bt` - Number of trades

**For tick data (1T):**
- `Bars.hdr` - Header file (256 bytes)
- `Closes.bt` - Trade prices
- `Volumes.bt` - Trade volumes
- `Timestamps.bt` - Unix timestamps (4 bytes per tick, Int32)
- `Aggressors.bt` - Aggressor side indicator
- `NumTrades.bt` - Trade count

Note: All timestamps are stored as Unix seconds in Int32 format (4 bytes) using ListMmfTimeSeriesDateTimeSeconds. Even with 6 years of tick data, the Timestamps.bt file can exceed 3GB due to the high frequency of trades.

Backtesting Performance
-

During backtesting, BruTrader creates hundreds of thousands of orders that must be evaluated at every bar for potential fills. The system uses ListMmf's high-performance binary search capabilities to achieve dramatic speedups:

### Order Fill Detection

When checking if an order should be filled at a specific price:

1. **DataFilesSearch.cs** - Uses `LowerBound()` on timestamp files to quickly find the first index of a timestamp
2. **PriceReachedChecker.cs** - Determines which price level (stop/limit) was reached first within a bar
3. **Parallel Arrays** - After finding the timestamp index, the system uses direct array indexing into parallel ListMmf arrays (Closes, Highs, Lows, etc.) to retrieve price values

### Performance Impact

- **With ListMmf**: A complex backtest with hundreds of strategies can complete in 1-2 hours
- **Without fast file access**: The same backtest could take a day or even a week
- **Key optimization**: `LowerBound()` provides O(log n) binary search on sorted timestamp data
- **Memory efficiency**: Data stays memory-mapped, avoiding RAM limitations even with massive datasets

This architecture enables institutional-grade backtesting performance while maintaining data integrity and accuracy.

View
-

There is only one view of the entire file (which could be a persisted File or
could be a non-persisted Memory-based MMF).

Header
-

The first headerReserveBytes (if any) are reserved by use by the creator of the
ListMmf instance. ListMmf requires this to be evenly divisible by 8 (in case alignment
matters).

The next 8 bytes are a long value of the array Count.

So the actual list values are an array starting at headerReserveBytes + 8 into
the file.

The List\<T\> Version field is NOT included. Slowing every write down seems a
very poor trade-off for the very minimal benefit of throwing an “Enumeration
modified” error during enumeration.

SmallestInt and SmallestEnum
=
The Smallest... classes support storage of integers in minimal bit-widths, from a bit array (1-bit width) to byte sizes from 1 to 8 and both unsigned and unsigned (except 8-byte unsigned). Internally each list element is an Int64, so the change only affects disk usage, not memory usage. But this also dramatically affects read/write performance, as there are dramatically fewer bytes to transfer in so many cases.

A "smallest" file can start out at a default size (SmallestInt64ListMmf.WidthBits), then the file will be automatically upgraded when a value is added that is too large or too small to fit in that width. For example, the Dow Jones Industrial Average (DJIA) currently fits in a UInt16(unsigned 2-byte). When it hits 65,536 the file would be upgraded to a 3-byte size, UInt24AsInt64. The file will have to increase to 4 bytes when the DJIA hits 16,777,215. 

Many times floating-precision number can be handled internally as integers. For example S&P Futures have a tick size of 0.25, so the integer can be stored as the price divided by the tick size (being careful about the conversion). This approach provides all the inherent speed and accuracy benefits of integers compared to floating point numbers.

Interesting References
=

<https://devblogs.microsoft.com/oldnewthing/20151218-00/?p=92672>

<https://stackoverflow.com/questions/11146352/why-memory-mapped-files-are-always-mapped-at-page-boundaries>

<https://stackoverflow.com/questions/49339804/memorymappedviewaccessor-performance-workaround>

<https://stackoverflow.com/questions/7956167/how-can-i-quickly-read-bytes-from-a-memory-mapped-file-in-net>

<https://stackoverflow.com/questions/23835932/atomic-unlocked-access-to-64bit-blocks-of-memory-mapped-files-in-net>
