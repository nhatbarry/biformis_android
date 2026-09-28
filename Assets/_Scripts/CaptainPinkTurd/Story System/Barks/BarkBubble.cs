using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Extensions;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CaptainPinkTurd.Story.Barks
{
    /// <summary>
    /// Floating line of text above a character that doesn't pause the game. Builds its own TextMeshPro at runtime.
    /// </summary>
    public class BarkBubble : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private float fontSize = 4f;
        [SerializeField] private float heightAboveTarget = 1.4f;
        [SerializeField] private float showSeconds = 2.6f;
        [SerializeField] private float fadeSeconds = 0.3f;
        [Tooltip("Topmost sorting layer, also kept out of the shockwave's screen capture")]
        [SerializeField] private string sortingLayerName = "CameraSortingLayer";
        [SerializeField] private int sortingOrder = 100;
        [SerializeField] private Color defaultColor = Color.white;
        [SerializeField] private SerializeKeyValuePair<string, Color>[] speakerColors =
        {
            new() { Key = "B", Value = new Color(1f, 0.45f, 0.45f) },
            new() { Key = "A", Value = new Color(0.6f, 0.95f, 0.85f) },
        };

        private TextMeshPro text;
        private Transform target;
        private Sequence sequence;

        public bool IsShowing => sequence != null && sequence.IsActive();

        private void Awake()
        {
            var go = new GameObject("Bark Text");
            go.transform.SetParent(transform, false);
            text = go.AddComponent<TextMeshPro>();
            if (font) text.font = font;
            text.fontSize = fontSize;
            text.alignment = TextAlignmentOptions.Bottom;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.outlineWidth = 0.25f;
            text.outlineColor = Color.black;
            text.sortingLayerID = SortingLayer.NameToID(sortingLayerName);
            text.sortingOrder = sortingOrder;
            text.rectTransform.sizeDelta = new Vector2(12f, 2f);
            text.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (target) transform.position = target.position + Vector3.up * heightAboveTarget;
        }

        private void OnDestroy()
        {
            sequence?.Kill();
        }

        public void Show(Transform follow, string speaker, string line)
        {
            target = follow;
            LateUpdate();

            text.text = line;
            text.color = speaker != null && speakerColors.TryGetValue(speaker, out Color color) ? color : defaultColor;
            text.alpha = 0f;

            sequence?.Kill();
            sequence = DOTween.Sequence()
                .Append(text.DOFade(1f, fadeSeconds))
                .AppendInterval(showSeconds)
                .Append(text.DOFade(0f, fadeSeconds));
        }
    }
}
