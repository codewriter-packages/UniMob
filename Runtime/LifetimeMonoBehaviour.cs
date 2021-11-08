using UnityEngine;

namespace UniMob
{
    public abstract class LifetimeMonoBehaviour : MonoBehaviour, IAtomScope
    {
        private readonly LifetimeController _lifetimeController = new LifetimeController();

        public Lifetime Lifetime => _lifetimeController.Lifetime;

        protected virtual void Start()
        {
        }

        protected virtual void OnDestroy()
        {
            _lifetimeController.Dispose();
        }
    }
}