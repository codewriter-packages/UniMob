using System;
using UnityEngine;

namespace UniMob.Core
{
    public class ReactionAtom : AtomBase, Reaction
    {
        private readonly Action _reaction;
        private readonly Action<Exception> _exceptionHandler;

        public ReactionAtom(
            Lifetime lifetime,
            string debugName,
            Action reaction,
            Action<Exception> exceptionHandler = null)
            : base(lifetime, debugName, AtomOptions.AutoActualize)
        {
            _reaction = reaction ?? throw new ArgumentNullException(nameof(reaction));
            _exceptionHandler = exceptionHandler ?? Debug.LogException;
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
                _reaction();
            }
            catch (Exception exception)
            {
                _exceptionHandler(exception);
            }
        }
    }
}