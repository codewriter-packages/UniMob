using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace UniMob.Core
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class ReactionAtom : AtomBase, Reaction
    {
        internal readonly Action reaction;
        internal readonly Action<Exception> exceptionHandler;

        public ReactionAtom(
            string debugName,
            Action reaction,
            Action<Exception> exceptionHandler = null)
        {
            this.debugName = debugName;
            this.reaction = reaction ?? throw new ArgumentNullException(nameof(reaction));
            this.exceptionHandler = exceptionHandler ?? Debug.LogException;
            options = AtomOptions.AutoActualize;
        }

        public void Activate(bool force = false)
        {
            Actualize(force);
        }

        protected override void Evaluate(bool activating)
        {
            state = AtomState.Actual;

            try
            {
                reaction();
            }
            catch (Exception exception)
            {
                exceptionHandler(exception);
            }
        }
    }
}