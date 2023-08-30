using System;

namespace UniMob.Core
{
    public class AsyncSinkAtom<T> : SinkAtom<AtomAsyncValue<T>>, AsyncAtom<T>
    {
        internal readonly Action<bool> reload;

        internal AsyncSinkAtom(
            string debugName,
            AtomAsyncValue<T> initialValue,
            Action<AtomSink<AtomAsyncValue<T>>> subscribe,
            Action unsubscribe = null,
            Action<bool> reload = null)
            : base(debugName, initialValue, subscribe, unsubscribe)
        {
            this.reload = reload;
        }

        public void Reload(bool clearValue)
        {
            reload?.Invoke(clearValue);
        }
    }
}