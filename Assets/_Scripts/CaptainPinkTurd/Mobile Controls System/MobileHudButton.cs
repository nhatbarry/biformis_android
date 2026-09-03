using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CaptainPinkTurd.MobileControls
{
    /// <summary>
    /// Press feedback for a touch HUD button. Purely cosmetic - the input itself is sent by the sibling
    /// <c>OnScreenButton</c>, which receives the same pointer events.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileHudButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Graphic[] graphics;
        [SerializeField] private float restAlpha = 0.55f;
        [SerializeField] private float pressedAlpha = 1f;
        [SerializeField] private float pressedScale = 0.9f;
        [SerializeField] private float responseSpeed = 18f;

        private RectTransform rect;
        private bool isPressed;
        private float blend;

        public void Configure(Graphic[] targets, float rest, float pressed)
        {
            graphics = targets;
            restAlpha = rest;
            pressedAlpha = pressed;
        }

        private void Awake()
        {
            rect = (RectTransform)transform;
            ApplyBlend();
        }

        private void OnDisable()
        {
            // A finger held down when the HUD hides would otherwise leave the button stuck looking pressed.
            isPressed = false;
            blend = 0f;
            ApplyBlend();
        }

        private void Update()
        {
            float target = isPressed ? 1f : 0f;
            if (Mathf.Approximately(blend, target)) return;

            // Unscaled: the game freezes time on game over, and a button that stops animating reads as broken.
            blend = Mathf.MoveTowards(blend, target, responseSpeed * Time.unscaledDeltaTime);
            ApplyBlend();
        }

        public void OnPointerDown(PointerEventData eventData) => isPressed = true;

        public void OnPointerUp(PointerEventData eventData) => isPressed = false;

        private void ApplyBlend()
        {
            float alpha = Mathf.Lerp(restAlpha, pressedAlpha, blend);
            float scale = Mathf.Lerp(1f, pressedScale, blend);

            if (graphics != null)
            {
                foreach (var graphic in graphics)
                {
                    if (!graphic) continue;

                    Color color = graphic.color;
                    color.a = alpha;
                    graphic.color = color;
                }
            }

            if (rect) rect.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
