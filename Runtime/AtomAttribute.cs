using System;

namespace UniMob
{
    /// <summary>
    /// Tells the compiler that a property should be converted to a computed atom at compile time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class AtomAttribute : Attribute
    {
    }
}