using System.Collections.Generic;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Game.Player;
using UnityEngine;
using UnityEngine.Serialization;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>
    /// The existing enemies' R2/B2 round bullets, with bounded data and two batched meshes.
    /// Only the player's small hitbox is tested. Colour and dash rules match the other Biformis emitters.
    /// </summary>
    public class BossHazards : MonoBehaviour
    {
        [FormerlySerializedAs("redSyringe"), SerializeField] private Sprite redProjectile;
        [FormerlySerializedAs("blueSyringe"), SerializeField] private Sprite blueProjectile;
        [SerializeField] private Material projectileMaterial;
        [SerializeField] private Sprite[] lightningOne;
        [SerializeField] private Sprite[] lightningTwo;
        [SerializeField] private float lightningFrameSeconds = 0.1f;
        [SerializeField] private float warningSeconds = 1.1f;
        [SerializeField] private float strikeRadius = 0.8f;
        [FormerlySerializedAs("needleLength"), SerializeField] private float projectileDiameter = 1f;
        [FormerlySerializedAs("needleLifetime"), SerializeField] private float projectileLifetime = 4.5f;
        [FormerlySerializedAs("maximumNeedles"), SerializeField] private int maximumProjectiles = 640;

        private struct Projectile
        {
            public Vector2 position, direction;
            public float speed, remaining;
            public EColor color;
        }

        private sealed class Batch
        {
            public Sprite sprite;
            public Mesh mesh;
            public Material material;
            public Vector4 uv;
            public readonly List<Vector3> vertices = new(2560);
            public readonly List<Vector2> uvs = new(2560);
            public readonly List<Color32> colors = new(2560);
            public readonly List<int> triangles = new(3840);
        }

        private sealed class Strike
        {
            public Vector2 position;
            public EColor color;
            public float born;
            public bool hit;
            public Sprite[] frames;
            public GameObject root;
            public LineRenderer warning;
            public SpriteRenderer bolt;
        }

        private readonly List<Projectile> projectiles = new(640);
        private readonly List<Strike> strikes = new(20);
        private Batch redBatch, blueBatch;
        private PlayerUnit player;
        private IDamageable playerHealth;
        private CircleCollider2D playerHitbox;
        private Material unlit;
        private BossArenaController arena;
        private int redLayer, blueLayer;

        public int ActiveProjectileCount => projectiles.Count;
        public int ActiveStrikeCount => strikes.Count;
        public int TotalProjectilesEmitted { get; private set; }
        public int TotalStrikesWarned { get; private set; }
        public float WarningSeconds => warningSeconds;
        public bool HasPlayer => PlayerAvailable;
        public Vector2 PlayerHitPosition => HitboxPosition;
        public float PlayerRadius => HitboxRadius;
        public Material ProjectileMaterial => projectileMaterial;
        public Sprite ProjectileSprite(EColor color) => color == EColor.Red ? redProjectile : blueProjectile;
        public bool CanHurtPlayer(EColor color) => HurtsPlayer(color);
        public int TotalPlayerImpacts { get; private set; }
        public int TotalAttackImpacts { get; private set; }
        public void PlayAttackImpact(float stopSeconds = 0.045f)
        {
            TotalAttackImpacts++;
            Impact(stopSeconds, 0.13f);
        }
        private void Impact(float stopSeconds, float strength)
        {
            if (!arena) arena = FindAnyObjectByType<BossArenaController>();
            if (arena) arena.ShakeImpact(strength);
            HitStop.Stop(stopSeconds);
        }
        public void HitPlayer(EColor color)
        {
            if (!HurtsPlayer(color)) return;
            int previous = playerHealth.CurrentHealth;
            playerHealth.TakeDamage(new SDamageData(1, gameObject));
            if (playerHealth.CurrentHealth >= previous) return;
            TotalPlayerImpacts++;
            Impact(0.08f, 0.2f);
        }

        private void Awake()
        {
            unlit = projectileMaterial ? projectileMaterial : GetComponent<SpriteRenderer>().sharedMaterial;
            redLayer = LayerMask.NameToLayer("Red");
            blueLayer = LayerMask.NameToLayer("Blue");
            redBatch = CreateBatch("Red Projectiles", redProjectile);
            blueBatch = CreateBatch("Blue Projectiles", blueProjectile);
        }

        public void Bind(PlayerUnit target)
        {
            player = target;
            playerHealth = target ? target.GetComponent<IDamageable>() : null;
            playerHitbox = target ? target.GetComponentInChildren<CircleCollider2D>() : null;
        }

        private Batch CreateBatch(string name, Sprite art)
        {
            var batch = new Batch { sprite = art, mesh = new Mesh { name = name } };
            batch.mesh.MarkDynamic();
            batch.uv = art ? UnityEngine.Sprites.DataUtility.GetOuterUV(art) : Vector4.zero;
            batch.material = new Material(unlit) { name = name, mainTexture = art ? art.texture : null };
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.layer = LayerMask.NameToLayer("Projectile");
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = batch.mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = batch.material;
            renderer.sortingLayerName = "Default";
            renderer.sortingOrder = 5;
            return batch;
        }

        public void FireProjectile(Vector2 origin, Vector2 direction, EColor color, float speed)
        {
            if (projectiles.Count >= maximumProjectiles || direction.sqrMagnitude < 0.001f) return;
            projectiles.Add(new Projectile
            {
                position = origin, direction = direction.normalized, speed = speed,
                remaining = projectileLifetime, color = color,
            });
            TotalProjectilesEmitted++;
        }

        /// <summary>Positions are locked when warned; the marker never chases the player.</summary>
        public void WarnLightning(Vector2 position, EColor color, int variant)
        {
            var frames = variant == 0 ? lightningOne : lightningTwo;
            if (frames == null || frames.Length == 0) return;
            var root = new GameObject("Lightning Warning " + color);
            root.layer = LayerMask.NameToLayer("Ignore Raycast");
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            var warning = root.AddComponent<LineRenderer>();
            warning.sharedMaterial = unlit;
            warning.useWorldSpace = true;
            warning.loop = true;
            warning.widthMultiplier = 0.075f;
            warning.positionCount = 40;
            warning.sortingLayerName = "Default";
            warning.sortingOrder = 4;
            for (int i = 0; i < 40; i++)
            {
                float angle = i * Mathf.PI * 2f / 40;
                warning.SetPosition(i, new Vector3(position.x + Mathf.Cos(angle) * strikeRadius,
                    position.y + Mathf.Sin(angle) * strikeRadius, 0f));
            }
            var boltObject = new GameObject("Vertical Lightning", typeof(SpriteRenderer));
            boltObject.layer = root.layer;
            boltObject.transform.SetParent(root.transform, false);
            var bolt = boltObject.GetComponent<SpriteRenderer>();
            bolt.sharedMaterial = unlit;
            bolt.sortingLayerName = "Default";
            bolt.sortingOrder = 6;
            bolt.color = StrikeColor(color);
            bolt.enabled = false;
            strikes.Add(new Strike { position = position, color = color, born = Time.time,
                root = root, warning = warning, bolt = bolt, frames = frames });
            TotalStrikesWarned++;
        }

        private static Color StrikeColor(EColor color) => color == EColor.Red
            ? new Color(1f, 0.18f, 0.23f) : new Color(0.35f, 0.85f, 1f);

        private bool PlayerAvailable => player && player.gameObject.activeInHierarchy && playerHealth != null;
        private bool HurtsPlayer(EColor color) => PlayerAvailable &&
            player.gameObject.layer == (color == EColor.Red ? blueLayer : redLayer);
        private Vector2 HitboxPosition => playerHitbox ? playerHitbox.bounds.center : player.transform.position;
        private float HitboxRadius => playerHitbox ? playerHitbox.bounds.extents.x : 0.125f;

        private void Update()
        {
            float tick = Time.deltaTime;
            if (tick <= 0f) return;
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var projectile = projectiles[i];
                Vector2 previous = projectile.position;
                projectile.position += projectile.direction * projectile.speed * tick;
                projectile.remaining -= tick;
                // The old emitters circle-cast at Scale/2. Sweep a round bullet at the same visual radius.
                bool hit = HurtsPlayer(projectile.color) && DistanceToSegment(HitboxPosition,
                    previous, projectile.position) <= HitboxRadius + projectileDiameter * 0.5f;
                if (hit) HitPlayer(projectile.color);
                if (hit || projectile.remaining <= 0f)
                {
                    projectiles[i] = projectiles[^1];
                    projectiles.RemoveAt(projectiles.Count - 1);
                }
                else projectiles[i] = projectile;
            }

            for (int i = strikes.Count - 1; i >= 0; i--)
            {
                var strike = strikes[i];
                strike.root.transform.position = strike.position;
                float age = Time.time - strike.born;
                // Both delivered clips first contact the ground on their second frame.
                float impact = warningSeconds + lightningFrameSeconds;
                if (age < impact)
                {
                    var color = StrikeColor(strike.color);
                    color.a = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(age * 9f));
                    strike.warning.startColor = strike.warning.endColor = color;
                }
                else
                {
                    strike.warning.enabled = false;
                    if (!strike.hit)
                    {
                        strike.hit = true;
                        if (HurtsPlayer(strike.color) && Vector2.Distance(HitboxPosition, strike.position) <= strikeRadius + HitboxRadius)
                            HitPlayer(strike.color);
                    }
                }
                if (age < warningSeconds) continue;
                int frame = Mathf.FloorToInt((age - warningSeconds) / lightningFrameSeconds);
                if (frame >= strike.frames.Length)
                {
                    Destroy(strike.root);
                    strikes.RemoveAt(i);
                    continue;
                }
                strike.bolt.enabled = true;
                strike.bolt.sprite = strike.frames[frame];
            }
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 delta = end - start;
            float t = delta.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - start, delta) / delta.sqrMagnitude) : 0f;
            return Vector2.Distance(point, start + delta * t);
        }

        private void LateUpdate()
        {
            redBatch.vertices.Clear(); redBatch.uvs.Clear(); redBatch.colors.Clear(); redBatch.triangles.Clear();
            blueBatch.vertices.Clear(); blueBatch.uvs.Clear(); blueBatch.colors.Clear(); blueBatch.triangles.Clear();
            foreach (var projectile in projectiles) AddQuad(projectile.color == EColor.Red ? redBatch : blueBatch, projectile);
            Upload(redBatch); Upload(blueBatch);
        }

        private void AddQuad(Batch batch, Projectile projectile)
        {
            if (!batch.sprite) return;
            // Keep the pixel circle upright and square, exactly like the standard enemy projectile texture.
            Vector2 side = Vector2.right * (projectileDiameter * 0.5f);
            Vector2 length = Vector2.up * (projectileDiameter * 0.5f);
            int first = batch.vertices.Count;
            batch.vertices.Add(transform.InverseTransformPoint(projectile.position - side - length));
            batch.vertices.Add(transform.InverseTransformPoint(projectile.position + side - length));
            batch.vertices.Add(transform.InverseTransformPoint(projectile.position + side + length));
            batch.vertices.Add(transform.InverseTransformPoint(projectile.position - side + length));
            batch.uvs.Add(new Vector2(batch.uv.x, batch.uv.y));
            batch.uvs.Add(new Vector2(batch.uv.z, batch.uv.y));
            batch.uvs.Add(new Vector2(batch.uv.z, batch.uv.w));
            batch.uvs.Add(new Vector2(batch.uv.x, batch.uv.w));
            for (int i = 0; i < 4; i++) batch.colors.Add(new Color32(255, 255, 255, 255));
            batch.triangles.Add(first); batch.triangles.Add(first + 1); batch.triangles.Add(first + 2);
            batch.triangles.Add(first); batch.triangles.Add(first + 2); batch.triangles.Add(first + 3);
        }

        private static void Upload(Batch batch)
        {
            batch.mesh.Clear();
            batch.mesh.SetVertices(batch.vertices);
            batch.mesh.SetUVs(0, batch.uvs);
            batch.mesh.SetColors(batch.colors);
            batch.mesh.SetTriangles(batch.triangles, 0);
            batch.mesh.RecalculateBounds();
        }

        public void ClearHazards()
        {
            ClearProjectiles();
            foreach (var strike in strikes) if (strike.root) Destroy(strike.root);
            strikes.Clear();
        }

        public void ClearProjectiles()
        {
            projectiles.Clear();
            redBatch?.mesh.Clear(); blueBatch?.mesh.Clear();
        }

        private void OnDisable() => ClearHazards();
        private void OnDestroy()
        {
            if (redBatch != null) { Destroy(redBatch.mesh); Destroy(redBatch.material); }
            if (blueBatch != null) { Destroy(blueBatch.mesh); Destroy(blueBatch.material); }
        }
    }
}
