using System;

namespace UniMob
{
    public abstract class Zone
    {
        public static IZone Current { get; set; }
    }

    public interface IZone
    {
        void HandleUncaughtException(Exception exception);
        void Invoke(Action action);
        void InvokeDelayed(float delay, Action action);
        
        void AddTicker(Action action);
        void RemoveTicker(Action action);
    }
}