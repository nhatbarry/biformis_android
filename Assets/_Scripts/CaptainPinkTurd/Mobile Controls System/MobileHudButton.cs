using UnityEngine;
using UnityEngine.EventSystems;

namespace CaptainPinkTurd.MobileControls
{
    /// <summary>
    /// Whether a finger is holding a touch HUD button, so the HUD can show the button's pressed art. Purely cosmetic -
    /// the input itself is sent by the sibling <c>OnScreenButton</c>, which receives the same pointer events.
    /// </summary>
    [DisallowMultipleComponent]
    public class MobileHudButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool IsPressed { get; private set; }

        //a finger held down when the HUD hides would otherwise leave the button stuck looking pressed
        private void OnDisable() => IsPressed = false;

        public void OnPointerDown(PointerEventData eventData) => IsPressed = true;

        public void OnPointerUp(PointerEventData eventData) => IsPressed = false;
    }
}
