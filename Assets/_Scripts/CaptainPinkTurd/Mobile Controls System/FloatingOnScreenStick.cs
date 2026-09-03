using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.OnScreen;

namespace CaptainPinkTurd.MobileControls
{
    /// <summary>
    /// A floating on-screen stick: invisible until a finger lands anywhere in its zone, at which point the
    /// stick springs up under that finger. Feeds a virtual <c>Gamepad</c> stick, so every system that reads
    /// the Move action keeps working untouched.
    /// </summary>
    /// <remarks>
    /// This exists instead of Unity's <c>OnScreenStick</c> because that component only accepts a finger that
    /// lands on the stick graphic itself, which is the opposite of what a floating stick needs.
    /// </remarks>
    [AddComponentMenu("Input/Floating On-Screen Stick")]
    [RequireComponent(typeof(RectTransform))]
    public class FloatingOnScreenStick : OnScreenControl, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private const int NoPointer = int.MinValue;

        [InputControl(layout = "Vector2")]
        [SerializeField] private string stickControlPath = "<Gamepad>/leftStick";

        [Header("References")]
        [Tooltip("The stick's base. Moved to wherever the finger lands and hidden while nothing is touching.")]
        [SerializeField] private RectTransform stickBase;
        [Tooltip("The knob that follows the finger inside the base.")]
        [SerializeField] private RectTransform stickKnob;

        [Header("Feel")]
        [Tooltip("How far, in canvas units, the knob travels before the stick reads as fully deflected.")]
        [SerializeField] private float movementRange = 34f;
        [Tooltip("Deflection below this fraction reads as no input, so resting thumbs don't creep.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float deadZone = 0.15f;

        private RectTransform zone;
        private int activePointerId = NoPointer;
        private bool hasOrigin;

        protected override string controlPathInternal
        {
            get => stickControlPath;
            set => stickControlPath = value;
        }

        public void Configure(RectTransform baseTransform, RectTransform knob, string path, float range, float dead)
        {
            stickBase = baseTransform;
            stickKnob = knob;
            stickControlPath = path;
            movementRange = range;
            deadZone = dead;
        }

        protected override void OnEnable()
        {
            zone = (RectTransform)transform;
            base.OnEnable();
            Release();
        }

        protected override void OnDisable()
        {
            // Zero the stick while the virtual device is still alive, otherwise the last direction sticks and
            // the player keeps walking after the HUD is hidden.
            Release();
            base.OnDisable();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (activePointerId != NoPointer) return; // one finger owns the stick; extra fingers are for buttons

            Grab(eventData, plantUnderFinger: true);
        }

        public void OnDrag(PointerEventData eventData)
        {
            // A finger already on screen when the stick loses its grip - the HUD was hidden, the component
            // was toggled, a scene finished loading - would otherwise send drags nobody listens to, and the
            // player would have to lift and press again before they could move. Adopt it instead.
            if (activePointerId == NoPointer) Grab(eventData, plantUnderFinger: false);

            if (eventData.pointerId != activePointerId) return;
            if (!TryGetLocalPoint(eventData, out Vector2 point)) return;

            Vector2 origin = stickBase ? stickBase.anchoredPosition : Vector2.zero;
            Vector2 offset = Vector2.ClampMagnitude(point - origin, movementRange);

            if (stickKnob) stickKnob.anchoredPosition = offset;

            Vector2 value = movementRange > 0f ? offset / movementRange : Vector2.zero;
            if (value.sqrMagnitude < deadZone * deadZone) value = Vector2.zero;

            SendValueToControl(value);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId) return;
            Release();
        }

        /// <summary>
        /// Takes ownership of a finger. A fresh press plants the stick under it; re-adopting a finger that
        /// never lifted keeps the origin it already had, so the player carries on walking the same way
        /// instead of having to nudge their thumb to get going again.
        /// </summary>
        private void Grab(PointerEventData eventData, bool plantUnderFinger)
        {
            if (!TryGetLocalPoint(eventData, out Vector2 point)) return;

            activePointerId = eventData.pointerId;

            if (stickBase)
            {
                if (plantUnderFinger || !hasOrigin) stickBase.anchoredPosition = ClampInsideZone(point);
                stickBase.gameObject.SetActive(true);
                hasOrigin = true;
            }
            if (stickKnob) stickKnob.anchoredPosition = Vector2.zero;

            SendValueToControl(Vector2.zero);
        }

        private void Release()
        {
            activePointerId = NoPointer;

            if (stickKnob) stickKnob.anchoredPosition = Vector2.zero;
            if (stickBase) stickBase.gameObject.SetActive(false);

            SendValueToControl(Vector2.zero);
        }

        private bool TryGetLocalPoint(PointerEventData eventData, out Vector2 point)
        {
            if (!zone) zone = (RectTransform)transform;

            return RectTransformUtility.ScreenPointToLocalPointInRectangle(
                zone, eventData.position, eventData.pressEventCamera, out point);
        }

        /// <summary>Keeps the stick's base fully on screen when a finger lands right against an edge.</summary>
        private Vector2 ClampInsideZone(Vector2 point)
        {
            if (!stickBase) return point;

            Rect zoneRect = zone.rect;
            Vector2 margin = stickBase.rect.size * 0.5f;

            // A zone narrower than the stick would invert the clamp range, so give up rather than snap to a corner.
            if (zoneRect.width <= stickBase.rect.width || zoneRect.height <= stickBase.rect.height) return point;

            return new Vector2(
                Mathf.Clamp(point.x, zoneRect.xMin + margin.x, zoneRect.xMax - margin.x),
                Mathf.Clamp(point.y, zoneRect.yMin + margin.y, zoneRect.yMax - margin.y));
        }
    }
}
