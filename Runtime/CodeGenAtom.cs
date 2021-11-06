namespace UniMob
{
    public static class CodeGenAtom
    {
        public static ComputedAtom<T> Create<T>(string debugName, AtomPull<T> pull,
            bool keepAlive, bool requireReaction)
        {
            var lifetime = Lifetime.Eternal;
            return new ComputedAtom<T>(lifetime, debugName, pull, null);
        }
    }
}