namespace UniMob
{
    // ReSharper disable once InconsistentNaming
    public interface Atom<out T>
    {
        T Value { get; }

        void Deactivate();

        void Invalidate();
    }
}