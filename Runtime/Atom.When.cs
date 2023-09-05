using System;
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
        /// <param name="lifetime">Reaction lifetime.</param>
        /// <param name="data">An observed predicate function.</param>
        /// <param name="effect">An effect function.</param>
        /// <param name="exceptionHandler">A function that called when an exception is thrown while computing an reaction.</param>
        /// <param name="debugName">Debug name for this reaction.</param>
        /// <returns>Created reaction.</returns>
        /// <example>
        /// 
        /// var counter = Atom.Value(1);
        /// 
        /// var reaction = Atom.When(Lifetime,
        ///     () => counter.Value == 10,
        ///     () => Debug.Log("Counter value equals 10")
        /// );
        /// 
        /// </example>
        public static Reaction When(
            Lifetime lifetime,
            Func<bool> data,
            Action effect,
            Action<Exception> exceptionHandler = null,
            string debugName = null)
        {
            var disposer = lifetime.CreateNested(out var reactionLifetime);

            return Reaction(reactionLifetime, WhenInternal, debugName: debugName);

            void WhenInternal()
            {
                Exception exception = null;
                try
                {
                    if (!data())
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
                        effect();
                    }

                    disposer.Dispose();
                }
            }
        }

        /// <summary>
        /// Creates an reaction that observes and runs the given predicate function
        /// until it returns true. Once that happens, the returned task is completed
        /// and the reaction is deactivated.<br/>
        /// <br/>
        /// The task will fail if the predicate throws an exception.
        /// </summary>
        /// <param name="lifetime">Reaction lifetime.</param>
        /// <param name="data">An observed predicate function.</param>
        /// <param name="debugName">Debug name for this reaction.</param>
        /// <returns>Task that completes when the predicate returns true or predicate function throws exception.</returns>
        /// <example>
        /// 
        /// var counter = Atom.Value(1);
        /// 
        /// await Atom.When(Lifetime, () => counter.Value == 10);
        /// Debug.Log("Counter value equals 10");
        /// 
        /// </example>
        public static Task When(
            Lifetime lifetime,
            Func<bool> data,
            string debugName = null)
        {
            var disposer = lifetime.CreateNested(out var reactionLifetime);
            var tcs = new TaskCompletionSource<object>();

            disposer.Register(() => tcs.TrySetCanceled());

            Reaction(reactionLifetime, WhenInternal, debugName: debugName);

            return tcs.Task;

            void WhenInternal()
            {
                Exception exception = null;
                try
                {
                    if (!data())
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
                        tcs.TrySetException(exception);
                    }
                    else
                    {
                        tcs.TrySetResult(null);
                    }

                    disposer.Dispose();
                }
            }
        }
    }
}