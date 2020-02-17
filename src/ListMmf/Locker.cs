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
        private readonly CancellationToken _cancellationToken;
        private readonly object _lock;
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
            _lock = lockObject;
            _actionEnter = () => Monitor.Enter(_lock);
            _actionExit = () => Monitor.Exit(_lock);
        }

        /// <summary>
        /// Use this ctor for a locker that uses a Semaphore to lock on a system-wide semaphore name.
        /// For example, this can be a Path or MapName to lock MemoryMappedFiles system-wide.
        /// </summary>
        /// <param name="systemWideSymaphoreName"><c>null</c> or empty to make this local and not system-wide. Maximum length is 260 characters.</param>
        /// <param name="cancellationToken"></param>
        /// <param name="timeout"></param>
        public Locker(string systemWideSymaphoreName, CancellationToken cancellationToken = default, int timeout = -1)
        {
            _cancellationToken = cancellationToken;

            // TODO
            //_actionEnter = () => Monitor.Enter(_lock);
            //_actionExit = () => Monitor.Exit(_lock);
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
        }
    }
}
