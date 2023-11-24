#if UNIMOB_UNITASK_INTEGRATION && UNIMOB_EXPERIMENTAL_UNITASK

using System;
using Cysharp.Threading.Tasks;

namespace UniMob
{
    public static partial class Atom
    {
        public static partial class UniTask
        {
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
            /// <returns>UniTask that completes when the predicate returns true or predicate function throws exception.</returns>
            /// <example>
            /// 
            /// var counter = Atom.Value(1);
            /// 
            /// await Atom.UniTask.When(Lifetime, () => counter.Value == 10);
            /// Debug.Log("Counter value equals 10");
            /// 
            /// </example>
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public static Cysharp.Threading.Tasks.UniTask When(
                Lifetime lifetime,
                Func<bool> data,
                string debugName = null)
            {
                var disposer = lifetime.CreateNested(out var reactionLifetime);
                var tcs = new UniTaskCompletionSource();

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
                            tcs.TrySetResult();
                        }

                        disposer.Dispose();
                    }
                }
            }
        }
    }
}
#endif