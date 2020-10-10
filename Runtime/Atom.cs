using System;
using System.Collections.Generic;

namespace UniMob
{
    public static partial class Atom
    {
        public static AtomBase CurrentScope => AtomBase.Stack.Peek();

        /// <summary>
        /// Creates a scope which blocks changes propagation. <br/>
        /// <br/>
        /// Must only be used in using statement.
        /// </summary>
        /// <example>
        /// 
        /// using(Atom.NoWatch)
        /// {
        ///     // ...
        /// }
        /// 
        /// </example>
        public static IDisposable NoWatch => WatchScope.Enter();

        /// <summary>
        /// Creates an atom that store the value.
        /// </summary>
        /// <param name="value">Initial value.</param>
        /// <param name="callbacks">Atom lifetime callbacks.</param>
        /// <param name="comparer">Value comparer used for reconciling.</param>
        /// <param name="debugName">Debug name for this atom.</param>
        /// <typeparam name="T">Atom value type.</typeparam>
        /// <returns>Created atom.</returns>
        /// <example>
        ///
        /// var counter = Atom.Value();
        /// counter.Value += 1;
        ///
        /// Debug.Log(counter.Value);
        /// 
        /// </example>
        public static MutableAtom<T> Value<T>(
            T value,
            IAtomCallbacks callbacks = null,
            IEqualityComparer<T> comparer = null,
            string debugName = null)
        {
            return new ValueAtom<T>(debugName, value, callbacks, comparer);
        }

        /// <summary>
        /// Creates an atom that compute its value by a function.<br/>
        /// </summary>
        /// <remarks>
        /// Computed values can be used to derive information from other atoms.
        /// They evaluate lazily, caching their output and only recomputing
        /// if one of the underlying atoms has changed. If they are not observed
        /// by anything, they suspend entirely.<br/>
        /// <br/>
        /// Conceptually, they are very similar to formulas in spreadsheets,
        /// and can't be underestimated. They help in reducing the amount of state
        /// you have to store and are highly optimized. Use them wherever possible.
        /// </remarks>
        /// <param name="pull">Function for pulling value.</param>
        /// <param name="push">Function for pushing new value.</param>
        /// <param name="keepAlive">Should an atom keep its value cached when there are no subscribers?</param>
        /// <param name="requiresReaction">Should an atom print warnings when its values are tried to be read outside of the reaction?</param>
        /// <param name="callbacks">Atom lifetime callbacks.</param>
        /// <param name="comparer">Value comparer used for reconciling.</param>
        /// <param name="debugName">Debug name for this atom.</param>
        /// <typeparam name="T">Atom value type.</typeparam>
        /// <returns>Created atom.</returns>
        /// <example>
        ///
        /// var a = Atom.Value(1);
        /// var b = Atom.Value(2);
        ///
        /// var sum = Atom.Computed(() => a.Value + b.Value);
        ///
        /// Debug.Log(sum.Value);
        /// 
        /// </example>
        public static Atom<T> Computed<T>(
            AtomPull<T> pull,
            AtomPush<T> push = null,
            bool keepAlive = false,
            bool requiresReaction = false,
            IAtomCallbacks callbacks = null,
            IEqualityComparer<T> comparer = null,
            string debugName = null)
        {
            return new ComputedAtom<T>(debugName, pull, push, keepAlive, requiresReaction, callbacks, comparer);
        }

        private class WatchScope : IDisposable
        {
            private static readonly WatchScope Instance = new WatchScope();

            public static IDisposable Enter()
            {
                AtomBase.Stack.Push(null);

                return Instance;
            }

            public void Dispose()
            {
                AtomBase.Stack.Pop();
            }
        }
    }
}