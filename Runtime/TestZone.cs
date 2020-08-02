using System;
using System.Threading;

namespace UniMob
{
    public class TestZone : IZone, IDisposable
    {
        private readonly Action<Exception> _exceptionHandler;
        private readonly TimerDispatcher _dispatcher;

        public TestZone(Action<Exception> exceptionHandler)
        {
            _exceptionHandler = exceptionHandler;
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _dispatcher = new TimerDispatcher(mainThreadId, exceptionHandler);

            Zone.Current = this;
        }

        public void Dispose()
        {
            Zone.Current = null;

            _dispatcher.Dispose();
        }

        public Ticker Tick => _dispatcher.Tick;

        public static void Run(Action<Ticker> scope)
            => Run(ex => throw ex, scope);

        public static void Run(Action<Exception> exceptionHandler, Action<Ticker> scope)
        {
            using (var zone = new TestZone(exceptionHandler))
            {
                scope(zone.Tick);
            }
        }

        public delegate void Ticker(float time);

        public void HandleUncaughtException(Exception exception)
        {
            _exceptionHandler(exception);
        }

        public void Invoke(Action action)
        {
            _dispatcher.Invoke(action);
        }

        public void InvokeDelayed(float delay, Action action)
        {
            _dispatcher.InvokeDelayed(delay, action);
        }

        public void AddTicker(Action action)
        {
            _dispatcher.AddTicker(action);
        }

        public void RemoveTicker(Action action)
        {
            _dispatcher.RemoveTicker(action);
        }
    }
}