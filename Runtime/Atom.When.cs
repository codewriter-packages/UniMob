using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace UniMob
{
    public static partial class Atom
    {
        /// <summary>
        /// Creates an reaction that observes and runs the given predicate function
        /// until it returns true. Once that happens, the given effect function
        /// is executed and the reaction is deactivated.<br/>
        /// <br/>
        /// Returns a reaction, allowing you to cancel it manually.
        /// </summary>
        /// <param name="p">An observed predicate function.</param>
        /// <param name="sideEffect">An effect function.</param>
        /// <param name="exceptionHandler">A function that called when an exception is thrown while computing an reaction.</param>
        /// <param name="debugName">Debug name for this reaction.</param>
        /// <returns>Created reaction.</returns>
        /// <example>
        ///
        /// var counter = Atom.Value(1);
        ///
        /// var reaction = Atom.When(
        ///     () => counter.Value == 10,
        ///     () => Debug.Log("Counter value equals 10")
        /// );
        /// 
        /// </example>
        public static Reaction When(
            Func<bool> p,
            Action sideEffect,
            Action<Exception> exceptionHandler = null,
            string debugName = null)
        {
            Reaction watcher = null;
            watcher = new ReactionAtom(debugName, () =>
            {
                Exception exception = null;
                try
                {
                    if (!p())
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    exception = e;
                }

                if (exception != null && exceptionHandler == null)
                {
                    Debug.LogException(exception);
                    return;
                }

                using (NoWatch)
                {
                    if (exception != null)
                    {
                        exceptionHandler(exception);
                    }
                    else
                    {
                        sideEffect();
                    }

                    // ReSharper disable once AccessToModifiedClosure
                    watcher?.Deactivate();
                    watcher = null;
                }
            });

            watcher.Activate();

            return watcher;
        }

        /// <summary>
        /// Creates an reaction that observes and runs the given predicate function
        /// until it returns true. Once that happens, the returned task is completed
        /// and the reaction is deactivated.<br/>
        /// <br/>
        /// The task will fail if the predicate throws an exception.
        /// </summary>
        /// <param name="p">An observed predicate function.</param>
        /// <param name="cancellationToken">Token for reaction cancellation.</param>
        /// <param name="debugName">Debug name for this reaction.</param>
        /// <returns>Task that completes when the predicate returns true or predicate function throws exception.</returns>
        /// <example>
        ///
        /// var counter = Atom.Value(1);
        ///
        /// await Atom.When(() => counter.Value == 10);
        /// Debug.Log("Counter value equals 10");
        /// 
        /// </example>
        public static Task When(
            Func<bool> p,
            CancellationToken cancellationToken = default,
            string debugName = null)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();

            Reaction watcher = null;
            CancellationTokenRegistration? cancellationTokenRegistration = null;

            void Dispose()
            {
                taskCompletionSource.TrySetCanceled();
                // ReSharper disable once AccessToModifiedClosure
                cancellationTokenRegistration?.Dispose();
                // ReSharper disable once AccessToModifiedClosure
                watcher?.Deactivate();
                watcher = null;
            }

            watcher = new ReactionAtom(debugName, () =>
            {
                Exception exception = null;
                try
                {
                    if (!p())
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    exception = e;
                }

                using (NoWatch)
                {
                    if (exception != null)
                    {
                        taskCompletionSource.TrySetException(exception);
                    }
                    else
                    {
                        taskCompletionSource.TrySetResult(null);
                    }

                    Dispose();
                }
            });

            if (cancellationToken.IsCancellationRequested)
            {
                Dispose();
            }
            else
            {
                watcher.Activate();

                if (cancellationToken.CanBeCanceled)
                {
                    cancellationTokenRegistration = cancellationToken.Register(Dispose, true);
                }
            }

            return taskCompletionSource.Task;
        }
    }
}