namespace UniMob
{
    public static class AtomExtension
    {
        /// <summary>
        /// Gets the value of the current Atom instance.
        /// </summary>
        /// <param name="atom">CurrentAtom instance.</param>
        /// <returns>The value of the current Atom instance.</returns>
        public static T Get<T>(this Atom<T> atom)
        {
            return atom.Value;
        }

        /// <summary>
        /// Sets the value for the currentAtom instance.
        /// </summary>
        /// <param name="atom">Current Atom instance.</param>
        /// <param name="value">The new value for the current Atom instance.</param>
        public static void Set<T>(this MutableAtom<T> atom, T value)
        {
            atom.Value = value;
        }
    }
}