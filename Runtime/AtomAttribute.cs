using System;

namespace UniMob
{
#if !UNIMOB_CODEGEN_ENABLED
    [Obsolete("Required UNIMOB_CODEGEN_ENABLED define", true)]
#endif
    [AttributeUsage(AttributeTargets.Property)]
    public class AtomAttribute : Attribute
    {
    }
}