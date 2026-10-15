using System;
using System.Threading;

namespace BruSoftware.ListMmf;

/// <summary>
/// Tracks which ListMmf (if any) each thread is reading through right now, so a remap or Dispose
/// can wait until no thread can still hold a pointer into the view it is about to unmap.
///
/// Readers: <c>slot.Owner = list</c> (plain store), read the published <see cref="MmfMapping"/>, dereference it,
/// then <c>Volatile.Write(ref slot.Owner, null)</c>. Nothing shared is written, so reads cost a few nanoseconds
/// and don't contend across threads.
///
/// Writer (remap or Dispose): publish the replacement mapping, then call <see cref="WaitForReaders"/>.
/// <see cref="Interlocked.MemoryBarrierProcessWide"/> makes every other thread's earlier stores visible, so for each
/// reader either the writer sees its Owner store and waits for it to clear, or the reader's load of the mapping
/// happens after the publish and gets the replacement. Either way, once WaitForReaders returns, no thread can be
/// reading through the old view.
///
/// Read sections must be leaf code: no calls into other lists, no blocking, no waiting on the writer.
/// </summary>
internal static class MmfReaders
{
    [ThreadStatic]
    private static Slot? t_slot;

    private static readonly object s_registerLock = new();

    /// <summary>
    /// Copy-on-write so WaitForReaders can scan without taking s_registerLock
    /// </summary>
    private static Slot[] s_slots = [];

    /// <summary>
    /// The calling thread's slot
    /// </summary>
    public static Slot Current => t_slot ?? Register();

    private static Slot Register()
    {
        var slot = new Slot(Thread.CurrentThread);
        lock (s_registerLock)
        {
            var old = s_slots;
            var live = new Slot[old.Length + 1];
            var n = 0;
            foreach (var existing in old)
            {
                // A thread can't exit inside a read section, so a dead thread's slot is always clear
                if (existing.IsThreadAlive)
                {
                    live[n++] = existing;
                }
            }
            live[n++] = slot;
            Array.Resize(ref live, n);
            Volatile.Write(ref s_slots, live);
        }
        t_slot = slot;
        return slot;
    }

    /// <summary>
    /// Returns once no thread is inside a read section on owner that could have loaded a mapping
    /// published before this call.
    /// </summary>
    public static void WaitForReaders(object owner)
    {
        Interlocked.MemoryBarrierProcessWide();
        var slots = Volatile.Read(ref s_slots);
        foreach (var slot in slots)
        {
            var spinner = new SpinWait();
            while (ReferenceEquals(Volatile.Read(ref slot.Owner), owner))
            {
                spinner.SpinOnce();
            }
        }
    }

    internal sealed class Slot
    {
        private readonly WeakReference<Thread> _thread;

        /// <summary>
        /// The list this thread is reading through right now, or null
        /// </summary>
        public object? Owner;

        public Slot(Thread thread)
        {
            _thread = new WeakReference<Thread>(thread);
        }

        public bool IsThreadAlive => _thread.TryGetTarget(out var thread) && thread.IsAlive;
    }
}
