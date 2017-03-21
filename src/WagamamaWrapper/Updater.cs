using System;
using System.Threading;

namespace WagamamaWrapper
{
    public class Updater
    {
        private readonly Lazy<Timer> _timer;
        private readonly long _intervalMilliseconds;
        private readonly LockObject _lockObject;
        private Action _endHandler;

        public Updater(
            long intervalMilliseconds,
            Action updateHandler,
            Action endHandler,
            Action<Exception> exceptionHandler = null)
        {
            _timer                = new Lazy<Timer>(() =>
            {
                var timer = new Timer(
                    obj =>
                    {
                        var lockObject = (LockObject)obj;

                        if (!lockObject.IsLocked)
                        {
                            lockObject.IsLocked = true;

                            try
                            {
                                updateHandler.Invoke();
                            }
                            catch (Exception ex)
                            {
                                exceptionHandler?.Invoke(ex);
                            }
                        }

                        lockObject.IsLocked = false;
                    },
                    _lockObject,
                    Timeout.Infinite,
                    0);

                return timer;
            });
            _intervalMilliseconds = intervalMilliseconds;
            _lockObject           = new LockObject();
            _endHandler           = endHandler;
        }

        public void Begin()
        {
            _timer.Value.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(_intervalMilliseconds));
        }

        public void End()
        {
            if (_timer.IsValueCreated)
            {
                _timer.Value.Dispose();
            }

            _endHandler?.Invoke();
        }

        private class LockObject
        {
            public bool IsLocked { get; set; }
        }
    }
}