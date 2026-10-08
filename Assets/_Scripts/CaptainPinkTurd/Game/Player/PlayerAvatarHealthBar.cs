using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.UnitSystem;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaptainPinkTurd.Game.Player
{
    /// <summary>Always-visible ten-point health next to the existing portrait, in the portrait canvas.</summary>
    [RequireComponent(typeof(PlayerAvatar))]
    public class PlayerAvatarHealthBar : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        private UnitHealth health;
        private RectTransform root;
        private Image[] points;
        private TMP_Text label;
        private EColor form;
        public string DisplayedHealth => label ? label.text : string.Empty;

        private void Awake()
        {
            var portrait = (RectTransform)transform;
            root = (RectTransform)new GameObject("Player Health Bar", typeof(RectTransform)).transform;
            // A sibling: PlayerAvatar toggles its own children when changing form.
            root.SetParent(portrait.parent, false);
            root.anchorMin = root.anchorMax = new Vector2(0f, 1f);
            root.pivot = new Vector2(0f, 0.5f);
            root.anchoredPosition = portrait.anchoredPosition + Vector2.right * (portrait.rect.width * portrait.localScale.x / 2f + 14f);
            root.sizeDelta = new Vector2(174f, 20f);
            var back = CreateImage("Health Background", root, new Vector2(62f, 0f), new Vector2(124f, 18f));
            back.color = new Color(0.08f, 0.15f, 0.2f, 0.95f);
            points = new Image[10];
            for (int i = 0; i < points.Length; i++)
                points[i] = CreateImage("Health " + (i + 1), root, new Vector2(8f + i * 12f, 0f), new Vector2(10f, 12f));
            var text = new GameObject("Health Count", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(root, false);
            var rect = text.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(151f, 0f);
            rect.sizeDelta = new Vector2(48f, 24f);
            label = text.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 20f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            FindPlayer();
        }
        private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => FindPlayer();
        private void FindPlayer()
        {
            var player = FindAnyObjectByType<PlayerUnit>();
            health = player ? player.GetComponent<UnitHealth>() : null;
        }

        public void OnPlayerColorChangeEvent(EColor color) => form = color;

        private void LateUpdate()
        {
            if (!health) FindPlayer();
            root.gameObject.SetActive(health);
            if (!health) return;
            label.text = $"{health.CurrentHealth}/{health.MaxHealth}";
            Color filled = form == EColor.Red ? new Color(1f, 0.35f, 0.4f) : new Color(0.57f, 0.86f, 0.82f);
            for (int i = 0; i < points.Length; i++)
            {
                points[i].gameObject.SetActive(i < health.MaxHealth);
                points[i].color = i < health.CurrentHealth ? filled : new Color(0.18f, 0.24f, 0.28f);
            }
        }

        private static Image CreateImage(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private void OnDestroy() { if (root) Destroy(root.gameObject); }
    }
}
