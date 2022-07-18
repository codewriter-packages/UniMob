using System;
using JetBrains.Annotations;
using UniMob.Core;

namespace UniMob
{
    public static partial class Atom
    {
        [CanBeNull] public static AtomBase CurrentScope => AtomBase.Stack;

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
        /// var counter = Atom.Value(Lifetime, 0);
        /// counter.Value += 1;
        /// 
        /// Debug.Log(counter.Value);
        /// 
        /// </example>
        public static MutableAtom<T> Value<T>(Lifetime lifetime, T value, string debugName = null)
        {
            var atom = new ValueAtom<T>(debugName, value);
            lifetime.Register(atom);
            return atom;
        }

        /// <summary>
        /// Creates an atom that store the value.
        /// </summary>
        /// <param name="value">Initial value.</param>
        /// <param name="debugName">Debug name for this atom.</param>
        /// <typeparam name="T">Atom value type.</typeparam>
        /// <returns>Created atom.</returns>
        /// <example>
        /// 
        /// var counter = Atom.Value(0);
        /// counter.Value += 1;
        /// 
        /// Debug.Log(counter.Value);
        /// 
        /// </example>
        public static MutableAtom<T> Value<T>(T value, string debugName = null)
        {
            return Value(Lifetime.Eternal, value, debugName);
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
        /// var sum = Atom.Computed(Lifetime, () => a.Value + b.Value);
        /// 
        /// Debug.Log(sum.Value);
        /// 
        /// </example>
        public static Atom<T> Computed<T>(Lifetime lifetime, Func<T> pull,
            bool keepAlive = false, string debugName = null)
        {
            var atom = new ComputedAtom<T>(debugName, pull, keepAlive);
            lifetime.Register(atom);
            return atom;
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
        /// var sum = Atom.Computed(Lifetime, () => a.Value + b.Value);
        /// 
        /// Debug.Log(sum.Value);
        /// 
        /// </example>
        public static MutableAtom<T> Computed<T>(Lifetime lifetime, Func<T> pull, Action<T> push,
            bool keepAlive = false, string debugName = null)
        {
            var atom = new MutableComputedAtom<T>(debugName, pull, push, keepAlive);
            lifetime.Register(atom);
            return atom;
        }
    }
}