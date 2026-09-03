using UnityEngine;

namespace CaptainPinkTurd.MobileControls
{
    /// <summary>
    /// Keeps a RectTransform inside <see cref="Screen.safeArea"/>. The project builds with
    /// <c>androidRenderOutsideSafeArea</c> on, so without this the joystick and buttons can end up under a
    /// notch or the gesture bar.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform rect;
        private Rect appliedSafeArea;
        private Vector2Int appliedScreenSize;

        private void Awake()
        {
            rect = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea == appliedSafeArea &&
                Screen.width == appliedScreenSize.x &&
                Screen.height == appliedScreenSize.y)
            {
                return;
            }

            Apply();
        }

        private void Apply()
        {
            var screenSize = new Vector2Int(Screen.width, Screen.height);
            if (screenSize.x <= 0 || screenSize.y <= 0) return;

            appliedSafeArea = Screen.safeArea;
            appliedScreenSize = screenSize;

            Vector2 min = appliedSafeArea.position;
            Vector2 max = appliedSafeArea.position + appliedSafeArea.size;

            min.x /= screenSize.x;
            min.y /= screenSize.y;
            max.x /= screenSize.x;
            max.y /= screenSize.y;

            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
