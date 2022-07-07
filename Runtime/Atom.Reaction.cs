using System;
using UniMob.Core;

namespace UniMob
{
    public static partial class Atom
    {
        /// <summary>
        /// Creates an reaction that should run every time anything it observes changes.
        /// It also runs once when you create the reaction itself. It only responds
        /// to changes in observable state, things you have annotated atom.
        /// </summary>
        /// <param name="lifetime">Reaction lifetime.</param>
        /// <param name="reaction">A function for reaction.</param>
        /// <param name="exceptionHandler">A function that called when an exception is thrown while computing an reaction.</param>
        /// <param name="debugName">Debug name for this reaction.</param>
        /// <returns>Created reaction.</returns>
        /// <example>
        /// 
        /// var counter = Atom.Value(1);
        /// 
        /// Atom.Reaction(Lifetime, () =>
        /// {
        ///     Debug.Log("Counter: " + counter.Value);
        /// });
        /// 
        /// </example>
        public static Reaction Reaction(
            Lifetime lifetime,
            Action reaction,
            Action<Exception> exceptionHandler = null,
            string debugName = null)
        {
            var atom = new ReactionAtom(debugName, reaction, exceptionHandler);
            lifetime.Register(atom);
            atom.Activate();
            return atom;
        }

        /// <summary>
        /// Creates an reaction that takes two functions:
        /// the first, data function, is tracked and returns the data that is used
        /// as input for the second, effect function. It is important to note that
        /// the side effect only reacts to data that was accessed in the data function,
        /// which might be less than the data that is actually used in the effect function.
        /// 
        /// The typical pattern is that you produce the things you need in your side effect
        /// in the data function, and in that way control more precisely when the effect triggers.
        /// </summary>
        /// <param name="lifetime">Reaction lifetime.</param>
        /// <param name="data">A data function.</param>
        /// <param name="effect">A side effect function.</param>
        /// <param name="exceptionHandler">A function that called when an exception is thrown while computing an reaction.</param>
        /// <param name="fireImmediately">Should the side effect runs once when you create a reaction itself? Default value is true.</param>
        /// <param name="debugName">Debug name for this reaction.</param>
        /// <typeparam name="T">Reaction data type.</typeparam>
        /// <returns>Created reaction.</returns>
        /// <example>
        /// 
        /// var counter = Atom.Value(1);
        /// 
        /// Atom.Reaction(Lifetime, () => counter.Value, value =>
        /// {
        ///     Debug.Log("Counter: " + value);
        /// });
        /// 
        /// </example>
        public static Reaction Reaction<T>(
            Lifetime lifetime,
            Func<T> data,
            Action<T> effect,
            Action<Exception> exceptionHandler = null,
            bool fireImmediately = true,
            string debugName = null)
        {
            var dataAtom = Computed(lifetime, data);
            var firstRun = true;

            return Reaction(lifetime, () =>
            {
                var value = dataAtom.Value;

                using (NoWatch)
                {
                    if (firstRun)
                    {
                        firstRun = false;

                        if (!fireImmediately)
                        {
                            return;
                        }
                    }

                    effect(value);
                }
            }, exceptionHandler, debugName);
        }
    }
}