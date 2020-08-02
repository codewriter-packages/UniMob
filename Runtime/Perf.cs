using System;
using UnityEngine;
using UnityEngine.Profiling;

namespace UniMob
{
    internal class PerfWatcher : IDisposable
    {
        private readonly string _name;
        private readonly CustomSampler _sampler;

        public string Name => _name;

        public PerfWatcher(string name)
        {
            _name = name;
#if UNITY_EDITOR
            _sampler = name != null ? CustomSampler.Create(name) : null;
#endif
        }

        public IDisposable Watch()
        {
#if UNITY_EDITOR
            _sampler?.Begin();
#endif
            return this;
        }

        public IDisposable Watch(GameObject context)
        {
#if UNITY_EDITOR
            _sampler?.Begin(context);
#endif
            return this;
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            _sampler?.End();
#endif
        }
    }
}