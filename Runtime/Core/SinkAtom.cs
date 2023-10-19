using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SinkAtom<T> : AtomBase, Atom<T>, AtomSink<T>
    {
        internal Action<AtomSink<T>> subscribe;
        internal Action unsubscribe;

        internal T value;
        internal ExceptionDispatchInfo exception;

        internal SinkAtom(
            string debugName,
            T initialValue,
            Action<AtomSink<T>> subscribe,
            Action unsubscribe
        )
        {
            this.subscribe = subscribe;
            this.unsubscribe = unsubscribe;
            this.debugName = debugName;

            options.Set(AtomOptions.HasCache);
            value = initialValue;
            exception = null;
        }

        public T Value
        {
            get
            {
                Actualize();
                SubscribeToParent();

                if (exception != null)
                {
                    exception.Throw();
                }

                return value;
            }
        }

        public override void Deactivate()
        {
            if (options.Has(AtomOptions.Active))
            {
                Unsubscribe();
            }

            base.Deactivate();
        }

        protected override void Evaluate(bool activating)
        {
            state = AtomState.Pulling;

            if (activating)
            {
                Subscribe();
            }

            state = AtomState.Actual;
        }

        private void Subscribe()
        {
            using (Atom.NoWatch)
            {
                try
                {
                    subscribe?.Invoke(this);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        private void Unsubscribe()
        {
            using (Atom.NoWatch)
            {
                try
                {
                    unsubscribe?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public void SetValue(T newValue)
        {
            if (options.Has(AtomOptions.HasCache) && EqualityComparer<T>.Default.Equals(newValue, value))
            {
                return;
            }

            options.Set(AtomOptions.HasCache);
            value = newValue;
            exception = null;

            state = AtomState.Obsolete;

            ObsoleteSubscribers();
        }

        public void SetException(Exception newException)
        {
            options.Reset(AtomOptions.HasCache);
            value = default;
            exception = ExceptionDispatchInfo.Capture(newException);

            state = AtomState.Obsolete;

            ObsoleteSubscribers();
        }
    }
}