using System.Collections.Generic;
using CaptainPinkTurd.Game.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>Floor contact, depth ordering and floor details for the open top-down arena.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class BossGroundPresentation : MonoBehaviour
    {
        private PlagueDoctorBoss boss;
        private SpriteRenderer bossArt;
        private PlayerUnit player;
        private SpriteRenderer[] playerArt;
        private Mesh shadowMesh, marksMesh;
        private Material floorMaterial;
        private MeshRenderer shadow;
        private Transform shadowTransform;
        private GameObject marks;

        private void Awake()
        {
            boss = GetComponent<PlagueDoctorBoss>();
            bossArt = GetComponent<SpriteRenderer>();
            floorMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            floorMaterial.mainTexture = Texture2D.whiteTexture;
            var root = new GameObject("Boss Ground Shadow", typeof(MeshFilter), typeof(MeshRenderer));
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.up * 0.08f;
            shadowTransform = root.transform;
            shadow = root.GetComponent<MeshRenderer>();
            shadow.sharedMaterial = floorMaterial;
            shadow.sortingLayerName = "Ground Decor";
            shadow.sortingOrder = 2;
            const int sides = 24;
            var vertices = new Vector3[sides + 1];
            var colours = new Color[sides + 1];
            var triangles = new int[sides * 3];
            for (int i = 0; i <= sides; i++) colours[i] = new Color(0.02f, 0.025f, 0.04f, 0.42f);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.78f, Mathf.Sin(angle) * 0.27f);
                triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = (i + 1) % sides + 1;
            }
            shadowMesh = new Mesh { name = "Boss floor-contact ellipse", vertices = vertices, colors = colours,
                uv = new Vector2[vertices.Length], triangles = triangles };
            root.GetComponent<MeshFilter>().sharedMesh = shadowMesh;
        }

        private void Start()
        {
            player = FindAnyObjectByType<PlayerUnit>();
            playerArt = player ? player.GetComponentsInChildren<SpriteRenderer>(true) : System.Array.Empty<SpriteRenderer>();
            var arena = FindAnyObjectByType<BossArenaController>();
            if (arena) BuildFloorMarks(arena.PlayArea);
        }

        private void LateUpdate()
        {
            SetDepth(bossArt, transform.position.y);
            if (player)
                foreach (var art in playerArt)
                    if (art && (art.name == "Red" || art.name == "Blue")) SetDepth(art, player.transform.position.y);
            foreach (var minion in boss.SummonedEnemies)
                if (minion) SetDepth(minion.GetComponent<SpriteRenderer>(), minion.transform.position.y);
            shadow.enabled = boss.Phase != PlagueDoctorBoss.EPhase.Dead;
            shadowTransform.localScale = Vector3.one / (1f + boss.VisualLift * 0.25f);
        }

        private static void SetDepth(SpriteRenderer art, float footY)
        {
            if (!art) return;
            art.sortingLayerName = "Character";
            art.sortingOrder = -Mathf.RoundToInt(footY * 100f);
            art.spriteSortPoint = SpriteSortPoint.Pivot;
        }

        private void BuildFloorMarks(Rect area)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            void Stroke(Vector2 start, Vector2 end)
            {
                Vector2 offset = new Vector2(-(end - start).y, (end - start).x).normalized * 0.025f;
                int first = vertices.Count;
                vertices.Add(start - offset); vertices.Add(start + offset); vertices.Add(end + offset); vertices.Add(end - offset);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
                triangles.Add(first); triangles.Add(first + 2); triangles.Add(first + 3);
            }
            for (float x = area.xMin - 2f; x <= area.xMax + 2f; x += 2f)
                for (float y = area.yMin - 2f; y <= area.yMax + 2f; y += 2f)
                {
                    Vector2 centre = new(x, y);
                    Stroke(centre - Vector2.right * 0.25f, centre + Vector2.right * 0.25f);
                    Stroke(centre - Vector2.up * 0.25f, centre + Vector2.up * 0.25f);
                }
            var colours = new Color[vertices.Count];
            for (int i = 0; i < colours.Length; i++) colours[i] = new Color(0.18f, 0.28f, 0.34f, 0.2f);
            marksMesh = new Mesh { name = "Top-down floor details" };
            marksMesh.SetVertices(vertices); marksMesh.SetTriangles(triangles, 0);
            marksMesh.colors = colours; marksMesh.uv = new Vector2[vertices.Count]; marksMesh.RecalculateBounds();
            marks = new GameObject("Boss Arena Floor Details", typeof(MeshFilter), typeof(MeshRenderer));
            SceneManager.MoveGameObjectToScene(marks, gameObject.scene);
            marks.GetComponent<MeshFilter>().sharedMesh = marksMesh;
            var renderer = marks.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = floorMaterial; renderer.sortingLayerName = "Ground Decor";
            renderer.sortingOrder = 30;
        }

        private void OnDestroy()
        {
            if (marks) Destroy(marks);
            if (shadowMesh) Destroy(shadowMesh);
            if (marksMesh) Destroy(marksMesh);
            if (floorMaterial) Destroy(floorMaterial);
        }
    }
}
