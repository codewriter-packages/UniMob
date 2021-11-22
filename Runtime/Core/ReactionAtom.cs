using System;
using UnityEngine;

namespace UniMob.Core
{
    public class ReactionAtom : AtomBase, Reaction
    {
        internal readonly Action reaction;
        internal readonly Action<Exception> exceptionHandler;

        public ReactionAtom(
            Lifetime lifetime,
            string debugName,
            Action reaction,
            Action<Exception> exceptionHandler = null)
            : base(lifetime, debugName, AtomOptions.AutoActualize)
        {
            this.reaction = reaction ?? throw new ArgumentNullException(nameof(reaction));
            this.exceptionHandler = exceptionHandler ?? Debug.LogException;
        }

        public void Activate(bool force = false)
        {
            Actualize(force);
        }

        protected override void Evaluate()
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