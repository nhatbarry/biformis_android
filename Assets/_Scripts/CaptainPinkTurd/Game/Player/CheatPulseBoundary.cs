using System.Collections.Generic;
using CaptainPinkTurd.Game.Enemy;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaptainPinkTurd.Game.Player
{
    /// <summary>World-space perimeter intersections, including when the emitter starts inside a trigger collider.</summary>
    internal sealed class CheatPulseBoundary
    {
        private readonly List<(Vector2[] points, bool closed)> paths = new();

        public static CheatPulseBoundary ForMap(Scene map)
        {
            var boundary = new CheatPulseBoundary();
            foreach (var arena in Object.FindObjectsByType<BossArenaController>(FindObjectsSortMode.None))
            {
                if (!arena.isActiveAndEnabled || arena.gameObject.scene != map) continue;
                boundary.AddRectangle(arena.PlayArea);
                return boundary;
            }
            foreach (var collider in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                if (!collider.enabled || collider.gameObject.scene != map || collider.name != "Map Bound") continue;
                if (collider is BoxCollider2D box)
                {
                    var rect = new Rect(box.offset - box.size * 0.5f, box.size);
                    boundary.AddPath(Rectangle(rect), box.transform, Vector2.zero, true);
                }
                else if (collider is PolygonCollider2D polygon)
                {
                    for (int i = 0; i < polygon.pathCount; i++)
                        boundary.AddPath(polygon.GetPath(i), polygon.transform, polygon.offset, true);
                }
                else if (collider is EdgeCollider2D edge)
                    boundary.AddPath(edge.points, edge.transform, edge.offset, false);
                else if (collider is CompositeCollider2D composite)
                {
                    for (int i = 0; i < composite.pathCount; i++)
                    {
                        var points = new Vector2[composite.GetPathPointCount(i)];
                        composite.GetPath(i, points);
                        boundary.AddPath(points, composite.transform, composite.offset, true);
                    }
                }
                else boundary.AddRectangle(new Rect(collider.bounds.min, collider.bounds.size));
            }
            if (boundary.paths.Count > 0) return boundary;

            // Some older Endless maps have no named confiner. Bound their geometry, with the view as a minimum.
            var bounds = new Bounds(Vector3.zero, new Vector3(16f, 16f, 0f));
            var camera = Camera.main;
            if (camera && camera.orthographic)
                bounds = new Bounds(camera.transform.position,
                    new Vector3(camera.orthographicSize * camera.aspect * 2f, camera.orthographicSize * 2f, 0f));
            foreach (var collider in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (collider.enabled && collider.gameObject.scene == map) bounds.Encapsulate(collider.bounds);
            boundary.AddRectangle(new Rect(bounds.min, bounds.size));
            return boundary;
        }

        public float MaximumDistance(Vector2 origin)
        {
            float distance = 1f;
            foreach (var (points, _) in paths)
                foreach (var point in points) distance = Mathf.Max(distance, Vector2.Distance(origin, point));
            return distance;
        }

        public float ExitDistance(Vector2 origin, Vector2 direction, float maximum)
        {
            float closest = maximum;
            foreach (var (points, closed) in paths)
            {
                int edges = closed ? points.Length : points.Length - 1;
                for (int i = 0; i < edges; i++)
                {
                    Vector2 a = points[i], segment = points[(i + 1) % points.Length] - a;
                    float denominator = Cross(direction, segment);
                    if (Mathf.Abs(denominator) < 0.00001f) continue;
                    Vector2 fromOrigin = a - origin;
                    float distance = Cross(fromOrigin, segment) / denominator;
                    float along = Cross(fromOrigin, direction) / denominator;
                    if (distance > 0.0001f && along >= 0f && along <= 1f)
                        closest = Mathf.Min(closest, distance);
                }
            }
            return closest;
        }

        private void AddRectangle(Rect rect) => paths.Add((Rectangle(rect), true));
        private void AddPath(Vector2[] points, Transform transform, Vector2 offset, bool closed)
        {
            for (int i = 0; i < points.Length; i++) points[i] = transform.TransformPoint(points[i] + offset);
            paths.Add((points, closed));
        }
        private static Vector2[] Rectangle(Rect rect) => new[]
        {
            new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin),
            new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax)
        };
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
