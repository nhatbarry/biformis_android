using System.Collections;
using CaptainPinkTurd.Core.InputPaths;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace CaptainPinkTurd.MobileControls.Tests
{
    public class CheatHudTests
    {
        private GameObject hud;
        private GameObject eventSystem;

        [SetUp]
        public void SetUp()
        {
            CheatSettings.Enabled = false;
            foreach (var existing in Object.FindObjectsByType<MobileControlsHUD>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(existing.gameObject);
            eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            hud = new GameObject("HUD").AddComponent<MobileControlsHUD>().gameObject;
        }

        [TearDown]
        public void TearDown()
        {
            CheatSettings.Enabled = false;
            Object.DestroyImmediate(hud);
            Object.DestroyImmediate(eventSystem);
        }

        [UnityTest]
        public IEnumerator CheckboxShowsSkullAboveSwitchAndLeftOfDashAndHidingItKeepsHeldMovement()
        {
            yield return ShowHud();
            var button = Find("Cheat Pulse");
            var visibility = button.GetComponent<CanvasGroup>();
            Assert.AreEqual(0f, visibility.alpha);
            Assert.IsFalse(visibility.blocksRaycasts);

            var stick = Find("Move Zone").GetComponent<FloatingOnScreenStick>();
            var pointer = new PointerEventData(EventSystem.current) { pointerId = 0 };
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, stick.transform.position);
            stick.OnPointerDown(pointer);
            pointer.position += Vector2.right * Screen.height * 0.2f;
            stick.OnDrag(pointer);
            yield return null;
            var device = Gamepad.current;
            Assert.IsNotNull(device);
            Assert.Greater(device.leftStick.ReadValue().x, 0.5f);

            CheatSettings.Enabled = true;
            yield return null;
            Assert.AreEqual(1f, visibility.alpha);
            Assert.IsTrue(visibility.blocksRaycasts);
            Assert.IsNotNull(button.GetComponentInChildren<CheatSkullGraphic>());
            Assert.IsNotNull(button.GetComponentInChildren<CheatSkullGraphic>().canvasRenderer,
                "custom skull artwork needs its own canvas renderer to be visible");
            var rect = button.GetComponent<RectTransform>();
            Assert.Greater(rect.anchoredPosition.y,
                Find("Switch Dimension").GetComponent<RectTransform>().anchoredPosition.y);
            Assert.Less(rect.anchoredPosition.x,
                Find("Run And Dash").GetComponent<RectTransform>().anchoredPosition.x);

            CheatSettings.Enabled = false;
            yield return null;
            Assert.AreEqual(0f, visibility.alpha);
            Assert.IsFalse(visibility.blocksRaycasts);
            Assert.AreSame(device, Gamepad.current, "toggling cheat must keep the shared gamepad alive");
            Assert.Greater(device.leftStick.ReadValue().x, 0.5f,
                "toggling cheat must not release a held joystick");
            stick.OnPointerUp(pointer);
        }

        [UnityTest]
        public IEnumerator SkullButtonDrivesPulseActionAndUntickingReleasesHeldPulse()
        {
            yield return ShowHud();
            CheatSettings.Enabled = true;
            using var pulse = new InputAction(type: InputActionType.Button, binding: MobileControlPaths.CheatPulse);
            pulse.Enable();
            bool fired = false;
            pulse.performed += _ => fired = true;
            ExecuteEvents.Execute(Find("Cheat Pulse"), new PointerEventData(EventSystem.current),
                ExecuteEvents.pointerDownHandler);
            yield return null;
            yield return null;
            Assert.IsTrue(fired, "skull must reach gameplay through the virtual gamepad");
            Assert.IsTrue(pulse.IsPressed());

            CheatSettings.Enabled = false;
            yield return null;
            yield return null;
            Assert.IsFalse(pulse.IsPressed(), "unticking a held skull must release its virtual input");
            Assert.IsFalse(Find("Cheat Pulse").GetComponent<MobileHudButton>().IsPressed);
        }

        private IEnumerator ShowHud()
        {
            yield return null;
            hud.GetComponentInChildren<Canvas>(true).gameObject.SetActive(true);
            yield return null;
        }

        private GameObject Find(string name)
        {
            foreach (var child in hud.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;
            Assert.Fail($"Missing control: {name}");
            return null;
        }
    }
}
