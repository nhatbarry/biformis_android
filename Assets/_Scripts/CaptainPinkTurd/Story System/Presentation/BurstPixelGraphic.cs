using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// Draws an exploding background as one UI mesh instead of thousands of Image objects.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BurstPixelGraphic : MaskableGraphic
    {
        private struct Pixel
        {
            public Vector2 origin;
            public Vector2 velocity;
            public Vector2 size;
            public float delay;
            public float duration;
            public float uMin;
            public float uMax;
            public float vMin;
            public float vMax;
        }

        private readonly List<Pixel> pixels = new();
        private Texture2D sourceTexture;
        private float born;
        private float lifetime;

        public override Texture mainTexture => sourceTexture ? sourceTexture : s_WhiteTexture;

        public void Initialize(Texture2D texture, int sourcePixelSize, float scale)
        {
            sourceTexture = texture;
            raycastTarget = false;
            pixels.Clear();
            born = Time.unscaledTime;
            lifetime = 0f;

            Vector2 screenCenter = new Vector2(texture.width * scale * 0.5f, texture.height * scale * 0.5f);
            for (int y = 0; y < texture.height; y += sourcePixelSize)
            {
                for (int x = 0; x < texture.width; x += sourcePixelSize)
                {
                    int width = Mathf.Min(sourcePixelSize, texture.width - x);
                    int height = Mathf.Min(sourcePixelSize, texture.height - y);
                    Vector2 size = new Vector2(width * scale, height * scale);
                    Vector2 origin = new Vector2((x + width * 0.5f) * scale, (texture.height - y - height * 0.5f) * scale) - screenCenter;
                    Vector2 direction = origin.normalized;
                    if (direction.sqrMagnitude < 0.01f) direction = Random.insideUnitCircle.normalized;
                    direction = (direction + Random.insideUnitCircle * 0.5f).normalized;

                    float delay = origin.magnitude / 367f * 2.5f + Random.Range(0f, 0.25f);
                    const float duration = 0.9f;
                    pixels.Add(new Pixel
                    {
                        origin = origin,
                        velocity = direction * Random.Range(80f, 280f),
                        size = size,
                        delay = delay,
                        duration = duration,
                        uMin = x / (float)texture.width,
                        uMax = (x + width) / (float)texture.width,
                        vMin = (texture.height - y - height) / (float)texture.height,
                        vMax = (texture.height - y) / (float)texture.height,
                    });
                    lifetime = Mathf.Max(lifetime, delay + duration);
                }
            }

            SetVerticesDirty();
        }

        private void Update()
        {
            if (!sourceTexture) return;
            if (Time.unscaledTime - born >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            if (!sourceTexture) return;

            float now = Time.unscaledTime - born;
            for (int i = 0; i < pixels.Count; i++)
            {
                Pixel pixel = pixels[i];
                float t = now - pixel.delay;
                float progress = Mathf.Clamp01(t / pixel.duration);
                float moveTime = Mathf.Max(0f, t);
                Vector2 position = pixel.origin + pixel.velocity * moveTime + Vector2.up * (240f * moveTime * moveTime);
                float halfWidth = pixel.size.x * 0.5f;
                float halfHeight = pixel.size.y * 0.5f;
                Color32 tint = new Color(1f, 1f, 1f, t < 0f ? 1f : 1f - progress);
                int first = vertexHelper.currentVertCount;

                vertexHelper.AddVert(new Vector3(position.x - halfWidth, position.y - halfHeight), tint, new Vector2(pixel.uMin, pixel.vMin));
                vertexHelper.AddVert(new Vector3(position.x - halfWidth, position.y + halfHeight), tint, new Vector2(pixel.uMin, pixel.vMax));
                vertexHelper.AddVert(new Vector3(position.x + halfWidth, position.y + halfHeight), tint, new Vector2(pixel.uMax, pixel.vMax));
                vertexHelper.AddVert(new Vector3(position.x + halfWidth, position.y - halfHeight), tint, new Vector2(pixel.uMax, pixel.vMin));
                vertexHelper.AddTriangle(first, first + 1, first + 2);
                vertexHelper.AddTriangle(first, first + 2, first + 3);
            }
        }
    }
}
