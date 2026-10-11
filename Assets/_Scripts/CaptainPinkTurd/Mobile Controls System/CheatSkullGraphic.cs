using CaptainPinkTurd.Core.Enum;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.MobileControls
{
    /// <summary>A pixel skull button drawn as UI geometry, using the HUD's whole art pixels.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class CheatSkullGraphic : MaskableGraphic
    {
        private static readonly string[] Skull =
        {
            "  ########  ",
            " ########## ",
            "############",
            "############",
            "##...##...##",
            "##...##...##",
            "###..##..###",
            " ####..#### ",
            "  ########  ",
            "  # #  # #  ",
            "  ########  ",
            "   ######   "
        };

        private EColor theme = EColor.Red;
        private bool pressed;
        private float opacity = 0.75f;

        public void Show(EColor nextTheme, bool isPressed, float alpha)
        {
            if (theme == nextTheme && pressed == isPressed && Mathf.Approximately(opacity, alpha)) return;
            theme = nextTheme;
            pressed = isPressed;
            opacity = alpha;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            float unit = Mathf.Min(rect.width, rect.height) / 24f;
            var origin = rect.center - Vector2.one * (12f * unit);
            var accent = theme == EColor.Blue
                ? new Color32(149, 224, 204, 255)
                : new Color32(241, 78, 82, 255);
            Color32 edge = new Color32(27, 29, 44, 255);
            Color32 fill = pressed ? accent : new Color32(33, 39, 57, 255);
            Color32 skull = pressed ? edge : new Color32(235, 249, 239, 255);

            for (int y = 1; y < 23; y++)
            {
                for (int x = 1; x < 23; x++)
                {
                    if (!InsideDisc(x, y)) continue;
                    bool border = !InsideDisc(x - 1, y) || !InsideDisc(x + 1, y) ||
                                  !InsideDisc(x, y - 1) || !InsideDisc(x, y + 1);
                    AddPixel(vh, origin, unit, x, y, border ? accent : fill);
                }
            }

            // Keep the skull centered, with a one-pixel press depression.
            int drop = pressed ? 1 : 0;
            for (int row = 0; row < Skull.Length; row++)
            {
                for (int x = 0; x < Skull[row].Length; x++)
                {
                    if (Skull[row][x] == '#') AddPixel(vh, origin, unit, x + 6, 17 - row - drop, skull);
                }
            }
        }

        private static bool InsideDisc(int x, int y)
        {
            if (y < 1 || y > 22) return false;
            int inset = y <= 2 || y >= 21 ? 7 : y <= 4 || y >= 19 ? 4 : y <= 6 || y >= 17 ? 2 : 1;
            return x >= inset && x < 24 - inset;
        }

        private void AddPixel(VertexHelper vh, Vector2 origin, float unit, int x, int y, Color32 tint)
        {
            tint.a = (byte)Mathf.RoundToInt(opacity * 255f);
            int start = vh.currentVertCount;
            var min = origin + new Vector2(x, y) * unit;
            vh.AddVert(new Vector3(min.x, min.y), tint, Vector2.zero);
            vh.AddVert(new Vector3(min.x, min.y + unit), tint, Vector2.zero);
            vh.AddVert(new Vector3(min.x + unit, min.y + unit), tint, Vector2.zero);
            vh.AddVert(new Vector3(min.x + unit, min.y), tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
