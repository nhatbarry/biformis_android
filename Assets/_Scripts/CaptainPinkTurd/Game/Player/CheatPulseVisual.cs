using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaptainPinkTurd.Game.Player
{
    /// <summary>Code-drawn energy ring. Map-edge sectors dissolve into three shrinking sparks.</summary>
    public sealed class CheatPulseVisual : MonoBehaviour
    {
        public const int Segments = 192;
        private const float AbsorptionSeconds = 0.28f;
        private static readonly Color Energy = new(0.64f, 1f, 0.88f, 0.8f);
        private readonly List<Vector3> vertices = new(Segments * 20);
        private readonly List<Color> colours = new(Segments * 20);
        private readonly List<int> triangles = new(Segments * 30);
        private Mesh mesh;
        private Material material;
        private float[] walls, absorbedAt;
        private float maximumRadius, seconds, elapsed;

        public static Vector2 Direction(int segment)
        {
            float angle = segment * Mathf.PI * 2f / Segments;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        public static CheatPulseVisual Spawn(Scene scene, Vector2 origin, float radius, float[] wallDistances)
        {
            var root = new GameObject("Cheat Energy Pulse", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = new Vector3(origin.x, origin.y, 0f);
            var visual = root.AddComponent<CheatPulseVisual>();
            visual.maximumRadius = radius;
            visual.seconds = Mathf.Clamp(radius / 24f, 0.65f, 3f);
            visual.walls = wallDistances;
            visual.absorbedAt = new float[Segments];
            for (int i = 0; i < Segments; i++) visual.absorbedAt[i] = -1f;
            visual.Draw(0.15f);
            return visual;
        }

        private void Awake()
        {
            mesh = new Mesh { name = "Cheat energy ring" };
            mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = mesh;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (!shader) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Cheat energy" };
            material.mainTexture = Texture2D.whiteTexture;
            var renderer = GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 20;
        }

        private void Update()
        {
            // Gameplay time makes pause freeze both the wave and its absorption sparks.
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / seconds);
            Draw(Mathf.Max(0.15f, maximumRadius * progress * progress));
            if (elapsed >= seconds + AbsorptionSeconds) Destroy(gameObject);
        }

        private void Draw(float radius)
        {
            vertices.Clear(); colours.Clear(); triangles.Clear();
            for (int i = 0; i < Segments; i++)
            {
                int next = (i + 1) % Segments;
                float contact = Mathf.Min(walls[i], walls[next]);
                bool wall = contact < maximumRadius - 0.01f;
                if (wall && radius >= contact)
                {
                    if (absorbedAt[i] < 0f) absorbedAt[i] = elapsed;
                    DrawAbsorption(Direction(i) * contact, Direction(i), elapsed - absorbedAt[i]);
                    continue;
                }
                if (elapsed > seconds) continue;
                var a = Direction(i); var b = Direction(next);
                float front = Mathf.Min(radius, contact);
                float inner = Mathf.Max(0f, front - 0.38f);
                Quad(a * inner, a * front, b * front, b * inner,
                    new Color(Energy.r, Energy.g, Energy.b, 0.02f), new Color(Energy.r, Energy.g, Energy.b, 0.36f));
                Quad(a * Mathf.Max(0f, front - 0.075f), a * front, b * front, b * Mathf.Max(0f, front - 0.075f),
                    Energy, new Color(0.92f, 1f, 0.97f, 0.95f));
            }
            mesh.Clear();
            mesh.SetVertices(vertices); mesh.SetColors(colours); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
        }

        private void DrawAbsorption(Vector2 point, Vector2 direction, float age)
        {
            if (age >= AbsorptionSeconds) return;
            float remaining = 1f - age / AbsorptionSeconds;
            Vector2 tangent = new(-direction.y, direction.x);
            for (int spark = -1; spark <= 1; spark++)
            {
                Vector2 centre = point - direction * (remaining * 0.26f) + tangent * (spark * 0.16f * remaining);
                float size = 0.07f * remaining;
                var tint = new Color(0.82f, 1f, 0.94f, remaining * 0.8f);
                Quad(centre - direction * size, centre + tangent * size,
                    centre + direction * size, centre - tangent * size, tint, tint);
            }
        }

        private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color inner, Color outer)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            colours.Add(inner); colours.Add(outer); colours.Add(outer); colours.Add(inner);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}
