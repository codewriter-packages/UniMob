using System;

namespace UniMob
{
    /// <summary>
    /// Tells the compiler that a property should be converted to a computed atom at compile time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AtomAttribute : Attribute
    {
        /// <summary>
        /// Should an atom keep its value cached when there are no subscribers?
        /// </summary>
        public bool KeepAlive { get; set; }

        /// <summary>
        /// Should an atom print warnings when its values are tried to be read outside of the reaction?
        /// </summary>
        public bool RequireReaction { get; set; }
    }
}