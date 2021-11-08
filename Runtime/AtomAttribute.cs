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
        /// Should an atom keep its value actualized when there are no subscribers?
        /// </summary>
        public bool KeepAlive { get; set; }
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public class AtomGenerateDebugNamesAttribute : Attribute
    {
    }
}