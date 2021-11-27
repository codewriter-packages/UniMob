#if !UNIMOB_DISABLE_CODEGEN_CHECKER

using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

namespace UniMob
{
    internal class UniMobCodeGenChecker : ILifetimeScope
    {
        [Atom] private int WeavedProperty { get; } = 0;

        public Lifetime Lifetime => Lifetime.Eternal;

#if UNITY_EDITOR
        [UnityEditor.Callbacks.DidReloadScripts]
#endif
        [RuntimeInitializeOnLoadMethod]
        private static void Check()
        {
            if (!IsAtomWeaverSucceed())
            {
                Debug.LogError("[UniMob] Failed to run code weaver");
            }
        }

        [Preserve]
        private void Preserve()
        {
            Debug.Log(WeavedProperty);
        }

        private static bool IsAtomWeaverSucceed()
        {
            var type = typeof(UniMobCodeGenChecker);

            foreach (var fi in type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (typeof(Atom<int>).IsAssignableFrom(fi.FieldType))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

#endif