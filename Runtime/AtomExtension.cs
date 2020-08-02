namespace UniMob
{
    public static class AtomExtension
    {
        public static T Get<T>(this Atom<T> atom)
        {
            return atom.Value;
        }

        public static void Set<T>(this MutableAtom<T> atom, T value)
        {
            atom.Value = value;
        }
    }
}