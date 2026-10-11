using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.Core.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.UI.Popup
{
    /// <summary>Adds the main menu's audio controls beside the existing pause panel.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class PauseMenuSettings : MonoBehaviour
    {
        [Header("Main menu artwork")]
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite headerSprite;
        [SerializeField] private Sprite masterIcon;
        [SerializeField] private Sprite musicIcon;
        [SerializeField] private Sprite sfxIcon;
        [SerializeField] private Sprite sliderTrack;
        [SerializeField] private Sprite sliderHandle;
        [SerializeField] private TMP_FontAsset font;

        private RectTransform contents;
        private Toggle cheatToggle;
        private Sprite checkSprite;
        private Texture2D checkTexture;

        private void Awake()
        {
            Build();
        }

        private void OnEnable()
        {
            cheatToggle.SetIsOnWithoutNotify(CheatSettings.Enabled);
            cheatToggle.onValueChanged.AddListener(SetCheat);
            CheatSettings.Changed += RefreshCheat;
            FitContents();
        }

        private void OnDisable()
        {
            cheatToggle.onValueChanged.RemoveListener(SetCheat);
            CheatSettings.Changed -= RefreshCheat;
        }

        private void OnDestroy()
        {
            if (checkSprite) Destroy(checkSprite);
            if (checkTexture) Destroy(checkTexture);
        }

        private void OnRectTransformDimensionsChange()
        {
            if (contents) FitContents();
        }

        private void Build()
        {
            var root = (RectTransform)transform;
            var originalChildren = new RectTransform[root.childCount];
            for (int i = 0; i < originalChildren.Length; i++)
                originalChildren[i] = (RectTransform)root.GetChild(i);

            // Keep the original panel, score and button layout together at their authored scale.
            contents = Rect("Pause Contents", root, Vector2.zero, root.sizeDelta);
            contents.localScale = root.localScale;
            foreach (var child in originalChildren)
            {
                if (child.name == "Panel")
                {
                    // The dimming overlay covers every phone aspect ratio.
                    child.localScale = Vector3.one;
                    child.anchorMin = Vector2.zero;
                    child.anchorMax = Vector2.one;
                    child.offsetMin = child.offsetMax = Vector2.zero;
                    continue;
                }

                child.SetParent(contents, false);
                child.anchoredPosition += new Vector2(-61f, 0f);
            }

            root.localScale = Vector3.one;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            var panel = Rect("Pause Settings", contents, new Vector2(61f, 0f), new Vector2(111f, 123f));
            Image(panel, panelSprite, Color.white, true);
            var header = Rect("Title", panel, new Vector2(0f, 58f), new Vector2(70f, 14f));
            var headerImage = Image(header, headerSprite, Color.white);
            headerImage.type = UnityEngine.UI.Image.Type.Sliced;
            headerImage.pixelsPerUnitMultiplier = 6.25f;
            var title = Label("Label", header, Vector2.zero, new Vector2(70f, 14f), "");
            title.gameObject.AddComponent<LocalizedText>().SetKey("menu.settings");

            BuildVolume(panel, "Master", EVolumeType.Master, masterIcon, 24f);
            BuildVolume(panel, "Music", EVolumeType.Music, musicIcon, 4f);
            BuildVolume(panel, "SFX", EVolumeType.SFX, sfxIcon, -16f);
            BuildCheat(panel);
            FitContents();
        }

        private void BuildVolume(RectTransform panel, string name, EVolumeType type, Sprite icon, float y)
        {
            var iconRect = Rect(name + " Icon", panel, new Vector2(-30.5f, y), new Vector2(15f, 15f));
            Image(iconRect, icon, Color.white);

            var sliderRect = Rect(name, panel, new Vector2(9.4f, y - 0.5f), new Vector2(55.89f, 16f));
            sliderRect.gameObject.SetActive(false);
            var background = Rect("Background", sliderRect, Vector2.zero, new Vector2(61f, 15f));
            Image(background, sliderTrack, Color.white, true);
            var fillArea = Rect("Fill Area", sliderRect, Vector2.zero, new Vector2(55.66f, 6f));
            var fill = Rect("Fill", fillArea, Vector2.zero, new Vector2(0f, 6f));
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.sizeDelta = Vector2.zero;
            Image(fill, null, new Color(0.9450981f, 0.30588236f, 0.32156864f));

            var handleArea = Rect("Handle Slide Area", sliderRect, Vector2.zero, new Vector2(56.7f, 13f));
            var handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(13f, 13f));
            var handleImage = Image(handle, sliderHandle, Color.white, true);
            var slider = sliderRect.gameObject.AddComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            sliderRect.gameObject.AddComponent<VolumeSlider>().Configure(type);
            sliderRect.gameObject.SetActive(true);
        }

        private void BuildCheat(RectTransform panel)
        {
            var row = Rect("cheat", panel, new Vector2(2f, -36f), new Vector2(90f, 18f));
            Image(row, null, Color.clear, true);
            var border = Rect("Checkbox", row, new Vector2(-32.5f, 0f), new Vector2(12f, 12f));
            var borderImage = Image(border, null, new Color(0.85f, 0.85f, 0.85f));
            var inside = Rect("Inside", border, Vector2.zero, new Vector2(8f, 8f));
            Image(inside, null, new Color(0.129f, 0.153f, 0.224f));
            var check = Rect("Checkmark", border, Vector2.zero, new Vector2(8f, 8f));
            checkSprite = BuildCheckmark();
            var checkImage = Image(check, checkSprite, Color.white);
            Label("Label", row, new Vector2(8f, 0f), new Vector2(60f, 18f), "cheat");

            cheatToggle = row.gameObject.AddComponent<Toggle>();
            cheatToggle.targetGraphic = borderImage;
            cheatToggle.graphic = checkImage;
            cheatToggle.toggleTransition = Toggle.ToggleTransition.None;
            cheatToggle.SetIsOnWithoutNotify(CheatSettings.Enabled);
        }

        private Sprite BuildCheckmark()
        {
            // Eight native pixels, kept sharp like the rest of the game's UI.
            string[] rows = { "........", "......##", ".....##.", "#...##..", "##.##...", ".###....", "..#.....", "........" };
            checkTexture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[64];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++)
                    pixels[(7 - y) * 8 + x] = rows[y][x] == '#' ? new Color32(149, 224, 204, 255) : new Color32();
            checkTexture.SetPixels32(pixels);
            checkTexture.Apply(false, true);
            return Sprite.Create(checkTexture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 16f);
        }

        private static void SetCheat(bool enabled) => CheatSettings.Enabled = enabled;
        private void RefreshCheat(bool enabled) => cheatToggle.SetIsOnWithoutNotify(enabled);

        private void FitContents()
        {
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0f || size.y <= 0f) return;
            float scale = Mathf.Min(2f, (size.x - 24f) / 233f, (size.y - 24f) / 130f);
            contents.localScale = Vector3.one * Mathf.Max(0.1f, scale);
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.gameObject.layer = parent.gameObject.layer;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static Image Image(RectTransform rect, Sprite sprite, Color color, bool raycast = false)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private TMP_Text Label(string name, Transform parent, Vector2 position, Vector2 size, string text)
        {
            var rect = Rect(name, parent, position, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 16f;
            label.text = text;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.raycastTarget = false;
            return label;
        }
    }
}
