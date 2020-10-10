using System;
using System.Threading.Tasks;

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
        public static Reaction When(
            Func<bool> p,
            Action sideEffect,
            Action<Exception> exceptionHandler = null,
            string debugName = null)
        {
            Reaction watcher = null;
            return watcher = Reaction(() =>
            {
                Exception exception = null;
                try
                {
                    if (!p()) return;
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
                    else sideEffect();

                    // ReSharper disable once AccessToModifiedClosure
                    watcher?.Deactivate();
                    watcher = null;
                }
            }, debugName: debugName);
        }

        /// <summary>
        /// Creates an reaction that observes and runs the given predicate function
        /// until it returns true. Once that happens, the returned task is completed
        /// and the reaction is deactivated.<br/>
        /// <br/>
        /// The task will fail if the predicate throws an exception.
        /// </summary>
        /// <param name="p">An observed predicate function.</param>
        /// <param name="debugName">Debug name for this reaction.</param>
        /// <returns>Task that completes when the predicate returns true or predicate function throws exception.</returns>
        public static Task When(Func<bool> p, string debugName = null)
        {
            var tcs = new TaskCompletionSource<object>();

            Atom.When(p,
                () => tcs.TrySetResult(null),
                exception => tcs.TrySetException(exception),
                debugName
            );

            return tcs.Task;
        }
    }
}