using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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
            var lockerNoLocks= new Locker();
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

       
    }
}
