using UnityEngine;

namespace CaptainPinkTurd.MobileControls
{
    /// <summary>
    /// Draws the touch HUD artwork procedurally, so the mobile controls need no imported sprites and
    /// stay independent of the game's art pass. Every sprite is one small anti-aliased texture that is
    /// built once per session and uploaded straight to the GPU.
    /// </summary>
    internal static class MobileControlGraphics
    {
        private const int Size = 128;
        private const float Half = Size * 0.5f;
        private const float OuterRadius = Half - 1f;

        /// <summary>A filled circle with a ring around it. <paramref name="outlineFraction"/> is the ring's
        /// thickness as a fraction of the radius.</summary>
        public static Sprite Disc(Color fill, Color outline, float outlineFraction)
        {
            float inner = OuterRadius * (1f - Mathf.Clamp01(outlineFraction));
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = Shade(fill, outline, Distance(x, y), inner);
                }
            }

            return ToSprite(pixels);
        }

        /// <summary>A circle split down the middle into two colours - used for the dimension button, whose
        /// two halves mirror the game's two dimensions.</summary>
        public static Sprite SplitDisc(Color left, Color right, Color outline, float outlineFraction)
        {
            float inner = OuterRadius * (1f - Mathf.Clamp01(outlineFraction));
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // One pixel of blend across the seam keeps it from aliasing when the button is scaled up.
                    float seam = Mathf.Clamp01(x + 0.5f - Half + 0.5f);
                    pixels[y * Size + x] = Shade(Color.Lerp(left, right, seam), outline, Distance(x, y), inner);
                }
            }

            return ToSprite(pixels);
        }

        /// <summary>Two chevrons pointing right - the run/dash glyph.</summary>
        public static Sprite Chevrons(Color color)
        {
            // A ">" as a two-segment polyline in unit-square coordinates, drawn twice side by side.
            var chevron = new[]
            {
                new Vector2(0.26f, 0.76f),
                new Vector2(0.48f, 0.50f),
                new Vector2(0.26f, 0.24f),
            };
            const float secondChevronOffset = 0.26f;
            float halfStroke = Size * 0.055f;

            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = float.MaxValue;

                    for (int stroke = 0; stroke < 2; stroke++)
                    {
                        float offset = stroke * secondChevronOffset;
                        for (int i = 0; i < chevron.Length - 1; i++)
                        {
                            var a = new Vector2((chevron[i].x + offset) * Size, chevron[i].y * Size);
                            var b = new Vector2((chevron[i + 1].x + offset) * Size, chevron[i + 1].y * Size);
                            distance = Mathf.Min(distance, DistanceToSegment(p, a, b));
                        }
                    }

                    var c = color;
                    c.a *= Mathf.Clamp01(halfStroke - distance + 0.5f);
                    pixels[y * Size + x] = c;
                }
            }

            return ToSprite(pixels);
        }

        /// <summary>Two upright bars - the pause glyph.</summary>
        public static Sprite Bars(Color color)
        {
            var bars = new[]
            {
                new[] { new Vector2(0.37f, 0.26f), new Vector2(0.37f, 0.74f) },
                new[] { new Vector2(0.63f, 0.26f), new Vector2(0.63f, 0.74f) },
            };

            return Strokes(color, bars, Size * 0.075f);
        }

        /// <summary>A right-pointing triangle - the resume glyph.</summary>
        public static Sprite Triangle(Color color)
        {
            var vertices = new[]
            {
                new Vector2(0.36f, 0.22f) * Size,
                new Vector2(0.36f, 0.78f) * Size,
                new Vector2(0.76f, 0.50f) * Size,
            };

            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);

                    // Consistent edge-cross signs mean the point is inside; the nearest edge gives the
                    // one-pixel band that keeps the diagonal from stair-stepping.
                    bool inside = true;
                    float sign = 0f;
                    float distance = float.MaxValue;

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        var a = vertices[i];
                        var b = vertices[(i + 1) % vertices.Length];
                        var edge = b - a;
                        float cross = edge.x * (p.y - a.y) - edge.y * (p.x - a.x);

                        if (sign == 0f) sign = Mathf.Sign(cross);
                        else if (Mathf.Sign(cross) != sign) inside = false;

                        distance = Mathf.Min(distance, DistanceToSegment(p, a, b));
                    }

                    var c = color;
                    c.a *= inside
                        ? Mathf.Clamp01(distance + 0.5f)
                        : Mathf.Clamp01(0.5f - distance);
                    pixels[y * Size + x] = c;
                }
            }

            return ToSprite(pixels);
        }

        /// <summary>Draws round-capped strokes along polylines given in unit-square coordinates.</summary>
        private static Sprite Strokes(Color color, Vector2[][] polylines, float halfStroke)
        {
            var pixels = new Color32[Size * Size];

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = float.MaxValue;

                    foreach (var line in polylines)
                    {
                        for (int i = 0; i < line.Length - 1; i++)
                        {
                            distance = Mathf.Min(distance,
                                DistanceToSegment(p, line[i] * Size, line[i + 1] * Size));
                        }
                    }

                    var c = color;
                    c.a *= Mathf.Clamp01(halfStroke - distance + 0.5f);
                    pixels[y * Size + x] = c;
                }
            }

            return ToSprite(pixels);
        }

        private static float Distance(int x, int y)
        {
            float dx = x + 0.5f - Half;
            float dy = y + 0.5f - Half;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Blends fill, ring and transparency for one pixel at <paramref name="distance"/> from the
        /// centre. The half-pixel offsets are what give the edges their anti-aliasing.</summary>
        private static Color32 Shade(Color fill, Color outline, float distance, float innerRadius)
        {
            float coverage = Mathf.Clamp01(OuterRadius - distance + 0.5f);
            if (coverage <= 0f) return new Color32(0, 0, 0, 0);

            float insideRing = Mathf.Clamp01(innerRadius - distance + 0.5f);

            Color c = Color.Lerp(outline, fill, insideRing);
            c.a = Mathf.Lerp(outline.a, fill.a, insideRing) * coverage;
            return c;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            float t = lengthSquared > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSquared) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static Sprite ToSprite(Color32[] pixels)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = "Mobile Control Graphic",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true); // drop the CPU copy, the HUD never reads these back

            var sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            sprite.name = "Mobile Control Graphic";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
