using System.Collections.Generic;
using CaptainPinkTurd.Core.Enum;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>Phase-two charge, coloured trails and travelling crescent waves; uses the same player colour rules as phase one.</summary>
    [RequireComponent(typeof(BossHazards))]
    public class BossPhaseTwoEffects : MonoBehaviour
    {
        [SerializeField] private float shotSpeed = 36f;
        [SerializeField] private float shotRadius = 0.34f;
        [SerializeField] private float waveSpeed = 7.5f;
        [SerializeField] private float waveWidth = 5.4f;
        [SerializeField] private float lifetime = 4.5f;
        private const int MaximumFlying = 48;
        private BossHazards rules;
        private Material lines;
        private Transform flyingRoot;
        private GameObject charge;
        private SpriteRenderer orb;
        private SpriteRenderer[] sparks;
        private LineRenderer chargeRing;
        private float chargeProgress;
        private EColor chargeColour;
        private readonly List<Flying> flying = new(48);
        private readonly List<Slash> slashes = new(4);

        private sealed class Flying
        {
            public GameObject root;
            public Vector2 position, direction;
            public EColor colour;
            public float born;
            public bool wave;
            public LineRenderer outer, core;
            public readonly List<Vector3> history = new(24);
            public readonly List<float> times = new(24);
            public float nextTrailSample;
        }
        private sealed class Slash
        {
            public GameObject root;
            public LineRenderer outer, core;
            public float born;
            public EColor colour;
        }

        public int TotalShotsEmitted { get; private set; }
        public int TotalWavesEmitted { get; private set; }
        public int ActiveProjectileCount => flying.Count;
        public bool IsCharging => charge && charge.activeSelf;
        public float WaveWidth => waveWidth;
        public static Color Colour(EColor colour) => colour == EColor.Red
            ? new Color(1f, 0.24f, 0.3f) : new Color(0.3f, 0.86f, 1f);

        private void Awake()
        {
            rules = GetComponent<BossHazards>();
            lines = new Material(rules.ProjectileMaterial) { name = "Boss Phase Two Lines", mainTexture = Texture2D.whiteTexture };
            var host = new GameObject("Boss Phase Two Travelling Effects");
            SceneManager.MoveGameObjectToScene(host, gameObject.scene);
            flyingRoot = host.transform;
            charge = new GameObject("Boss Energy Charge");
            charge.transform.SetParent(transform, false);
            orb = NewSprite("Energy Core", charge.transform);
            sparks = new SpriteRenderer[6];
            for (int i = 0; i < sparks.Length; i++) sparks[i] = NewSprite("Gathering Spark " + i, charge.transform);
            chargeRing = NewLine("Charge Ring", charge.transform, 0.05f, false);
            chargeRing.loop = true;
            chargeRing.positionCount = 24;
            charge.SetActive(false);
        }

        private SpriteRenderer NewSprite(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sprite = go.GetComponent<SpriteRenderer>();
            sprite.sharedMaterial = rules.ProjectileMaterial;
            sprite.sortingOrder = 16;
            return sprite;
        }

        private LineRenderer NewLine(string name, Transform parent, float width, bool world)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(parent, false);
            var line = go.GetComponent<LineRenderer>();
            line.sharedMaterial = lines;
            line.useWorldSpace = world;
            line.widthMultiplier = width;
            line.sortingOrder = 15;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            return line;
        }

        public void ShowCharge(EColor colour, float progress)
        {
            chargeColour = colour;
            chargeProgress = Mathf.Clamp01(progress);
            charge.SetActive(true);
            DrawCharge();
        }
        public void HideCharge() { if (charge) charge.SetActive(false); }

        private void DrawCharge()
        {
            charge.transform.localPosition = Vector3.up * 2.9f;
            SetArtSize(orb, chargeColour, Mathf.Lerp(0.2f, 1.25f, chargeProgress));
            var colour = Colour(chargeColour);
            chargeRing.startColor = chargeRing.endColor = colour;
            float radius = Mathf.Lerp(0.55f, 0.25f, chargeProgress);
            for (int i = 0; i < 24; i++)
            {
                float angle = i * Mathf.PI * 2f / 24;
                chargeRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }
            for (int i = 0; i < sparks.Length; i++)
            {
                float angle = Time.time * 7f + i * Mathf.PI * 2f / sparks.Length;
                SetArtSize(sparks[i], chargeColour, 0.13f);
                sparks[i].transform.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + 0.2f);
            }
        }

        public void FireFan(Vector2 origin, Vector2 direction, EColor colour)
        {
            // Cover the player's general direction instead of placing the middle shot exactly on them.
            float drift = Random.Range(-8f, 8f);
            for (int i = -1; i <= 1; i++)
                Spawn(origin, Rotate(direction, i * 30f + drift + Random.Range(-3f, 3f)), colour, false);
        }
        public void FireWave(Vector2 origin, Vector2 direction, EColor colour) => Spawn(origin, direction, colour, true);

        private void Spawn(Vector2 origin, Vector2 direction, EColor colour, bool wave)
        {
            if (flying.Count >= MaximumFlying || direction.sqrMagnitude < 0.001f) return;
            var go = new GameObject(wave ? "Boss Sword Aura " + colour : "Boss Energy Shot " + colour);
            go.transform.SetParent(flyingRoot, false);
            go.transform.position = origin;
            var item = new Flying { root = go, position = origin, direction = direction.normalized,
                colour = colour, wave = wave, born = Time.time, nextTrailSample = Time.time };
            item.outer = NewLine(wave ? "Aura Colour" : "Coloured Trail", go.transform, wave ? 0.65f : 0.28f, true);
            item.core = NewLine(wave ? "Aura Core" : "Trail Core", go.transform, wave ? 0.13f : 0.08f, true);
            item.outer.startColor = item.outer.endColor = Colour(colour);
            item.core.startColor = item.core.endColor = new Color(1f, 1f, 1f, 0.85f);
            item.outer.widthCurve = item.core.widthCurve = wave
                ? new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0))
                : AnimationCurve.Linear(0, 1, 1, 0);
            if (!wave)
            {
                var art = NewSprite("Projectile", go.transform);
                SetArtSize(art, colour, shotRadius * 2f);
                TotalShotsEmitted++;
            }
            else TotalWavesEmitted++;
            flying.Add(item);
            Draw(item);
        }

        public void ShowSlash(Vector2 centre, Vector2 direction, float radius, EColor colour)
        {
            var root = new GameObject("Boss Melee Slash " + colour);
            root.transform.SetParent(flyingRoot, false);
            var outer = NewLine("Slash Colour", root.transform, 0.2f, true);
            var core = NewLine("Slash Core", root.transform, 0.055f, true);
            outer.startColor = outer.endColor = Colour(colour);
            core.startColor = core.endColor = Color.white;
            outer.positionCount = core.positionCount = 21;
            for (int i = 0; i < 21; i++)
            {
                Vector2 point = centre + Rotate(direction, -60f + i * 6f) * radius;
                outer.SetPosition(i, point); core.SetPosition(i, point);
            }
            slashes.Add(new Slash { root = root, outer = outer, core = core, born = Time.time, colour = colour });
        }

        public bool TryHitSector(Vector2 centre, Vector2 direction, float radius, EColor colour)
        {
            if (!rules.CanHurtPlayer(colour)) return false;
            Vector2 offset = rules.PlayerHitPosition - centre;
            if (offset.magnitude > radius + rules.PlayerRadius) return false;
            if (offset.sqrMagnitude > 0.16f && Vector2.Dot(offset.normalized, direction) < 0.5f) return false;
            rules.HitPlayer(colour);
            return true;
        }

        public bool TryHitSegment(Vector2 from, Vector2 to, float radius, EColor colour)
        {
            if (!rules.CanHurtPlayer(colour) || DistanceToSegment(rules.PlayerHitPosition, from, to) > radius + rules.PlayerRadius) return false;
            rules.HitPlayer(colour);
            return true;
        }

        private void Update()
        {
            float tick = Time.deltaTime;
            if (tick <= 0f) return;
            if (IsCharging) DrawCharge();
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var item = flying[i];
                Vector2 previous = item.position;
                item.position += item.direction * (item.wave ? waveSpeed : shotSpeed) * tick;
                bool hit = false;
                if (rules.CanHurtPlayer(item.colour))
                {
                    if (item.wave)
                    {
                        Vector2 side = new(-item.direction.y, item.direction.x);
                        Vector2 offset = rules.PlayerHitPosition - previous;
                        float across = Vector2.Dot(offset, side);
                        float u = Mathf.Clamp(across / (waveWidth * 0.5f), -1f, 1f);
                        float front = Vector2.Dot(offset, item.direction) - 0.8f * (1f - u * u);
                        hit = Mathf.Abs(across) <= waveWidth * 0.5f + rules.PlayerRadius &&
                            front >= -0.3f - rules.PlayerRadius && front <= waveSpeed * tick + 0.3f + rules.PlayerRadius;
                    }
                    else hit = DistanceToSegment(rules.PlayerHitPosition, previous, item.position) <= shotRadius + rules.PlayerRadius;
                }
                if (hit) rules.HitPlayer(item.colour);
                if (hit || Time.time - item.born >= lifetime)
                {
                    Destroy(item.root);
                    flying.RemoveAt(i);
                    continue;
                }
                Draw(item);
            }
            for (int i = slashes.Count - 1; i >= 0; i--)
            {
                var slash = slashes[i];
                float age = Time.time - slash.born;
                if (age >= 0.22f) { Destroy(slash.root); slashes.RemoveAt(i); continue; }
                Color tint = Colour(slash.colour); tint.a = 1f - age / 0.22f;
                slash.outer.startColor = slash.outer.endColor = tint;
                slash.core.startColor = slash.core.endColor = new Color(1f, 1f, 1f, tint.a);
            }
        }

        private void Draw(Flying item)
        {
            item.root.transform.position = item.position;
            if (item.wave)
            {
                item.outer.positionCount = item.core.positionCount = 25;
                Vector2 side = new(-item.direction.y, item.direction.x);
                for (int i = 0; i < 25; i++)
                {
                    float u = i / 12f - 1f;
                    Vector2 point = item.position + side * (u * waveWidth * 0.5f) + item.direction * (0.8f * (1f - u * u));
                    item.outer.SetPosition(i, point); item.core.SetPosition(i, point);
                }
            }
            else
            {
                if (Time.time >= item.nextTrailSample)
                {
                    item.history.Insert(0, item.position);
                    item.times.Insert(0, Time.time);
                    item.nextTrailSample = Time.time + 0.025f;
                }
                while (item.history.Count > 20 || (item.times.Count > 1 && Time.time - item.times[^1] > 0.14f))
                { item.history.RemoveAt(item.history.Count - 1); item.times.RemoveAt(item.times.Count - 1); }
                item.outer.positionCount = item.core.positionCount = item.history.Count;
                for (int i = 0; i < item.history.Count; i++)
                { item.outer.SetPosition(i, item.history[i]); item.core.SetPosition(i, item.history[i]); }
            }
        }

        public void ClearProjectiles()
        {
            foreach (var item in flying) if (item.root) Destroy(item.root);
            flying.Clear();
        }
        public void ClearAll()
        {
            ClearProjectiles();
            HideCharge();
            foreach (var slash in slashes) if (slash.root) Destroy(slash.root);
            slashes.Clear();
        }
        private void OnDisable() => ClearAll();
        private void OnDestroy()
        {
            if (flyingRoot) Destroy(flyingRoot.gameObject);
            if (lines) Destroy(lines);
        }
        private static Vector2 Rotate(Vector2 point, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector2(point.x * Mathf.Cos(angle) - point.y * Mathf.Sin(angle),
                point.x * Mathf.Sin(angle) + point.y * Mathf.Cos(angle));
        }
        private void SetArtSize(SpriteRenderer art, EColor colour, float diameter)
        {
            art.sprite = rules.ProjectileSprite(colour);
            float width = art.sprite.rect.width / art.sprite.pixelsPerUnit;
            art.transform.localScale = Vector3.one * (diameter / width);
        }
        private static float DistanceToSegment(Vector2 point, Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float t = delta.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(point - from, delta) / delta.sqrMagnitude) : 0f;
            return Vector2.Distance(point, from + delta * t);
        }
    }
}
