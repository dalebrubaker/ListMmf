using System;
using System.Threading;

namespace BruSoftware.ListMmf
{
    /// <summary>
    /// This class allows multiple locking actions with guaranteed unlocking when used in a using block.
    /// E.g.
    ///     var locker = new locker(_lock);
    ///     using (locker.Lock())
    ///     {
    ///     } // unlock happens here
    /// Note that the scope braces are not required in C# 8
    /// </summary>
    public class Locker : IDisposable
    {
        private readonly Action _actionEnter;
        private readonly Action _actionExit;

        public Locker(Action actionEnter, Action actionExit)
        {
            _actionEnter = actionEnter;
            _actionExit = actionExit;
        }

        /// <summary>
        /// Use this ctor for a locker that doesn't lock
        /// </summary>
        public Locker()
        {
            _actionEnter = null;
            _actionExit = null;
        }

        /// <summary>
        /// Use this ctor for a locker that locks on lockObject (Monitor.Enter/Exit)
        /// </summary>
        /// <param name="lockObject"></param>
        public Locker(object lockObject)
        {
            _actionEnter = () => Monitor.Enter(lockObject);
            _actionExit = () => Monitor.Exit(lockObject);
        }

        /// <summary>
        /// Use this for a locker that uses a Mutex to lock on a system-wide semaphore name.
        /// For example, this can be a Path or MapName to lock MemoryMappedFiles system-wide.
        /// Instantiate it with false (not owned)
        /// </summary>
        /// <param name="mutex"></param>
        public Locker(Mutex mutex)
        {
            _actionEnter = () => mutex.WaitOne();
            _actionExit = () => mutex.ReleaseMutex();
        }

        public Locker Lock()
        {
            _actionEnter?.Invoke();
            return this;
        }

        public void Dispose()
        {
            // release lock
            _actionExit?.Invoke();
            GC.SuppressFinalize(this);
        }
    }
}
