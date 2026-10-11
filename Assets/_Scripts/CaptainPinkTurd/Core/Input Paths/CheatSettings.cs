using System;
using UnityEngine;

namespace CaptainPinkTurd.Core.InputPaths
{
    /// <summary>The pause menu's optional cheat control, shared by gameplay and the touch HUD.</summary>
    public static class CheatSettings
    {
        private static bool enabled;

        public static event Action<bool> Changed;

        public static bool Enabled
        {
            get => enabled;
            set
            {
                if (enabled == value) return;
                enabled = value;
                Changed?.Invoke(value);
            }
        }

        // Domain reload is disabled in this project; every new play session starts with cheats off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlay()
        {
            enabled = false;
            Changed = null;
        }
    }
}
