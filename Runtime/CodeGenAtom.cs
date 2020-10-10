namespace UniMob
{
    public static class CodeGenAtom
    {
        public static ComputedAtom<T> Create<T>(string debugName, AtomPull<T> pull)
        {
            return new ComputedAtom<T>(debugName, pull);
        }
    }
}