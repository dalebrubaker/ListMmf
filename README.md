ListMmf
=======

List\<T\> using Memory Mapped Files (MMFs).

Support IList64\<T\>, similar to IList\<T\> but using Int64 values where
appropriate.

View
----

There is only one view of the entire file (which could be a persisted File or
could be a non-persisted Memory-based MMF).

Header
------

The first headerReserveBytes (if any) are reserved by use by the creator of
ListMmf. ListMmf requires this to be evenly divisible by 8 (in case alignment
matters).

The next 8 bytes are a long value of the array Count.

The next 8 bytes are a long value of the array Size. This cannot be calculated
from the MMF because the MMF allows writing/reading to/from a rounded-up view
past the end of the underlying file.

The next 8 bytes are a long value of the array Version, a counter incremented on
writes that is used for throwing an “Enumeration modified” error during
enumeration.

So the actual list values are an array starting at headerReserveBytes + 24 into
the file.
