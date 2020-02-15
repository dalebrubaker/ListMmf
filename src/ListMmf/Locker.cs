using System;

namespace BruSoftware.ListMmf
{
    public class Locker : IDisposable
    {
        private readonly Action _actionEnter;
        private readonly Action _actionExit;

        public Locker(Action actionEnter, Action actionExit)
        {
            _actionEnter = actionEnter;
            _actionExit = actionExit;
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