using System;
using UniMob.Core;

namespace UniMob
{
    public static partial class Atom
    {
        /// <summary>
        /// Creates an atom that value can be controlled by external datasource that can be subscribed to.
        /// </summary>
        /// <remarks>
        /// The created atom will subscribe to the datasource on atom activation,
        /// unsubscribing when atom is deactivated or disposed.
        /// The subscribe callback itself will receive a sink callback,
        /// which can be used to update the current state of the atom.
        /// </remarks>
        /// <param name="lifetime">Atom lifetime.</param>
        /// <param name="initialValue">Initial value.</param>
        /// <param name="subscribe">Subscribe callback.</param>
        /// <param name="unsubscribe">Unsubscribe callback.</param>
        /// <param name="debugName">Debug name for this atom.</param>
        /// <typeparam name="T">Atom value type.</typeparam>
        /// <returns>Created atom.</returns>
        public static Atom<T> FromSink<T>(Lifetime lifetime,
            T initialValue,
            Action<AtomSink<T>> subscribe,
            Action unsubscribe = null,
            string debugName = null)
        {
            var atom = new SinkAtom<T>(debugName, initialValue, subscribe, unsubscribe);
            lifetime.Register(atom);
            return atom;
        }
    }
}