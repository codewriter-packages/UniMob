using System;
using JetBrains.Annotations;

namespace UniMob
{
    public static partial class Atom
    {
        [CanBeNull] public static AtomBase CurrentScope => AtomBase.Stack.Peek();

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
        /// <param name="lifetime">Atom lifetime.</param>
        /// <param name="value">Initial value.</param>
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
        public static MutableAtom<T> Value<T>(Lifetime lifetime, T value, string debugName = null)
        {
            return new ValueAtom<T>(lifetime, debugName, value);
        }

        public static MutableAtom<T> Value<T>(T value, string debugName = null)
        {
            return new ValueAtom<T>(Lifetime.Eternal, debugName, value);
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
        /// <param name="lifetime">Atom lifetime.</param>
        /// <param name="pull">Function for pulling value.</param>
        /// <param name="keepAlive">Should an atom keep its value actualized when there are no subscribers?</param>
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
        public static Atom<T> Computed<T>(Lifetime lifetime, AtomPull<T> pull,
            bool keepAlive = false, string debugName = null)
        {
            return new ComputedAtom<T>(lifetime, debugName, pull, null, keepAlive);
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
        /// <param name="lifetime">Atom lifetime.</param>
        /// <param name="pull">Function for pulling value.</param>
        /// <param name="push">Function for pushing new value.</param>
        /// <param name="keepAlive">Should an atom keep its value actualized when there are no subscribers?</param>
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
        public static MutableAtom<T> Computed<T>(Lifetime lifetime, AtomPull<T> pull, AtomPush<T> push,
            bool keepAlive = false, string debugName = null)
        {
            return new ComputedAtom<T>(lifetime, debugName, pull, push, keepAlive);
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