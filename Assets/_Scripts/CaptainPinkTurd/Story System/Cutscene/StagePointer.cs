using UnityEngine;
using UnityEngine.EventSystems;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// On the stage's full-screen tap target: remembers whether a finger (or the mouse) is held on the stage and where,
    /// so a "reach" can walk a character towards it on a touch screen. Taps still go to the tap target's other handler.
    /// </summary>
    public class StagePointer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private int pointerId = int.MinValue;

        public bool Held { get; private set; }
        public Vector2 ScreenPosition { get; private set; }
        public Camera EventCamera { get; private set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Held) return; //a second finger doesn't take over
            Held = true;
            pointerId = eventData.pointerId;
            ScreenPosition = eventData.position;
            EventCamera = eventData.pressEventCamera;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Held && eventData.pointerId == pointerId) ScreenPosition = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) Held = false;
        }

        private void OnDisable() => Held = false;
    }
}
