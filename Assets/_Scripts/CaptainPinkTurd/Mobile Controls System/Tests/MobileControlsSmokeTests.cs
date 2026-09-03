using System.Collections;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CaptainPinkTurd.MobileControls.Tests
{
    /// <summary>
    /// Proves the touch HUD lands on the same actions the desktop keys do, which is the whole premise of
    /// the Android port: gameplay code is untouched because touch input arrives through the same actions.
    /// </summary>
    /// <remarks>
    /// These drive the HUD's pointer handlers directly. A version that drove a virtual Touchscreen through a
    /// real InputSystemUIInputModule - to cover multi-touch end to end - was tried and dropped: injected
    /// touch events never reach the device under -batchmode (the touch controls stayed at press=false,
    /// position=0 through three different injection methods), so it could only ever have reported a false
    /// failure. Multi-touch is worth checking by hand on a device.
    /// </remarks>
    public class MobileControlsSmokeTests
    {
        private GameObject hud;
        private GameObject eventSystem;
        private InputSystemActions actions;

        [SetUp]
        public void SetUp()
        {
            // The build target is Android, so the HUD has already auto-spawned itself into the test scene.
            // Clear it out so each test owns the one it builds.
            foreach (var existing in Object.FindObjectsByType<MobileControlsHUD>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            hud = new GameObject("HUD").AddComponent<MobileControlsHUD>().gameObject;

            actions = new InputSystemActions();
            actions.Enable();
        }

        [TearDown]
        public void TearDown()
        {
            actions.Disable();
            actions.Dispose();

            Object.DestroyImmediate(hud);
            Object.DestroyImmediate(eventSystem);
        }

        [UnityTest]
        public IEnumerator BuildsTheExpectedControls()
        {
            yield return ShowHud();

            Assert.IsNotNull(Find("Move Zone"), "joystick zone missing");
            Assert.IsNotNull(Find("Switch Dimension"), "dimension button missing");
            Assert.IsNotNull(Find("Run And Dash"), "run/dash button missing");
            Assert.IsNotNull(Find("Pause"), "pause button missing");
            Assert.IsNotNull(Gamepad.current, "no virtual gamepad was created by the on-screen controls");
        }

        [UnityTest]
        public IEnumerator JoystickDrivesTheMoveAction()
        {
            yield return ShowHud();

            var zone = Find("Move Zone");
            var stick = zone.GetComponent<FloatingOnScreenStick>();
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 0 };

            pointer.position = ScreenPointOf(zone);
            stick.OnPointerDown(pointer);
            pointer.position += new Vector2(Screen.height * 0.2f, 0f);
            stick.OnDrag(pointer);

            yield return null;
            yield return null;

            Vector2 move = actions.Player.Move.ReadValue<Vector2>();
            Assert.Greater(move.x, 0.5f, $"dragging right should move right, got {move}");

            stick.OnPointerUp(pointer);
            yield return null;
            yield return null;

            Assert.AreEqual(Vector2.zero, actions.Player.Move.ReadValue<Vector2>(),
                "letting go must stop the player");
        }

        /// <summary>
        /// Colliding with an enemy runs HitStop, which zeroes Time.timeScale for a moment. Whatever the HUD
        /// does around that, a finger that never left the glass has to keep steering - having to lift and
        /// press again after every hit is the bug this guards.
        /// </summary>
        [UnityTest]
        public IEnumerator JoystickKeepsSteeringThroughAnInterruptionWithoutLiftingTheFinger()
        {
            yield return ShowHud();

            var zone = Find("Move Zone");
            var stick = zone.GetComponent<FloatingOnScreenStick>();
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 0 };

            pointer.position = ScreenPointOf(zone);
            stick.OnPointerDown(pointer);
            pointer.position += new Vector2(Screen.height * 0.2f, 0f);
            stick.OnDrag(pointer);

            yield return null;
            yield return null;

            Assert.Greater(actions.Player.Move.ReadValue<Vector2>().x, 0.5f, "should be walking right");

            // The stick gets switched off and back on under a finger that is still down.
            zone.SetActive(false);
            yield return null;
            zone.SetActive(true);
            yield return null;

            // No new press arrives, because the finger never lifted - only more drags.
            stick.OnDrag(pointer);
            yield return null;
            yield return null;

            Assert.Greater(actions.Player.Move.ReadValue<Vector2>().x, 0.5f,
                "the stick must pick the held finger back up, and keep the direction it already had");
        }

        [UnityTest]
        public IEnumerator RunButtonHoldsRunAndTapsDash()
        {
            yield return ShowHud();

            var button = Find("Run And Dash");
            Press(button);
            yield return null;
            yield return null;

            Assert.IsTrue(actions.Player.Run.IsPressed(), "holding the button must hold Run, like left shift");
            Assert.IsTrue(actions.Player.Dash.IsPressed(), "the same press must also drive Dash, like left shift");

            Release(button);
            yield return null;
            yield return null;

            Assert.IsFalse(actions.Player.Run.IsPressed(), "releasing must stop running");
        }

        [UnityTest]
        public IEnumerator DimensionButtonFiresTheSameActionAsTheJKey()
        {
            yield return ShowHud();

            // Rebuilt exactly as GameManager builds it.
            var switchDimension = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/j");
            switchDimension.AddBinding(MobileControlPaths.SwitchDimension);
            switchDimension.Enable();

            bool fired = false;
            switchDimension.performed += _ => fired = true;

            Press(Find("Switch Dimension"));
            yield return null;
            yield return null;

            switchDimension.Disable();
            switchDimension.Dispose();

            Assert.IsTrue(fired, "the dimension button must trigger the same action the J key does");
        }

        /// <summary>
        /// The pause menu is opened by the Popup Manager prefab that sits in every level, listening to UI's
        /// Cancel action - which is Escape on a keyboard. The pause button has to land on that same action.
        /// </summary>
        [UnityTest]
        public IEnumerator PauseButtonFiresTheSameActionAsEscape()
        {
            yield return ShowHud();

            bool fired = false;
            actions.UI.Cancel.performed += _ => fired = true;

            Press(Find("Pause"));
            yield return null;
            yield return null;

            Assert.IsTrue(fired, "the pause button must trigger UI/Cancel, the action Escape opens the menu with");
        }



        private IEnumerator ShowHud()
        {
            yield return null;

            // The HUD hides itself outside gameplay scenes; the test scene is not one, so show it by hand.
            var canvas = hud.GetComponentInChildren<Canvas>(true);
            Assert.IsNotNull(canvas, "the HUD did not build a canvas");
            canvas.gameObject.SetActive(true);

            yield return null;
        }

        private GameObject Find(string name)
        {
            foreach (var t in hud.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t.gameObject;
            }

            return null;
        }

        private static Vector2 ScreenPointOf(GameObject go)
        {
            var rect = (RectTransform)go.transform;
            return RectTransformUtility.WorldToScreenPoint(null, rect.position);
        }

        private static void Press(GameObject go) =>
            ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);

        private static void Release(GameObject go) =>
            ExecuteEvents.Execute(go, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);

    }
}
