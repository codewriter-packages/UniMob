using System;

namespace UniMob
{
    // ReSharper disable once InconsistentNaming
    public interface Atom<out T>
    {
        /// <summary>
        /// Gets the value of the current Atom instance.<br/>
        /// </summary>
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

    // ReSharper disable once InconsistentNaming
    public interface MutableAtom<T> : Atom<T>
    {
        /// <summary>
        /// Gets the value of the current Atom instance.<br/>
        /// </summary>
        new T Value { get; set; }
    }

    // ReSharper disable once InconsistentNaming
    public interface Reaction
    {
        /// <summary>
        /// Starts reaction.
        /// </summary>
        /// <param name="force"></param>
        void Activate(bool force = false);

        /// <summary>
        /// Suspends reaction and clean up all subscriptions.<br/>
        /// Note, that reaction will be restarted if it will be activated again.
        /// </summary>
        void Deactivate();
    }

    // ReSharper disable once InconsistentNaming
    public interface AtomSink<T>
    {
        void SetValue(T value);
        void SetException(Exception exception);
    }

    // ReSharper disable once InconsistentNaming
    public interface AsyncAtom<T> : Atom<AtomAsyncValue<T>>
    {
        void Reload(bool clearValue = true);
    }
}