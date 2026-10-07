using System;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using UnityEngine;

namespace CaptainPinkTurd.MobileControls
{
    /// <summary>
    /// The touch HUD's artwork: the team's UI_Controls_Android pack (Assets/Sprites/UI/Mobile Controls), one entry per
    /// control holding its Aseprite tags as clips. Every file carries both themes, so clip names start with "red_"
    /// (B's form) or "blue_" (A's form), e.g. "blue_ready". Loaded from Resources, so the HUD still needs no scene edits.
    /// </summary>
    public class MobileControlsArt : ScriptableObject
    {
        public const string ResourcePath = "Mobile Controls Art";

        [Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames;
            [Tooltip("Seconds per frame, as set in Aseprite")]
            public float[] durations;
            [Tooltip("Loops, or holds its last frame")]
            public bool loop = true;
        }

        [Serializable]
        public class Control
        {
            public Clip[] clips;

            public Clip Find(string clipName)
            {
                if (clips == null) return null;
                foreach (var clip in clips)
                {
                    if (clip.name == clipName) return clip;
                }
                return null;
            }
        }

        [Tooltip("UI_Joystick_Base: idle, active (while touched)")]
        public Control joystickBase;
        [Tooltip("UI_Joystick_Knob: idle, pressed")]
        public Control joystickKnob;
        [Tooltip("UI_Btn_Dash: idle, pressed (no cooldown: the dash has none to show)")]
        public Control dash;
        [Tooltip("UI_Btn_Interact: disabled (nothing in reach), ready (blinks), pressed")]
        public Control interact;
        [Tooltip("UI_Btn_Swap: idle, pressed, and to_blue / to_red when the form changes")]
        public Control swap;
        [Tooltip("UI_Btn_Pause: pause, pause_pressed, play, play_pressed (play while the pause menu is open)")]
        public Control pause;

        [Tooltip("Raised by the game whenever the player changes form: the controls take that form's colours")]
        public EnumColorEvent onDimensionChange;

        [Tooltip("Canvas units per art pixel; the HUD keeps a whole number of screen pixels per art pixel")]
        public int unitsPerPixel = 4;

        public static MobileControlsArt Load() => Resources.Load<MobileControlsArt>(ResourcePath);
    }
}
