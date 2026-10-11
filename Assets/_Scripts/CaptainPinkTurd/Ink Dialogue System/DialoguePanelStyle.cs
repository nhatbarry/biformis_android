using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.InkDialogue
{
    /// <summary>One presentation for every Ink line, including the finale and hospital.</summary>
    public static class DialoguePanelStyle
    {
        public const float SideMargin = 16f;
        public const float BottomMargin = 12f;
        public const float TopMargin = 12f;
        public const float PanelHeight = 108f;
        public const float TextPadding = 16f;
        public const string ContinueMarker = ">";

        public static readonly Color PanelColor = new(0.12941177f, 0.15294118f, 0.22352941f, 1f);
        public static readonly Color AccentColor = new(0.58431375f, 0.8784314f, 0.8f, 1f);
        public static readonly Color TextColor = new(0.85490197f, 0.9490196f, 0.9137255f, 1f);

        public static void Apply(RectTransform panel, RectTransform content, TMP_Text speaker, TMP_Text line,
            RectTransform continueIndicator)
        {
            SetBottomLayout(panel);
            Stretch(content, 0f);

            var border = GetOrCreateImage(content, "Dialogue Border");
            StyleImage(border, AccentColor, 0f);
            border.transform.SetAsFirstSibling();

            var background = GetOrCreateImage(content, "Back Panel");
            StyleImage(background, PanelColor, 2f);
            background.transform.SetSiblingIndex(1);

            // All scenes use the project's pixel font with its Vietnamese character set.
            var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/m3x6 VI SDF");
            if (!font && line) font = line.font;

            if (speaker)
            {
                StyleText(speaker, font, 26f, AccentColor);
                PlaceText(speaker.rectTransform, 8f, 24f, TextPadding);
                speaker.enableAutoSizing = false;
                speaker.textWrappingMode = TextWrappingModes.NoWrap;
                speaker.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (line)
            {
                StyleText(line, font, 30f, TextColor);
                PlaceText(line.rectTransform, 34f, 62f, 34f);
                line.enableAutoSizing = true;
                line.fontSizeMin = 22f;
                line.fontSizeMax = 30f;
                line.textWrappingMode = TextWrappingModes.Normal;
                line.lineSpacing = -4f;
                line.overflowMode = TextOverflowModes.Truncate;
            }

            if (continueIndicator) StyleContinue(continueIndicator, font);
        }

        public static void SetLayout(RectTransform panel, string layout)
        {
            if (layout == "top") SetTopLayout(panel);
            else SetBottomLayout(panel);
        }

        public static void SetBottomLayout(RectTransform panel)
        {
            PlaceAtEdge(panel, false);
        }

        /// <summary>The finale can opt into the upper edge without changing its dialogue style.</summary>
        public static void SetTopLayout(RectTransform panel)
        {
            PlaceAtEdge(panel, true);
        }

        private static void PlaceAtEdge(RectTransform panel, bool top)
        {
            if (!panel) return;
            var canvas = panel.GetComponentInParent<Canvas>();
            float scale = canvas ? Mathf.Max(canvas.scaleFactor, 0.001f) : 1f;
            var safe = Screen.safeArea;
            float safeLeft = Screen.width > 0 ? safe.xMin / scale : 0f;
            float safeRight = Screen.width > 0 ? (Screen.width - safe.xMax) / scale : 0f;
            float safeBottom = Screen.height > 0 ? safe.yMin / scale : 0f;
            float safeTop = Screen.height > 0 ? (Screen.height - safe.yMax) / scale : 0f;

            var anchorMin = new Vector2(0f, top ? 1f : 0f);
            var anchorMax = new Vector2(1f, anchorMin.y);
            var pivot = new Vector2(0.5f, anchorMin.y);
            float edge = top ? -TopMargin - safeTop : BottomMargin + safeBottom;
            var offsetMin = new Vector2(SideMargin + safeLeft, top ? edge - PanelHeight : edge);
            var offsetMax = new Vector2(-SideMargin - safeRight, top ? edge : edge + PanelHeight);
            // Avoid dirtying the mobile canvas each frame when its geometry has not changed.
            if (panel.anchorMin != anchorMin) panel.anchorMin = anchorMin;
            if (panel.anchorMax != anchorMax) panel.anchorMax = anchorMax;
            if (panel.pivot != pivot) panel.pivot = pivot;
            if (panel.offsetMin != offsetMin) panel.offsetMin = offsetMin;
            if (panel.offsetMax != offsetMax) panel.offsetMax = offsetMax;
        }

        public static Color SpeakerColor(string speaker)
        {
            return speaker switch
            {
                "B" or "Red" => new Color(0.94509804f, 0.30588236f, 0.32156864f, 1f),
                "TeenB" => new Color(0.9647059f, 0.627451f, 0.6117647f, 1f),
                "Pink" or "CaptainPinkTurd" or "Captain Pink Turd" => new Color(1f, 0.62f, 0.78f, 1f),
                "Rabbit" or "Usagi" => new Color(0.95f, 0.84f, 0.94f, 1f),
                "Reaper" or "AB" => new Color(0.84313726f, 0.72156864f, 0.9529412f, 1f),
                "Villain" => new Color(0.78431374f, 0.627451f, 0.47843137f, 1f),
                "Mom" => new Color(0.9490196f, 0.76862746f, 0.3372549f, 1f),
                _ => AccentColor
            };
        }

        private static Image GetOrCreateImage(Transform parent, string name)
        {
            var child = parent.Find(name);
            if (child && child.TryGetComponent<Image>(out var image)) return image;
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            return go.GetComponent<Image>();
        }

        private static void StyleImage(Image image, Color color, float inset)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            Stretch(image.rectTransform, inset);
        }

        private static void Stretch(RectTransform rect, float inset)
        {
            if (!rect) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = -Vector2.one * inset;
        }

        private static void PlaceText(RectTransform rect, float top, float height, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(TextPadding, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void StyleText(TMP_Text text, TMP_FontAsset font, float size, Color color)
        {
            if (font) text.font = font;
            text.fontSize = size;
            text.fontStyle = FontStyles.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = color;
            text.margin = Vector4.zero;
            text.characterSpacing = 0f;
            text.wordSpacing = 0f;
            text.paragraphSpacing = 0f;
            text.raycastTarget = false;
        }

        private static void StyleContinue(RectTransform indicator, TMP_FontAsset font)
        {
            // Keep the existing GameObject: the typewriter still shows/hides it at the right time.
            if (indicator.TryGetComponent<Image>(out var image)) image.enabled = false;
            var markerObject = indicator.Find("Continue Marker");
            var marker = markerObject ? markerObject.GetComponent<TMP_Text>() : null;
            if (!marker)
            {
                var go = new GameObject("Continue Marker", typeof(RectTransform));
                go.transform.SetParent(indicator, false);
                marker = go.AddComponent<TextMeshProUGUI>();
            }

            indicator.anchorMin = Vector2.right;
            indicator.anchorMax = Vector2.right;
            indicator.pivot = new Vector2(1f, 0f);
            indicator.anchoredPosition = new Vector2(-10f, 8f);
            indicator.sizeDelta = new Vector2(16f, 16f);
            StyleText(marker, font, 28f, AccentColor);
            marker.alignment = TextAlignmentOptions.Center;
            marker.text = ContinueMarker;
            Stretch(marker.rectTransform, 0f);
            marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        }
    }
}
