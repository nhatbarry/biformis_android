using System.Collections.Generic;
using UnityEngine;

namespace CaptainPinkTurd.Core.SO
{
    public abstract class RuntimeScriptableObject : ScriptableObject
    {
        private static readonly List<RuntimeScriptableObject> instances = new();
        private static bool sessionReset;

        private void OnEnable()
        {
            instances.Add(this);

            //an asset first loaded after the session started (e.g. with a later scene, which is most assets in a build)
            //missed ResetAllInstance, so give it its fresh runtime state now
            if (Application.isPlaying && sessionReset) OnReset();
        }

        private void OnDisable() => instances.Remove(this);

        protected abstract void OnReset();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearSessionFlag() => sessionReset = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetAllInstance()
        {
            foreach (var instance in instances)
            {
                instance.OnReset();
            }
            sessionReset = true;
        }
    }
}
