namespace UniMob
{
    public static class CodeGenAtom
    {
        public static ComputedAtom<T> Create<T>(string debugName, AtomPull<T> pull,
            bool keepAlive, bool requireReaction)
        {
            return new ComputedAtom<T>(debugName, pull,
                keepAlive: keepAlive, requiresReaction: requireReaction);
        }
    }
}