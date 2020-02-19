using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BruSoftware.ListMmf;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace ListMmfTests
{
    public class LockerTests
    {
        private readonly ITestOutputHelper _output;

        public LockerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RegularLock_Locks()
        {
            var lockObject = new object();
            var valuesWithLock = new List<DateTime>(10);

            void AddValueWithLock()
            {
                lock (lockObject)
                {
                    Thread.Sleep(10);
                    valuesWithLock.Add(DateTime.UtcNow);
                }
            }

            var tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(AddValueWithLock);
            }
            Task.WaitAll(tasks);
            var elapseWithLock = (valuesWithLock[valuesWithLock.Count - 1] - valuesWithLock[0]).TotalMilliseconds;

            var valuesNoLock = new List<DateTime>(10);

            void AddValueNoLock()
            {
                Thread.Sleep(10);
                valuesNoLock.Add(DateTime.UtcNow);
            }

            tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(AddValueNoLock);
            }
            Task.WaitAll(tasks);
            var elapseNoLock = (valuesNoLock[valuesNoLock.Count - 1] - valuesNoLock[0]).TotalMilliseconds;
            elapseWithLock.Should().BeGreaterThan(90, "With locks they happen serially");
            elapseNoLock.Should().BeLessThan(10, "Without locks they are nearly simultaneous.");
        }

        [Fact]
        public void Monitor_Locks()
        {
            var lockObject = new object();
            var lockerNoLocks = new Locker();
            var lockerWithLocks = new Locker(lockObject);
            var valuesWithLock = new List<DateTime>(10);

            void AddValueWithLock()
            {
                using (lockerWithLocks.Lock())
                {
                    Thread.Sleep(10);
                    valuesWithLock.Add(DateTime.UtcNow);
                }
            }

            var tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(AddValueWithLock);
            }
            Task.WaitAll(tasks);
            var elapseWithLock = (valuesWithLock[valuesWithLock.Count - 1] - valuesWithLock[0]).TotalMilliseconds;

            var valuesNoLock = new List<DateTime>(10);

            void AddValueNoLock()
            {
                using (lockerNoLocks.Lock())
                {
                    Thread.Sleep(10);
                    valuesNoLock.Add(DateTime.UtcNow);
                }
            }

            tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(AddValueNoLock);
            }
            Task.WaitAll(tasks);
            var elapseNoLock = (valuesNoLock[valuesNoLock.Count - 1] - valuesNoLock[0]).TotalMilliseconds;
            elapseWithLock.Should().BeGreaterThan(90, "With locks they happen serially");
            elapseNoLock.Should().BeLessThan(10, "Without locks they are nearly simultaneous.");
        }

        [Fact]
        public void Mutex_Locks()
        {
            var mutex = new Mutex(false, $"{nameof(Mutex_Locks)}");
            var lockerNoLocks = new Locker();
            var lockerWithLocks = new Locker(mutex);
            var valuesWithLock = new List<DateTime>(10);

            void AddValueWithLock()
            {
                using (lockerWithLocks.Lock())
                {
                    Thread.Sleep(10);
                    valuesWithLock.Add(DateTime.UtcNow);
                }
            }

            var tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(AddValueWithLock);
            }
            Task.WaitAll(tasks);
            var elapseWithLock = (valuesWithLock[valuesWithLock.Count - 1] - valuesWithLock[0]).TotalMilliseconds;

            var valuesNoLock = new List<DateTime>(10);

            void AddValueNoLock()
            {
                using (lockerNoLocks.Lock())
                {
                    Thread.Sleep(10);
                    valuesNoLock.Add(DateTime.UtcNow);
                }
            }

            tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(AddValueNoLock);
            }
            Task.WaitAll(tasks);
            var elapseNoLock = (valuesNoLock[valuesNoLock.Count - 1] - valuesNoLock[0]).TotalMilliseconds;
            elapseWithLock.Should().BeGreaterThan(90, "With locks they happen serially");
            elapseNoLock.Should().BeLessThan(10, "Without locks they are nearly simultaneous.");
        }

        [Fact]
        public void Semaphore_Locks()
        {
            var semaphore = new Semaphore(1, 1, $"{nameof(Semaphore_Locks)}");
            var lockerNoLocks = new Locker();
            var lockerWithLocks = new Locker(semaphore);
            var valuesWithLock = new List<DateTime>(10);

            void AddValueWithLock()
            {
                using (lockerWithLocks.Lock())
                {
                    Thread.Sleep(10);
                    valuesWithLock.Add(DateTime.UtcNow);
                }
            }

            var tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(AddValueWithLock);
            }
            Task.WaitAll(tasks);
            var elapseWithLock = (valuesWithLock[valuesWithLock.Count - 1] - valuesWithLock[0]).TotalMilliseconds;

            var valuesNoLock = new List<DateTime>(10);

            void AddValueNoLock()
            {
                using (lockerNoLocks.Lock())
                {
                    Thread.Sleep(10);
                    valuesNoLock.Add(DateTime.UtcNow);
                }
            }

            tasks = new Task[10];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(AddValueNoLock);
            }
            Task.WaitAll(tasks);
            var elapseNoLock = (valuesNoLock[valuesNoLock.Count - 1] - valuesNoLock[0]).TotalMilliseconds;
            elapseWithLock.Should().BeGreaterThan(90, "With locks they happen serially");
            elapseNoLock.Should().BeLessThan(10, "Without locks they are nearly simultaneous.");
        }

  
  
        [Fact]
        public async Task Monitor_LockInLockCheck()
        {
            var lockObject = new object();
            using (var cts = new CancellationTokenSource())
            {
                var lockerWithLocks = new Locker(lockObject);
                var isBlocking = false;
                var isCancelled = false;
                var isTimedOut = false;

                void DoLock1()
                {
                    isBlocking = true;
                    try
                    {
                        using (lockerWithLocks.Lock())
                        {
                            isBlocking = false;
                            DoLock2();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        isCancelled = true;
                    }
                    catch (TimeoutException)
                    {
                        isTimedOut = true;
                    }
                }

                void DoLock2()
                {
                    isBlocking = true;
                    try
                    {
                        using (lockerWithLocks.Lock())
                        {
                            isBlocking = false;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        isCancelled = true;
                    }
                    catch (TimeoutException)
                    {
                        isTimedOut = true;
                    }
                }

                Task.Run(DoLock1);
                await Task.Delay(10);
                isBlocking.Should().BeFalse("Locker.Lock() made it through both locks because it is the same thread.");
            }
        }

        [Fact]
        public async Task Mutex_LockInLockCheck()
        {
            var mutex = new Mutex(false, nameof(Mutex_LockInLockCheck));
            using (var cts = new CancellationTokenSource())
            {
                var lockerWithLocks = new Locker(mutex);
                var isBlocking = false;
                var isCancelled = false;
                var isTimedOut = false;

                void DoLock1()
                {
                    isBlocking = true;
                    try
                    {
                        using (lockerWithLocks.Lock())
                        {
                            isBlocking = false;
                            DoLock2();
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        isCancelled = true;
                    }
                    catch (TimeoutException)
                    {
                        isTimedOut = true;
                    }
                }

                void DoLock2()
                {
                    isBlocking = true;
                    try
                    {
                        using (lockerWithLocks.Lock())
                        {
                            isBlocking = false;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        isCancelled = true;
                    }
                    catch (TimeoutException)
                    {
                        isTimedOut = true;
                    }
                }

                Task.Run(DoLock1);
                await Task.Delay(10);
                isBlocking.Should().BeFalse("Locker.Lock() made it through both locks because it is the same thread.");
            }
        }
    }
}
