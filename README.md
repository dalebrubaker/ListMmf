ListMmf
=

List\<T\> using Memory Mapped Files (MMFs).

Support IList64\<T\>, similar to IList\<T\> but using Int64 values wherever
possible. (For example, copying into an array uses Int32 because the maximum
array length is Int32.MaxValue.)

This library can only be run in a 64-bit process, a design choice to avoid locking issues with values 8 bytes or
smaller. (Reads and writes on a 64-bit processor are atomic when the size is 8 bytes or smaller.) Unsafe reads and
writes are used, using System.Runtime.CompilerServices.Unsafe. Structures larger than 8 bytes could have threading
issues, depending on your design choices.

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
The Smallest... classes support storage of integers in minimal bit-widths, from a bit array (1-bit width) to byte sizes
from 1 to 8 and both unsigned and unsigned (except 8-byte unsigned). Internally each list element is an Int64, so the
change only affects disk usage, not memory usage. But this also dramatically affects read/write performance, as there
are dramatically fewer bytes to transfer in so many cases.

A "smallest" file can start out at a default size (SmallestInt64ListMmf.WidthBits), then the file will be automatically
upgraded when a value is added that is too large or too small to fit in that width. For example, the Dow Jones
Industrial Average (DJIA) currently fits in a UInt16(unsigned 2-byte). When it hits 65,536 the file would be upgraded to
a 3-byte size, UInt24AsInt64. The file will have to increase to 4 bytes when the DJIA hits 16,777,215.

Many times floating-precision number can be handled internally as integers. For example S&P Futures have a tick size of
0.25, so the integer can be stored as the price divided by the tick size (being careful about the conversion). This
approach provides all the inherent speed and accuracy benefits of integers compared to floating point numbers.

Interesting References
=

<https://devblogs.microsoft.com/oldnewthing/20151218-00/?p=92672>

<https://stackoverflow.com/questions/11146352/why-memory-mapped-files-are-always-mapped-at-page-boundaries>

<https://stackoverflow.com/questions/49339804/memorymappedviewaccessor-performance-workaround>

<https://stackoverflow.com/questions/7956167/how-can-i-quickly-read-bytes-from-a-memory-mapped-file-in-net>

<https://stackoverflow.com/questions/23835932/atomic-unlocked-access-to-64bit-blocks-of-memory-mapped-files-in-net>
