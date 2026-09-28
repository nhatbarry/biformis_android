using CaptainPinkTurd.Core.Localization;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// Shows the level's name (and an optional one-line brief) across the screen for a moment when the level starts.
    /// </summary>
    public class LevelTitleCard : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        [Tooltip("Localization keys")]
        [SerializeField] private string titleKey;
        [SerializeField] private string subtitleKey;
        [SerializeField] private float startDelay = 0.8f;
        [SerializeField] private float holdSeconds = 2.4f;
        [SerializeField] private Color bandColor = new(0f, 0f, 0f, 0.6f);
        [SerializeField] private Color titleColor = Color.white;

        private void Start()
        {
            var canvas = OverlayCanvas.Create(transform, "Level Title Card", 40);
            var group = canvas.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            bool hasSubtitle = !string.IsNullOrEmpty(subtitleKey);
            var band = OverlayCanvas.CreateImage(canvas.transform, "Band", bandColor, false).rectTransform;
            band.anchorMin = new Vector2(0f, 0.5f);
            band.anchorMax = new Vector2(1f, 0.5f);
            band.sizeDelta = new Vector2(0f, hasSubtitle ? 64f : 44f);
            band.anchoredPosition = new Vector2(0f, 40f);

            CreateText(band, Localization.Get(titleKey), 40f, titleColor, hasSubtitle ? 10f : 0f);
            if (hasSubtitle) CreateText(band, Localization.Get(subtitleKey), 22f, new Color(1f, 1f, 1f, 0.8f), -16f);

            DOTween.Sequence()
                .AppendInterval(startDelay)
                .Append(group.DOFade(1f, 0.4f))
                .AppendInterval(holdSeconds)
                .Append(group.DOFade(0f, 0.6f))
                .OnKill(() => { if (canvas) Destroy(canvas.gameObject); })
                .SetLink(gameObject);
        }

        private void CreateText(RectTransform parent, string content, float size, Color color, float y)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.sizeDelta = new Vector2(0f, size);
            rect.anchoredPosition = new Vector2(0f, y);
        }
    }
}
