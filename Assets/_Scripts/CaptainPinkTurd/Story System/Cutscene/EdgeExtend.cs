using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// A full-screen picture drawn for 16:9 (160 x 90 art pixels) leaves empty bands on wider phones. This stretches
    /// the picture's outermost pixel column out to each side, which suits pictures whose edges are plain walls and
    /// floors (the opening's cage room and its spotlight overlay), so 20:9 and wider screens are filled.
    /// A picture with patterned edges (tiles, panels) gets ready-made bands instead (Left Band / Right Band), drawn at
    /// the picture's pixel size next to each side.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class EdgeExtend : MonoBehaviour
    {
        [Tooltip("How far each band reaches past the picture, in canvas units")]
        [SerializeField] private float reach = 400f;
        [Tooltip("Optional: drawn left of the picture instead of stretching its edge column (same height in pixels)")]
        [SerializeField] private Sprite leftBand;
        [Tooltip("Optional: drawn right of the picture instead of stretching its edge column (same height in pixels)")]
        [SerializeField] private Sprite rightBand;

        private Image image;
        private Image left, right;
        private Sprite builtFor;

        private void OnEnable()
        {
            image = GetComponent<Image>();
            Refresh();
        }

        private void LateUpdate() => Refresh();

        private void Refresh()
        {
            var sprite = image.sprite;
            if (sprite == builtFor) return;
            builtFor = sprite;

            left ??= CreateBand("Edge Left", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            right ??= CreateBand("Edge Right", new Vector2(1f, 0.5f), new Vector2(0f, 0.5f));
            left.enabled = right.enabled = sprite;
            if (!sprite) return;

            var r = sprite.textureRect;
            SetBand(left, leftBand, () => Sprite.Create(sprite.texture, new Rect(r.x, r.y, 1f, r.height), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit));
            SetBand(right, rightBand, () => Sprite.Create(sprite.texture, new Rect(r.xMax - 1f, r.y, 1f, r.height), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit));
        }

        private void SetBand(Image band, Sprite ready, System.Func<Sprite> edgeColumn)
        {
            if (!ready)
            {
                band.sprite = edgeColumn();
                band.rectTransform.sizeDelta = new Vector2(reach, 0f);
                return;
            }
            //a ready-made band keeps the picture's pixel size: as wide as it is drawn, scaled like the picture's height
            band.sprite = ready;
            float height = ((RectTransform)transform).rect.height;
            band.rectTransform.sizeDelta = new Vector2(ready.rect.width * height / ready.rect.height, 0f);
        }

        //a band of the picture's own height, glued to one side of it and reaching outwards
        private Image CreateBand(string bandName, Vector2 anchor, Vector2 pivot)
        {
            var go = new GameObject(bandName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.hideFlags = HideFlags.DontSave;
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(anchor.x, 0f);
            rect.anchorMax = new Vector2(anchor.x, 1f);
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(reach, 0f);

            var band = go.GetComponent<Image>();
            band.raycastTarget = false;
            band.color = image.color;
            return band;
        }
    }
}
