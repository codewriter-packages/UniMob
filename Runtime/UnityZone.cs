using System;
using System.Threading;
using UnityEngine;

namespace UniMob
{
    public class UnityZone : MonoBehaviour, IZone
    {
        private static TimerDispatcher _dispatcher;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        internal static void Init()
        {
            if (_dispatcher != null) return;

            var go = new GameObject(nameof(UnityZone));
            var zone = go.AddComponent<UnityZone>();
            DontDestroyOnLoad(go);
            DontDestroyOnLoad(zone);
        }

        private void Awake()
        {
            if (_dispatcher != null)
            {
                Destroy(gameObject);
                return;
            }

            var unityThreadId = Thread.CurrentThread.ManagedThreadId;
            _dispatcher = new TimerDispatcher(unityThreadId, HandleUncaughtException);

            Zone.Current = this;
        }

        private void Update()
        {
            _dispatcher.Tick(Time.unscaledTime);
        }

        public void HandleUncaughtException(Exception exception)
        {
            Debug.LogException(exception);
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