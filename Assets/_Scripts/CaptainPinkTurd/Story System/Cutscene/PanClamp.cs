using UnityEngine;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// For a stage picture wider than the screen that the cutscene slides sideways (a camera pan, e.g. the dungeons,
    /// 320 px wide): keeps it covering the whole screen, so a phone wider than 16:9 never sees past either end.
    /// At 16:9 the pan's own range already fits; on wider screens its ends are pulled in a little.
    /// The picture is centred on the screen when its x is 0 (its parents are centred containers).
    /// An end with its own edge band (EdgeExtend) can be left free instead, so the framing there stays as drawn for 16:9
    /// (the dungeons' door end: the dialogue portrait must not cover the villain at the door).
    /// </summary>
    [DefaultExecutionOrder(1000)] //after the tweens that slide it this frame
    [RequireComponent(typeof(RectTransform))]
    public class PanClamp : MonoBehaviour
    {
        [Tooltip("Never show past the picture's left end (off when an edge band continues it)")]
        [SerializeField] private bool keepLeftEnd = true;
        [Tooltip("Never show past the picture's right end (off when an edge band continues it)")]
        [SerializeField] private bool keepRightEnd = true;

        private RectTransform rect;
        private RectTransform screen;

        private void Awake()
        {
            rect = (RectTransform)transform;
            var canvas = GetComponentInParent<Canvas>();
            screen = canvas ? (RectTransform)canvas.rootCanvas.transform : (RectTransform)rect.parent;
        }

        private void LateUpdate()
        {
            float slack = Mathf.Max(0f, (rect.rect.width * Mathf.Abs(rect.localScale.x) - screen.rect.width) / 2f);
            var position = rect.anchoredPosition;
            //a positive x slides the picture right, showing its left end
            if (keepLeftEnd) position.x = Mathf.Min(position.x, slack);
            if (keepRightEnd) position.x = Mathf.Max(position.x, -slack);
            rect.anchoredPosition = position;
        }
    }
}
