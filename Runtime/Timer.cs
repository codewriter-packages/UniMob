using System;
using System.Runtime.CompilerServices;

namespace UniMob
{
    /// <summary>
    /// A timer that can be configured to fire once or repeatedly.
    /// </summary>
    public class Timer : IDisposable
    {
        private readonly float _delay;
        private readonly Action _callback;
        private readonly Action _invoke;
        private readonly bool _periodic;
        private bool _disposed;

        public bool IsActive => !_disposed;
        
        private Timer(float delay, bool periodic, Action callback)
        {
            if (delay < 0f)
                throw new ArgumentOutOfRangeException(nameof(delay));

            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _delay = delay;
            _invoke = Invoke;
            _periodic = periodic;

            Zone.Current.InvokeDelayed(_delay, _invoke);
        }

        /// <summary>
        /// Cancel the Timer.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;
        }

        private void Invoke()
        {
            if (_disposed) return;
            
            _callback();

            if (_periodic)
            {
                Zone.Current.InvokeDelayed(_delay, _invoke);
            }
        }

        /// <summary>
        /// Runs the given callback asynchronously as soon as possible.
        /// </summary>
        public static void Run(Action callback)
        {
            Zone.Current.Invoke(callback);
        }
        
        /// <summary>
        /// Creates a new timer.
        /// </summary>
        public static void Run(float delay, Action callback)
        {
            Zone.Current.InvokeDelayed(delay, callback);
        }
        
        /// <summary>
        /// Creates a new timer.
        /// </summary>
        public static Timer Delayed(float delay, Action action)
        {
            return new Timer(delay, false, action);
        }

        /// <summary>
        /// Creates a new repeating timer.
        /// The callback is invoked repeatedly with duration intervals until canceled with the Dispose method.
        /// </summary>
        public static Timer RunPeriodic(float delay, Action callback)
        {
            return new Timer(delay, true, callback);
        }
    }
}