using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.MobileControls;
using CaptainPinkTurd.Story.Cutscene;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// Drives the touch HUD the way a finger does, through the event system's pointer events (injected touchscreen
    /// events don't reach devices under -batchmode, see the mobile controls tests). The HUD lands on the same virtual
    /// gamepad controls as on a phone, so the game hears exactly what it hears there.
    /// </summary>
    internal static class TouchHud
    {
        /// <summary>
        /// The HUD spawns itself once per play session (the build target is Android), but the mobile controls tests
        /// destroy it to build their own: bring one back if it is gone.
        /// </summary>
        public static MobileControlsHUD Ensure()
        {
            var hud = Object.FindAnyObjectByType<MobileControlsHUD>(FindObjectsInactive.Include);
            return hud ? hud : new GameObject("Mobile Controls HUD").AddComponent<MobileControlsHUD>();
        }

        /// <summary>The control, if the HUD shows it right now.</summary>
        public static GameObject Find(string name)
        {
            var hud = Ensure();
            foreach (var t in hud.GetComponentsInChildren<Transform>(false))
            {
                if (t.name == name) return t.gameObject;
            }
            return null;
        }

        /// <summary>The clip a control's art plays, e.g. "red_ready".</summary>
        public static string Clip(string control)
        {
            var go = Find(control);
            Assert.IsNotNull(go, $"the touch HUD doesn't show {control}");
            return go.GetComponentInChildren<HudSpriteAnimation>().CurrentClip;
        }

        public static void Press(string button)
        {
            var go = Find(button);
            Assert.IsNotNull(go, $"the touch HUD doesn't show {button}");
            ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
        }

        public static void Release(string button)
        {
            var go = Find(button);
            if (go) ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);
        }

        /// <summary>A tap on the button: down, a few frames, up.</summary>
        public static IEnumerator Tap(string button)
        {
            Press(button);
            yield return null;
            yield return null;
            Release(button);
            yield return null;
        }

        /// <summary>A thumb put on the joystick zone and pushed that way (until <see cref="LetGoOfStick"/>).</summary>
        public static PointerEventData PushStick(Vector2 direction)
        {
            var zone = Find("Move Zone");
            Assert.IsNotNull(zone, "the touch HUD shows no joystick");
            var start = RectTransformUtility.WorldToScreenPoint(null, zone.transform.position);
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 7, position = start, pressPosition = start };
            ExecuteEvents.Execute(zone, pointer, ExecuteEvents.pointerDownHandler);
            pointer.position = start + direction.normalized * Screen.height * 0.2f;
            ExecuteEvents.Execute(zone, pointer, ExecuteEvents.dragHandler);
            return pointer;
        }

        public static void LetGoOfStick(PointerEventData pointer)
        {
            var zone = Find("Move Zone");
            if (zone) ExecuteEvents.Execute(zone, pointer, ExecuteEvents.pointerUpHandler);
        }

        /// <summary>
        /// The end of Level 4 on a phone: steer B to A with the joystick, then tap the "!" button once it lights up.
        /// </summary>
        public static IEnumerator TakeTheBox(CutsceneStage stage, float timeoutSeconds = 10f)
        {
            Assert.IsTrue(stage.IsReaching, "nothing to walk up to");
            var target = stage.ReachTarget;
            var walker = (RectTransform)target.parent.Find("B4");
            var thumb = PushStick(walker.anchoredPosition.x > target.anchoredPosition.x ? Vector2.left : Vector2.right);
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (Clip("Interact") == null || !Clip("Interact").EndsWith("ready"))
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"the interact button never lit up (B at {walker.anchoredPosition.x}, A at {target.anchoredPosition.x})");
                yield return null;
            }
            LetGoOfStick(thumb);
            yield return Tap("Interact");
        }

        public static List<string> Shown(params string[] names)
        {
            var shown = new List<string>();
            foreach (var name in names)
            {
                if (Find(name)) shown.Add(name);
            }
            return shown;
        }
    }
}
