using System;
using System.Collections.Generic;

namespace UniMob
{
    public static class Atom
    {
        public static MutableAtom<T> Value<T>(
            T value,
            IAtomCallbacks callbacks = null,
            IEqualityComparer<T> comparer = null)
        {
            return new ValueAtom<T>(null, value, callbacks, comparer);
        }

        public static Atom<T> Computed<T>(
            AtomPull<T> pull,
            bool keepAlive = false,
            bool requiresReaction = false,
            IAtomCallbacks callbacks = null,
            IEqualityComparer<T> comparer = null)
        {
            return new ComputedAtom<T>(null, pull, null, keepAlive, requiresReaction, callbacks, comparer);
        }

        public static MutableAtom<T> Computed<T>(
            AtomPull<T> pull,
            AtomPush<T> push,
            bool keepAlive = false,
            bool requiresReaction = false,
            IAtomCallbacks callbacks = null,
            IEqualityComparer<T> comparer = null)
        {
            return new ComputedAtom<T>(null, pull, push, keepAlive, requiresReaction, callbacks, comparer);
        }

        public static IDisposable Reaction<T>(
            AtomPull<T> pull,
            Action<T, IDisposable> reaction,
            IEqualityComparer<T> comparer = null,
            bool fireImmediately = false,
            Action<Exception> exceptionHandler = null)
        {
            var valueAtom = Computed(pull, comparer: comparer);
            bool firstRun = true;

            ReactionAtom atom = null;
            atom = new ReactionAtom(null, () =>
            {
                var value = valueAtom.Value;

                using (NoWatch)
                {
                    if (firstRun)
                    {
                        firstRun = false;

                        if (!fireImmediately)
                            return;
                    }

                    // ReSharper disable once AccessToModifiedClosure
                    reaction(value, atom);
                }
            }, exceptionHandler);

            atom.Get();
            return atom;
        }

        public static IDisposable Reaction<T>(
            AtomPull<T> pull,
            Action<T> reaction,
            IEqualityComparer<T> comparer = null,
            bool fireImmediately = false,
            Action<Exception> exceptionHandler = null)
        {
            return Reaction(pull, (value, _) => reaction(value), comparer, fireImmediately, exceptionHandler);
        }

        public static IDisposable AutoRun(Action reaction, Action<Exception> exceptionHandler = null)
        {
            var atom = new ReactionAtom(null, reaction, exceptionHandler);
            atom.Get();
            return atom;
        }

        private static readonly WatchScope NoWatchInstance = new WatchScope();

        public static IDisposable NoWatch
        {
            get
            {
                NoWatchInstance.Enter();
                return NoWatchInstance;
            }
        }

        public static AtomBase CurrentScope => AtomBase.Stack.Peek();

        private class WatchScope : IDisposable
        {
            public void Enter()
            {
                AtomBase.Stack.Push(null);
            }

            public void Dispose()
            {
                AtomBase.Stack.Pop();
            }
        }

        /// <summary>
        /// Invokes callback when condition becomes True.
        /// If condition throw exception, invoke exceptionHandler.
        /// </summary>
        /// <param name="cond">Watched condition</param>
        /// <param name="callback">Value handler</param>
        /// <param name="exceptionHandler">Exception handler</param>
        /// <returns>Disposable for cancel watcher</returns>
        public static IDisposable When(Func<bool> cond, Action callback, Action<Exception> exceptionHandler = null)
        {
            IDisposable watcher = null;
            return watcher = AutoRun(() =>
            {
                Exception exception = null;
                try
                {
                    if (!cond()) return;
                }
                catch (Exception e)
                {
                    exception = e;
                }

                if (exception != null && exceptionHandler == null)
                {
                    return;
                }

                using (NoWatch)
                {
                    if (exception != null) exceptionHandler(exception);
                    else callback();

                    // ReSharper disable once AccessToModifiedClosure
                    watcher?.Dispose();
                    watcher = null;
                }
            });
        }
    }
}