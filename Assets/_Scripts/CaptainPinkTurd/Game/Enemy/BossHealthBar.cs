using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>Only the current phase's seven/eight points appear above the boss.</summary>
    public class BossHealthBar : MonoBehaviour
    {
        [SerializeField] private TMP_FontAsset font;
        private PlagueDoctorBoss boss;
        private RectTransform bar;
        private TMP_Text label;
        private readonly Image[] points = new Image[8];

        public string DisplayedHealth => label ? label.text : string.Empty;

        private void Awake()
        {
            boss = GetComponent<PlagueDoctorBoss>();
            var go = new GameObject("Boss Health", typeof(RectTransform), typeof(Canvas));
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            go.transform.SetParent(transform, false);
            bar = go.GetComponent<RectTransform>();
            bar.sizeDelta = new Vector2(240f, 50f);
            bar.localScale = Vector3.one * 0.01f;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "Default";
            canvas.sortingOrder = 30;
            var text = new GameObject("Phase Health", typeof(RectTransform), typeof(TextMeshProUGUI));
            text.transform.SetParent(bar, false);
            var rect = text.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(240f, 32f);
            rect.anchoredPosition = new Vector2(0f, 16f);
            label = text.GetComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSize = 42f;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            for (int i = 0; i < points.Length; i++)
            {
                var point = new GameObject("HP " + (i + 1), typeof(RectTransform), typeof(Image));
                point.transform.SetParent(bar, false);
                var pointRect = point.GetComponent<RectTransform>();
                pointRect.sizeDelta = new Vector2(22f, 10f);
                points[i] = point.GetComponent<Image>();
                points[i].raycastTarget = false;
            }
        }

        private void LateUpdate()
        {
            // Sprite bounds include the sheets' transparent canvas; use the actual hood/head height plus its rise.
            bar.position = transform.position + Vector3.up * ((boss.IsFrozen ? 3.45f : 2.85f) + boss.VisualLift);
            int maximum = boss.PhaseMaximumHealth;
            int health = boss.PhaseHealth;
            label.text = $"{health}/{maximum}";
            label.color = boss.IsFrozen ? new Color(0.5f, 0.85f, 1f) : Color.white;
            for (int i = 0; i < points.Length; i++)
            {
                points[i].gameObject.SetActive(i < maximum);
                points[i].rectTransform.anchoredPosition = new Vector2((i - (maximum - 1) * 0.5f) * 27f, -7f);
                points[i].color = i < health ? new Color(0.95f, 0.3f, 0.36f) : new Color(0.16f, 0.19f, 0.25f);
            }
        }
    }
}
