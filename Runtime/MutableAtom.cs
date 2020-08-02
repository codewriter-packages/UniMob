namespace UniMob
{
    public delegate T AtomPull<out T>();

    public delegate void AtomPush<in T>(T value);

    // ReSharper disable once InconsistentNaming
    public interface MutableAtom<T> : Atom<T>
    {
        new T Value { get; set; }
    }
}