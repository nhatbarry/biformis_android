using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>Opaque black cube faces with the source animation's exact white pixel edges.</summary>
    public sealed class OpaqueBoxGraphic : MaskableGraphic
    {
        [Serializable] public sealed class EdgeRun { public int x, y, length; }
        public sealed class Shape
        {
            public Vector2[] hull;
            public EdgeRun[] edges;
        }

        private Image outline;
        private Dictionary<Sprite, Shape> shapes;
        private Sprite drawnFrame;

        public void Configure(Image source, Dictionary<Sprite, Shape> frameShapes)
        {
            outline = source;
            shapes = frameShapes;
            raycastTarget = false;
            color = Color.white;
            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (!outline || drawnFrame == outline.sprite) return;
            drawnFrame = outline.sprite;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (!outline || !outline.sprite || shapes == null ||
                !shapes.TryGetValue(outline.sprite, out var shape) || shape.hull.Length < 3) return;

            Rect rect = rectTransform.rect;
            Vector2 pixels = outline.sprite.rect.size;
            for (int i = 0; i < shape.hull.Length; i++)
                mesh.AddVert(ToCanvas(shape.hull[i], rect, pixels), Color.black, Vector2.zero);
            for (int i = 1; i < shape.hull.Length - 1; i++)
                mesh.AddTriangle(0, i, i + 1);

            //The child draws after the original Image: black covers trails inside the cube,
            //then the original solid-white edge pixels are restored. Exterior trails remain.
            foreach (var run in shape.edges)
            {
                int start = mesh.currentVertCount;
                mesh.AddVert(ToCanvas(new Vector2(run.x, run.y + 1), rect, pixels), Color.white, Vector2.zero);
                mesh.AddVert(ToCanvas(new Vector2(run.x, run.y), rect, pixels), Color.white, Vector2.zero);
                mesh.AddVert(ToCanvas(new Vector2(run.x + run.length, run.y), rect, pixels), Color.white, Vector2.zero);
                mesh.AddVert(ToCanvas(new Vector2(run.x + run.length, run.y + 1), rect, pixels), Color.white, Vector2.zero);
                mesh.AddTriangle(start, start + 1, start + 2);
                mesh.AddTriangle(start, start + 2, start + 3);
            }
        }

        private static Vector2 ToCanvas(Vector2 point, Rect rect, Vector2 pixels) =>
            new(rect.xMin + point.x / pixels.x * rect.width, rect.yMax - point.y / pixels.y * rect.height);
    }
}
