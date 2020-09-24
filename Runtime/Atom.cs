using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UniMob
{
    public static class Atom
    {
        public static MutableAtom<T> Value<T>(
            T value,
            IAtomCallbacks callbacks = null,
            IEqualityComparer<T> comparer = null,
            string debugName = null)
        {
            return new ValueAtom<T>(debugName, value, callbacks, comparer);
        }

        public static Atom<T> Computed<T>(
            AtomPull<T> pull,
            bool keepAlive = false,
            bool requiresReaction = false,
            IAtomCallbacks callbacks = null,
            IEqualityComparer<T> comparer = null,
            string debugName = null)
        {
            return new ComputedAtom<T>(debugName, pull, null, keepAlive, requiresReaction, callbacks, comparer);
        }

        public static MutableAtom<T> Computed<T>(
            AtomPull<T> pull,
            AtomPush<T> push,
            bool keepAlive = false,
            bool requiresReaction = false,
            IAtomCallbacks callbacks = null,
            IEqualityComparer<T> comparer = null,
            string debugName = null)
        {
            return new ComputedAtom<T>(debugName, pull, push, keepAlive, requiresReaction, callbacks, comparer);
        }

        public static Reaction Reaction<T>(
            AtomPull<T> pull,
            Action<T, Reaction> reaction,
            IEqualityComparer<T> comparer = null,
            bool fireImmediately = true,
            Action<Exception> exceptionHandler = null,
            string debugName = null)
        {
            var valueAtom = Computed(pull, comparer: comparer);
            bool firstRun = true;

            Reaction atom = null;
            atom = new ReactionAtom(debugName, () =>
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

            atom.Activate();
            return atom;
        }

        public static Reaction Reaction<T>(
            AtomPull<T> pull,
            Action<T> reaction,
            IEqualityComparer<T> comparer = null,
            bool fireImmediately = true,
            Action<Exception> exceptionHandler = null,
            string debugName = null)
        {
            return Reaction(pull, (value, _) => reaction(value), comparer, fireImmediately,
                exceptionHandler, debugName);
        }

        public static Reaction Reaction(
            Action reaction,
            Action<Exception> exceptionHandler = null,
            string debugName = null)
        {
            var atom = new ReactionAtom(debugName, reaction, exceptionHandler);
            atom.Activate();
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
        /// <param name="debugName">Atom debug name</param>
        /// <param name="cond">Watched condition</param>
        /// <param name="callback">Value handler</param>
        /// <param name="exceptionHandler">Exception handler</param>
        /// <returns>Disposable for cancel watcher</returns>
        public static Reaction When(
            Func<bool> cond, Action callback,
            Action<Exception> exceptionHandler = null,
            string debugName = null)
        {
            Reaction watcher = null;
            return watcher = Reaction(() =>
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
                    watcher?.Deactivate();
                    watcher = null;
                }
            }, debugName: debugName);
        }

        public static Task When(Func<bool> cond, string debugName = null)
        {
            var tcs = new TaskCompletionSource<object>();

            Atom.When(cond,
                () => tcs.TrySetResult(null),
                exception => tcs.TrySetException(exception),
                debugName
            );

            return tcs.Task;
        }
    }
}