namespace UniMob
{
    public delegate T AtomPull<out T>();

    public delegate void AtomPush<in T>(T value);

    // ReSharper disable once InconsistentNaming
    public interface MutableAtom<T> : Atom<T>
    {
        /// <summary>
        /// Gets the value of the current Atom instance.<br/>
        /// </summary>
        /// <remarks>
        /// Will start tracking the value of the current atom if the function
        /// is called inside a reaction. Otherwise, the value will be accessed directly,
        /// as if getting the value of a regular property.
        /// </remarks>
        new T Value { get; set; }
    }
}