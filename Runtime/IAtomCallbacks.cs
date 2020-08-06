using System;
using UnityEngine;

namespace UniMob
{
    public interface IAtomCallbacks
    {
        void OnActive();
        void OnInactive();
    }

    public class ActionAtomCallbacks : IAtomCallbacks
    {
        private readonly Action _onActive;
        private readonly Action _onInactive;

        public ActionAtomCallbacks(Action onActive, Action onInactive)
        {
            _onActive = onActive ?? delegate { };
            _onInactive = onInactive ?? delegate { };
        }

        public void OnActive()
        {
            try
            {
                _onActive();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void OnInactive()
        {
            try
            {
                _onInactive();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}