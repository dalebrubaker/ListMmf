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

        /// <summary>
        /// Use this ctor for a locker that uses a Semaphore to lock on a system-wide semaphore name.
        /// For example, this can be a Path or MapName to lock MemoryMappedFiles system-wide.
        /// Instantiate it with initial count 1 and maximum count 1
        /// </summary>
        /// <param name="semaphore"><c>null</c> or empty to make this local and not system-wide. Maximum length is 260 characters.</param>
        public Locker(Semaphore semaphore)
        {

            _actionEnter = () => semaphore.WaitOne();
            _actionExit = () => semaphore?.Release(1);
        }

        /// <summary>
        /// Use this ctor for a locker that uses a Semaphore to lock on a system-wide semaphore name.
        /// For example, this can be a Path or MapName to lock MemoryMappedFiles system-wide.
        /// Instantiate it with initial count 1 and maximum count 1
        /// </summary>
        /// <param name="semaphore"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="timeout"></param>
        public Locker(Semaphore semaphore, CancellationToken cancellationToken = default, int timeout = -1)
        {

            _actionEnter = () => BlockUntilAvailableCancelledOrTimeout(cancellationToken, semaphore, timeout);
            _actionExit = () => semaphore?.Release(1);
        }

        /// <summary>
        /// BlockUntilAvailableCancelledOrTimeout on semaphoreUnique until it is signaled (another user of this semaphore disposed/released), timed out or cancelled
        /// Thanks to https://docs.microsoft.com/en-us/dotnet/standard/threading/how-to-listen-for-cancellation-requests-that-have-wait-handles
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <param name="semaphoreUnique"></param>
        /// <param name="timeout">-1 means infinite</param>
        /// <exception cref="OperationCanceledException">if cancelled</exception>
        /// <exception cref="TimeoutException">if timeout</exception>
        public static void BlockUntilAvailableCancelledOrTimeout(CancellationToken cancellationToken, Semaphore semaphoreUnique, int timeout = -1)
        {
            int eventThatSignaledIndex = WaitHandle.WaitAny(new[]
            {
                semaphoreUnique,
                cancellationToken.WaitHandle
            }, timeout);
            if (eventThatSignaledIndex == 1)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (cancellationToken.IsCancellationRequested)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
            if (eventThatSignaledIndex == WaitHandle.WaitTimeout)
            {
                throw new TimeoutException();
            }
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
