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
    
    /// <summary>
    /// Tells the compiler that a class should be included to code generation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class AtomContainerAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Assembly)]
    public class AtomGenerateDebugNamesAttribute : Attribute
    {
    }
}