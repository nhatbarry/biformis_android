using System;
using System.Collections.Generic;
using CaptainPinkTurd.Core.Enum;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Core.InputPaths
{
    /// <summary>
    /// What the Interact input (E, the touch HUD's "!" button) can do right now, shared between gameplay and the
    /// touch HUD, which can't see each other: whether something is in the player's reach - an open level door, the box
    /// at the end of Level 4 - so the HUD's button lights up, and whether a cutscene wants the movement and interact
    /// controls on screen (the HUD otherwise only shows in levels).
    /// </summary>
    public static class InteractPrompt
    {
        private static readonly List<Object> inReach = new();

        /// <summary>Raised when a cutscene asks for the controls or lets them go.</summary>
        public static event Action CutsceneControlsChanged;

        public static bool CutsceneControls { get; private set; }

        /// <summary>The form whose colours the controls take in a cutscene (red = B, blue = A).</summary>
        public static EColor CutsceneTheme { get; private set; } = EColor.Red;

        /// <summary>Something the player can act on with Interact is in reach.</summary>
        public static bool Available
        {
            get
            {
                inReach.RemoveAll(source => !source); //a door unloaded with its level while the player stood at it
                return inReach.Count > 0;
            }
        }

        public static void SetInReach(Object source, bool reachable)
        {
            if (!reachable) inReach.Remove(source);
            else if (!inReach.Contains(source)) inReach.Add(source);
        }

        public static void ShowCutsceneControls(EColor theme)
        {
            CutsceneTheme = theme;
            if (CutsceneControls) return;
            CutsceneControls = true;
            CutsceneControlsChanged?.Invoke();
        }

        public static void HideCutsceneControls()
        {
            if (!CutsceneControls) return;
            CutsceneControls = false;
            CutsceneControlsChanged?.Invoke();
        }

        //the project runs with domain reload off: nothing may survive from the last play session
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlay()
        {
            inReach.Clear();
            CutsceneControls = false;
            CutsceneTheme = EColor.Red;
            CutsceneControlsChanged = null;
        }
    }
}
