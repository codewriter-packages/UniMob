namespace UniMob
{
    // ReSharper disable once InconsistentNaming
    public interface Atom<out T>
    {
        /// <summary>
        /// Gets the value of the current Atom instance.<br/>
        /// </summary>
        /// <remarks>
        /// Will start tracking the value of the current atom if the function
        /// is called inside a reaction. Otherwise, the value will be accessed directly,
        /// as if getting the value of a regular property.
        /// </remarks>
        T Value { get; }

        /// <summary>
        /// Suspends tracking the value of the current Atom and clean up all subscriptions.<br/>
        /// Note, that tracking of the Atom will be restarted if its value is read after deactivation.
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Makes Atom invalid, so it value will be recalculated the next time you try to get it.<br/>
        /// Unlike Deactivate, this method does not clean up atom subscriptions
        /// and the atom will continue to work as usual.
        /// </summary>
        void Invalidate();
    }
}