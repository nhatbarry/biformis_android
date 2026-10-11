using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// Draws the original door leaf, bends and cracks it as the blows accumulate, then throws its textured pieces
    /// into the room. The entrance sequence supplies the impact envelope and elapsed destruction time.
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
    public sealed class DoorBreakGraphic : MaskableGraphic
    {
        private const int Columns = 5;
        private const int Rows = 6;
        private const float FadeStart = 1.3f;
        private const float FadeEnd = 1.8f;

        private static readonly Vector2[][] CrackPaths =
        {
            new[]
            {
                new Vector2(0.50f, 0.90f), new Vector2(0.50f, 0.76f),
                new Vector2(0.59f, 0.76f), new Vector2(0.59f, 0.64f),
                new Vector2(0.50f, 0.64f), new Vector2(0.50f, 0.48f),
                new Vector2(0.39f, 0.48f), new Vector2(0.39f, 0.34f),
                new Vector2(0.48f, 0.34f), new Vector2(0.48f, 0.12f)
            },
            new[]
            {
                new Vector2(0.50f, 0.64f), new Vector2(0.72f, 0.64f),
                new Vector2(0.72f, 0.56f), new Vector2(0.91f, 0.56f)
            },
            new[]
            {
                new Vector2(0.50f, 0.48f), new Vector2(0.24f, 0.48f),
                new Vector2(0.24f, 0.38f), new Vector2(0.10f, 0.38f)
            }
        };

        private Texture2D source;
        private float damage;
        private float impact;
        private float breakElapsed;
        private bool shattering;

        public override Texture mainTexture => source ? source : Texture2D.whiteTexture;

        /// <summary>The texture is a full, cropped door leaf; layout and tint are supplied by its parent.</summary>
        public void Configure(Texture2D texture)
        {
            source = texture;
            damage = impact = breakElapsed = 0f;
            shattering = false;
            raycastTarget = false;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        /// <summary>Accumulates damage while impact is the current blow's decaying, zero-to-one envelope.</summary>
        public void SetDamage(float damage, float impact)
        {
            this.damage = Mathf.Clamp01(damage);
            this.impact = Mathf.Clamp01(impact);
            shattering = false;
            SetVerticesDirty();
        }

        /// <summary>Draws a deterministic destruction frame. All pieces disappear at 1.8 seconds.</summary>
        public void Break(float elapsed)
        {
            breakElapsed = Mathf.Max(0f, elapsed);
            impact = 0f;
            shattering = true;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!source) return;

            Rect rect = rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            if (shattering)
            {
                DrawShards(mesh, rect);
                return;
            }

            if (damage <= 0f && impact <= 0f)
            {
                //An undamaged leaf uses exactly the original texture, without gaps, warping or extra shading.
                AddQuad(mesh, new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMin, rect.yMax),
                    new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMax, rect.yMin),
                    Vector2.zero, Vector2.up, Vector2.one, Vector2.right, color);
                return;
            }

            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    GetCell(column, row, out Vector2 bottomLeft, out Vector2 topLeft,
                        out Vector2 topRight, out Vector2 bottomRight);
                    AddQuad(mesh, Deform(bottomLeft, rect), Deform(topLeft, rect),
                        Deform(topRight, rect), Deform(bottomRight, rect),
                        bottomLeft, topLeft, topRight, bottomRight, color);
                }
            }
            DrawCracks(mesh, rect);
        }

        private Vector2 Deform(Vector2 uv, Rect rect)
        {
            Vector2 point = new(rect.xMin + rect.width * uv.x, rect.yMin + rect.height * uv.y);
            float middle = Mathf.Sin(uv.y * Mathf.PI);
            float bulge = rect.width * damage * 0.30f * middle * (0.35f + 0.65f * uv.x);
            float bend = rect.height * damage * 0.035f * middle * (uv.x - 0.5f);

            //Snap the deformation to source pixels, leaving the source's texture coordinates untouched.
            float pixelX = rect.width / source.width;
            float pixelY = rect.height / source.height;
            point.x += Mathf.Round(bulge / pixelX) * pixelX;
            point.y += Mathf.Round(bend / pixelY) * pixelY;

            float spring = Mathf.Sin((1f - impact) * Mathf.PI * 2.5f) * impact;
            point.x += rect.width * (spring * 0.035f + impact * 0.018f);
            point.y += rect.height * spring * 0.006f;
            return point;
        }

        private void DrawCracks(VertexHelper mesh, Rect rect)
        {
            Color seam = color * new Color(0.075f, 0.055f, 0.065f, Mathf.Lerp(0.45f, 0.90f, damage));
            float thickness = Mathf.Min(rect.width / source.width, rect.height / source.height);
            for (int pathIndex = 0; pathIndex < CrackPaths.Length; pathIndex++)
            {
                float threshold = pathIndex == 0 ? 0.10f : pathIndex == 1 ? 0.42f : 0.72f;
                if (damage <= threshold) continue;

                Vector2[] path = CrackPaths[pathIndex];
                float length = Mathf.InverseLerp(threshold, 1f, damage) * (path.Length - 1);
                for (int step = 0; step < path.Length - 1 && step < length; step++)
                {
                    Vector2 end = Vector2.Lerp(path[step], path[step + 1], Mathf.Clamp01(length - step));
                    Vector2 from = Deform(SnapUv(path[step]), rect);
                    Vector2 to = Deform(SnapUv(end), rect);
                    Vector2 direction = to - from;
                    if (direction.sqrMagnitude <= 0.001f) continue;
                    Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness * 0.5f);
                    //The leaf's opaque centre supplies alpha for the dark seam in this same textured UI mesh.
                    Vector2 opaque = new(0.5f, 0.5f);
                    AddQuad(mesh, from - normal, from + normal, to + normal, to - normal,
                        opaque, opaque, opaque, opaque, seam);
                }
            }
        }

        private Vector2 SnapUv(Vector2 uv) => new(
            Mathf.Round(uv.x * source.width) / source.width,
            Mathf.Round(uv.y * source.height) / source.height);

        private void DrawShards(VertexHelper mesh, Rect rect)
        {
            if (breakElapsed >= FadeEnd) return;
            Color tint = color;
            tint.a *= 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FadeStart, FadeEnd, breakElapsed));

            //A small initial separation makes the leaf visibly broken even on the first destruction frame.
            float flight = breakElapsed + 0.025f;
            for (int row = 0; row < Rows; row++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int piece = row * Columns + column;
                    GetCell(column, row, out Vector2 bottomLeft, out Vector2 topLeft,
                        out Vector2 topRight, out Vector2 bottomRight);
                    Vector2 uvCentre = (bottomLeft + topRight) * 0.5f;
                    Vector2 centre = Deform(uvCentre, rect);
                    float velocityX = rect.width * Mathf.Lerp(0.75f, 3.05f, Noise(piece, 1));
                    float velocityY = rect.height * (0.12f + (uvCentre.y - 0.5f) * 0.85f + Noise(piece, 2) * 0.18f);
                    Vector2 translation = new(
                        velocityX * flight + (uvCentre.x - 0.5f) * rect.width * 0.04f,
                        velocityY * flight - rect.height * 1.9f * flight * flight +
                        (uvCentre.y - 0.5f) * rect.height * 0.04f);
                    float rotation = (Noise(piece, 3) * 2f - 1f) * Mathf.Lerp(95f, 315f, Noise(piece, 4)) * flight;
                    float radians = rotation * Mathf.Deg2Rad;
                    float sine = Mathf.Sin(radians);
                    float cosine = Mathf.Cos(radians);

                    AddQuad(mesh,
                        ShardPoint(Deform(bottomLeft, rect), centre, translation, sine, cosine),
                        ShardPoint(Deform(topLeft, rect), centre, translation, sine, cosine),
                        ShardPoint(Deform(topRight, rect), centre, translation, sine, cosine),
                        ShardPoint(Deform(bottomRight, rect), centre, translation, sine, cosine),
                        bottomLeft, topLeft, topRight, bottomRight, tint);
                }
            }
        }

        private static Vector2 ShardPoint(Vector2 point, Vector2 centre, Vector2 translation, float sine, float cosine)
        {
            Vector2 offset = (point - centre) * 0.96f;
            return centre + translation + new Vector2(offset.x * cosine - offset.y * sine,
                offset.x * sine + offset.y * cosine);
        }

        private static float Noise(int piece, int channel)
        {
            //Stable across devices and calls; no dependency on Unity's global random state.
            unchecked
            {
                uint value = (uint)(piece + 1) * 747796405u + (uint)channel * 2891336453u;
                value = (value ^ (value >> 16)) * 2246822519u;
                value ^= value >> 13;
                return (value & 0x00ffffffu) / 16777215f;
            }
        }

        private static void GetCell(int column, int row, out Vector2 bottomLeft, out Vector2 topLeft,
            out Vector2 topRight, out Vector2 bottomRight)
        {
            float left = (float)column / Columns;
            float right = (float)(column + 1) / Columns;
            float bottom = (float)row / Rows;
            float top = (float)(row + 1) / Rows;
            bottomLeft = new Vector2(left, bottom);
            topLeft = new Vector2(left, top);
            topRight = new Vector2(right, top);
            bottomRight = new Vector2(right, bottom);
        }

        private static void AddQuad(VertexHelper mesh, Vector2 bottomLeft, Vector2 topLeft, Vector2 topRight,
            Vector2 bottomRight, Vector2 uvBottomLeft, Vector2 uvTopLeft, Vector2 uvTopRight, Vector2 uvBottomRight,
            Color tint)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(bottomLeft, tint, uvBottomLeft);
            mesh.AddVert(topLeft, tint, uvTopLeft);
            mesh.AddVert(topRight, tint, uvTopRight);
            mesh.AddVert(bottomRight, tint, uvBottomRight);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
