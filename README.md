ListMmf
=======

List\<T\> using Memory Mapped Files (MMFs).

Support IList64\<T\>, similar to IList\<T\> but using Int64 values where
appropriate.

This class can only be run in a 64-bit process.

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

Locking
=======

Locking can be slow, and it is can be turned of with the noLocking constructor
parameter. This is useful when your design ensures that no writing and reading
can be done simultaneously in the same part of a file. For example, only one
writer is allowed on your system, it only does appends, and it sets the Length
field after it writes. This means a read can never be reading the same location
that is being written.

Unless you set noLocking, the following happens:

-   Sizeof(T) \> 8 – named semaphore with count of 1 (not Mutex to avoid thread
    affinity)

-   Sizeof(T) \<= 8

    -   IsReadOnly – no locking

    -   Not IsReadOnly – lock (Monitor) to avoid in-process attempts to read
        while Add() is extending (closing and reopening) the MMF

Interesting References
======================

<https://devblogs.microsoft.com/oldnewthing/20151218-00/?p=92672>

<https://stackoverflow.com/questions/11146352/why-memory-mapped-files-are-always-mapped-at-page-boundaries>

<https://stackoverflow.com/questions/49339804/memorymappedviewaccessor-performance-workaround>

<https://stackoverflow.com/questions/7956167/how-can-i-quickly-read-bytes-from-a-memory-mapped-file-in-net>

<https://stackoverflow.com/questions/23835932/atomic-unlocked-access-to-64bit-blocks-of-memory-mapped-files-in-net>
