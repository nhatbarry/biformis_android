using System;
using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.Core;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.EffectSystem.ShakeEffect;
using BulletHell;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>
    /// Fifteen points: seven hooded points, an invulnerable five-enemy ice intermission, then eight points.
    /// The red-haired phase uses its four colour-based attacks; death advances to the ending.
    /// Each clip keeps the frame durations and 2.5x world size of the team's imported art.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D), typeof(BossHazards))]
    [RequireComponent(typeof(BossHealthBar), typeof(CircleCollider2D), typeof(Rigidbody2D))]
    [RequireComponent(typeof(BossGroundPresentation))]
    public class PlagueDoctorBoss : MonoBehaviour, IDamageable
    {
        public enum EPhase { Hooded, Freezing, Frozen, Shattering, RedHaired, Dead, Revealing }
        public enum EPattern { LongStream, Circle, Clusters }
        [Serializable]
        public class Clip
        {
            public Sprite[] frames;
            [Tooltip("How long each frame shows, in milliseconds (the pack's manifest.json)")]
            public int[] frameMilliseconds;
            [HideInInspector] public Vector4[] bladeEndpoints;
            [HideInInspector] public float[] bladeWidths;

            public float FrameSeconds(int frame) => frameMilliseconds[frame] / 1000f;
        }

        [Header("Clips")]
        [SerializeField] private Clip idle;
        [SerializeField] private Clip cast;
        [SerializeField] private Clip walkThrow;
        [Tooltip("Ice closes over him, turns red and shatters, leaving the red-haired form")]
        [SerializeField] private Clip iceShatter;
        [SerializeField] private Sprite redHair;
        [SerializeField] private Sprite phaseTwoRestingPose;
        [Tooltip("Raises his arms and floats up. The pack has no sheet for it (group 04): rebuilt from its concept image")]
        [SerializeField] private Clip levitate;

        [Header("Behaviour")]
        [SerializeField] private Vector2 idleSeconds = new(0.65f, 1.1f);
        [Tooltip("Chance of each action against the others; 0 leaves it out")]
        [SerializeField] private float castWeight = 1f;
        [SerializeField] private float walkThrowWeight = 1f;
        [SerializeField] private float levitateWeight = 1f;
        [Tooltip("Walk-and-throw loops per walk")]
        [SerializeField] private Vector2Int walkLoops = new(2, 3);
        [Tooltip("How long he floats before coming back down")]
        [SerializeField] private float hoverSeconds = 4.5f;
        [Tooltip("While he floats, the levitate clip's last frames loop, bobbing him up and down")]
        [SerializeField] private int hoverLoopFrames = 4;
        [SerializeField] private float damageCooldown = 0.8f;
        [FormerlySerializedAs("needleSpeed"), SerializeField] private float projectileSpeed = 8.5f;

        [Header("Enemy hit feedback")]
        [SerializeField] private float knockbackForce = 30f;
        [SerializeField] private float hitStopDuration = 0.2f;
        [SerializeField] private GameObjectShakeProfile shakeProfile;
        [SerializeField] private ShockwaveScreen impactShockwavePrefab;
        [SerializeField] private SoundData damagedSfx;

        [Header("Ice intermission")]
        [Tooltip("Intact blue ice frame; red/shatter frames wait for all minions to die")]
        [SerializeField] private int frozenFrame = 15;
        [SerializeField] private GameObject[] summonPrefabs;
        [SerializeField] private float summonRadius = 6.5f;
        [SerializeField] private float summonSpacingSeconds = 0.4f;

        private enum EAction
        {
            Cast,
            WalkThrow,
            Levitate,
        }

        private SpriteRenderer sprite;
        private PlayerUnit player;
        private EAction? lastAction;
        private EPattern? previousPattern;
        private BoxCollider2D hitbox;
        private CircleCollider2D feet;
        private Rigidbody2D body;
        private Tween shakeTween, flashTween;
        private MaterialPropertyBlock flash;
        private static readonly int HitEffectAmount = Shader.PropertyToID("_HitEffectAmount");
        private BossHazards hazards;
        private Coroutine live;
        private float nextDamageTime;
        private readonly List<GameObject> minions = new(5);

        public EPhase Phase { get; private set; }
        public EPattern CurrentPattern { get; private set; }
        public int CurrentHealth { get; private set; } = 15;
        public int MaxHealth => 15;
        public int PhaseMaximumHealth => Phase == EPhase.Shattering || Phase == EPhase.Revealing || Phase == EPhase.RedHaired || Phase == EPhase.Dead ? 8 : 7;
        public int PhaseHealth => PhaseMaximumHealth == 8 ? CurrentHealth : Mathf.Max(0, CurrentHealth - 8);
        public bool IsFrozen => Phase == EPhase.Freezing || Phase == EPhase.Frozen || Phase == EPhase.Shattering;
        public bool IsInvulnerable => (Phase != EPhase.Hooded && Phase != EPhase.RedHaired) || Time.time < nextDamageTime;
        public float VisualLift { get; private set; }
        public IReadOnlyList<GameObject> SummonedEnemies => minions;
        public GameEvent<SDamageData> OnTakeDamage { get; } = new();
        public GameEvent<SDamageData> OnDeath { get; } = new();
        public GameEvent OnPhaseTwoReady { get; } = new();
        public Transform GetTransform() => transform;

        // Explicit editor preview: normal gameplay still reaches this pose only after all five summons die.
        [ContextMenu("Preview Phase Two (Play Mode)")]
        public void PreviewPhaseTwo()
        {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (live != null) StopCoroutine(live);
            shakeTween?.Kill();
            flashTween?.Kill();
            hazards.ClearHazards();
            foreach (var minion in minions) if (minion) Destroy(minion);
            minions.Clear();
            CurrentHealth = 8;
            VisualLift = 0f;
            hitbox.enabled = false;
            hitbox.offset = new Vector2(0f, 1f);
            feet.offset = new Vector2(0f, 0.25f);
            Phase = EPhase.Shattering;
            live = StartCoroutine(RevealPhaseTwo(true));
        }

        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            hitbox = GetComponent<BoxCollider2D>();
            feet = GetComponent<CircleCollider2D>();
            body = GetComponent<Rigidbody2D>();
            flash = new MaterialPropertyBlock();
            hazards = GetComponent<BossHazards>();
            gameObject.layer = LayerMask.NameToLayer("Enemy");
            hitbox.isTrigger = true;
            // Like Luneblade: a broad damage trigger plus a solid feet collider. The trigger reaches beyond
            // the feet so a normal approach registers its hit before the physical contact stops the player.
            hitbox.size = new Vector2(1.4f, 2.8f);
            hitbox.offset = new Vector2(0f, 1f);
            feet.isTrigger = false;
            feet.radius = 0.55f;
            feet.offset = new Vector2(0f, 0.25f);
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        private void OnEnable()
        {
            CurrentHealth = 15;
            Phase = EPhase.Hooded;
            nextDamageTime = 0f;
            lastAction = null;
            previousPattern = null;
            VisualLift = 0f;
            hitbox.enabled = true;
            feet.enabled = true;
            feet.offset = new Vector2(0f, 0.25f);
            SetFlash(0f);
            player = FindAnyObjectByType<PlayerUnit>();
            hazards.Bind(player);
            live = StartCoroutine(Live());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            shakeTween?.Kill();
            flashTween?.Kill();
            SetFlash(0f);
            hazards.ClearHazards();
            foreach (var minion in minions) if (minion) Destroy(minion);
            minions.Clear();
        }

        public void TakeDamage(SDamageData damageData)
        {
            if (IsInvulnerable || damageData.Amount <= 0 || !damageData.Source ||
                !damageData.Source.GetComponentInParent<PlayerUnit>()) return;
            bool hooded = Phase == EPhase.Hooded;
            CurrentHealth = Mathf.Max(hooded ? 8 : 0, CurrentHealth - damageData.Amount);
            nextDamageTime = Time.time + damageCooldown;
            OnTakeDamage.Raise(damageData);
            PlayHitFeedback(damageData);
            if (!hooded)
            {
                if (CurrentHealth > 0) return;
                Phase = EPhase.Dead;
                if (live != null) StopCoroutine(live);
                hazards.ClearHazards();
                hitbox.enabled = false;
                feet.enabled = false;
                OnDeath.Raise(damageData);
                return;
            }
            if (CurrentHealth > 8) return;
            // Interrupt even mid-levitation; no old warning or projectile may hit during ice.
            Phase = EPhase.Freezing;
            if (live != null) StopCoroutine(live);
            hazards.ClearHazards();
            hitbox.enabled = false;
            VisualLift = 0f;
            hitbox.offset = new Vector2(0f, 1f);
            feet.offset = new Vector2(0f, 0.25f);
            live = StartCoroutine(IceIntermission());
        }

        private void PlayHitFeedback(SDamageData damageData)
        {
            // Reuse the same damage SFX, impact prefab, shake profile and knockback as the old enemies.
            if (damageData.Source.transform.TryGetComponentInHierarchy(out IDamageable damageable))
            {
                Vector2 away = (damageData.Source.transform.position - transform.position).normalized;
                if (away.sqrMagnitude < 0.01f) away = Vector2.down;
                damageable.WithKnockback(away * knockbackForce, 0f);
            }
            if (damagedSfx?.clip)
                SoundManager.Instance.CreateSoundBuilder().WithPosition(transform.position).WithRandomPitch().Play(damagedSfx);
            if (impactShockwavePrefab)
                ObjectPoolManager.Instance.SpawnObject(impactShockwavePrefab.gameObject, transform.position,
                    Quaternion.identity, ObjectPoolManager.PoolType.VFX);
            if (shakeProfile)
            {
                shakeTween?.Kill();
                Vector3 origin = transform.position;
                shakeTween = transform.DOShakePosition(shakeProfile.useWithHitStop ? hitStopDuration : shakeProfile.defaultShakeDuration,
                        shakeProfile.shakeStrength, shakeProfile.vibration, shakeProfile.randomness,
                        shakeProfile.snapping, shakeProfile.fadeOut)
                    .SetUpdate(UpdateType.Normal, true)
                    .OnKill(() => { if (this) transform.position = origin; });
            }
            flashTween?.Kill();
            flashTween = DOVirtual.Float(0f, 1f, 0.25f, SetFlash).SetEase(Ease.OutExpo)
                .SetLoops(2, LoopType.Yoyo).SetUpdate(true).OnKill(() => SetFlash(0f));
            HitStop.Stop(hitStopDuration, () =>
            {
                if (!this || !isActiveAndEnabled) return;
                // Enemy OnDamagedTaken clears active bullets after hit-stop. Keep that reward for a boss ram.
                hazards.ClearProjectiles();
                if (TryGetComponent<BossPhaseTwoEffects>(out var effects)) effects.ClearProjectiles();
                ProjectileManager.Instance.ClearAllEmittersProjectiles();
            });
        }

        private void SetFlash(float value)
        {
            if (!sprite || flash == null) return;
            sprite.GetPropertyBlock(flash);
            flash.SetFloat(HitEffectAmount, value);
            sprite.SetPropertyBlock(flash);
        }

        private IEnumerator Live()
        {
            while (Phase == EPhase.Hooded)
            {
                float idleUntil = Time.time + Random.Range(idleSeconds.x, idleSeconds.y);
                while (Time.time < idleUntil)
                {
                    yield return Play(idle, FacePlayer);
                }

                if (!player || !player.gameObject.activeInHierarchy) { yield return null; continue; }
                lastAction = PickAction();
                switch (lastAction)
                {
                    case EAction.Cast:
                        yield return ThrowProjectiles(false);
                        break;
                    case EAction.WalkThrow:
                        yield return ThrowProjectiles(true);
                        break;
                    case EAction.Levitate:
                        FacePlayer();
                        yield return Levitate();
                        break;
                    default:
                        //every weight is 0: just idle
                        break;
                }
            }
        }

        /// <summary>A weighted pick that never repeats the last action while another one is allowed.</summary>
        private EAction? PickAction()
        {
            float Weight(EAction action) => action switch
            {
                EAction.Cast => castWeight,
                EAction.WalkThrow => walkThrowWeight,
                _ => levitateWeight,
            };

            var actions = (EAction[])Enum.GetValues(typeof(EAction));
            float total = 0f;
            int allowed = 0;
            foreach (var action in actions)
            {
                if (Weight(action) <= 0f) continue;
                total += Weight(action);
                allowed++;
            }
            if (allowed == 0) return null;
            if (allowed > 1 && lastAction.HasValue) total -= Mathf.Max(0f, Weight(lastAction.Value));

            float roll = Random.value * total;
            EAction? picked = null;
            foreach (var action in actions)
            {
                if (Weight(action) <= 0f || (allowed > 1 && action == lastAction)) continue;
                picked = action;
                roll -= Weight(action);
                if (roll <= 0f) break;
            }
            return picked;
        }

        private IEnumerator ThrowProjectiles(bool walking)
        {
            do { CurrentPattern = (EPattern)Random.Range(0, 3); } while (previousPattern == CurrentPattern);
            previousPattern = CurrentPattern;
            Clip clip = walking ? walkThrow : cast;
            int loops = Random.Range(walkLoops.x, walkLoops.y + 1);
            FacePlayer();
            float nextShot = Time.time + 0.25f;
            int volley = 0;
            float interval = CurrentPattern == EPattern.LongStream ? 0.20f : CurrentPattern == EPattern.Circle ? 0.48f : 0.42f;
            // Lock a stream's aim for the whole throw so the player can step out of its trail.
            Vector2 aim = ((Vector2)player.transform.position - ((Vector2)transform.position + Vector2.up * 0.25f)).normalized;
            for (int loop = 0; loop < loops; loop++)
            {
                for (int frame = 0; frame < clip.frames.Length; frame++)
                {
                    sprite.sprite = clip.frames[frame];
                    float end = Time.time + clip.FrameSeconds(frame);
                    while (Time.time < end)
                    {
                        if (frame >= 3 && frame <= 9 && Time.time >= nextShot)
                        {
                            FireVolley(aim, volley++);
                            nextShot = Time.time + interval;
                        }
                        yield return null;
                    }
                }
            }
        }

        private void FireVolley(Vector2 aim, int volley)
        {
            EColor color = volley % 2 == 0 ? EColor.Red : EColor.Blue;
            Vector2 origin = (Vector2)transform.position + Vector2.up * 0.25f;
            if (CurrentPattern == EPattern.LongStream)
            {
                Vector2 side = new Vector2(-aim.y, aim.x);
                for (int lane = -2; lane <= 2; lane++)
                    hazards.FireProjectile(origin + side * (lane * 0.24f), aim, color, projectileSpeed);
            }
            else if (CurrentPattern == EPattern.Circle)
            {
                for (int spoke = 0; spoke < 32; spoke++)
                    hazards.FireProjectile(origin, Rotate(Vector2.up, spoke * 360f / 32 + volley * 4f), color, projectileSpeed * 0.85f);
            }
            else
            {
                for (int group = 0; group < 5; group++)
                    for (int spoke = -2; spoke <= 2; spoke++)
                        hazards.FireProjectile(origin, Rotate(aim, group * 72f + spoke * 5.5f + volley * 6f),
                            group % 2 == 0 ? color : color == EColor.Red ? EColor.Blue : EColor.Red, projectileSpeed * 0.9f);
            }
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            float angle = degrees * Mathf.Deg2Rad;
            return new Vector2(vector.x * Mathf.Cos(angle) - vector.y * Mathf.Sin(angle),
                vector.x * Mathf.Sin(angle) + vector.y * Mathf.Cos(angle));
        }

        private IEnumerator Levitate()
        {
            for (int frame = 0; frame < levitate.frames.Length; frame++)
            {
                SetLift(frame);
                yield return Show(levitate, frame);
            }

            int first = Mathf.Max(0, levitate.frames.Length - hoverLoopFrames);
            float nextWave = Time.time;
            int wave = 0;
            for (float end = Time.time + hoverSeconds; Time.time < end;)
            {
                for (int frame = first; frame < levitate.frames.Length && Time.time < end; frame++)
                {
                    if (Time.time >= nextWave && player && player.gameObject.activeInHierarchy)
                    {
                        Vector2 target = player.transform.position;
                        for (int strike = 0; strike < 3; strike++)
                        {
                            Vector2 position = strike == 0 ? target : target + Rotate(Vector2.right,
                                wave * 53f + strike * 120f) * Random.Range(2.8f, 4.2f);
                            hazards.WarnLightning(position, (wave + strike) % 2 == 0 ? EColor.Red : EColor.Blue, (wave + strike) % 2);
                        }
                        wave++;
                        nextWave = Time.time + 0.75f;
                    }
                    SetLift(frame);
                    yield return Show(levitate, frame);
                }
            }

            //the pack has no landing: the rise played backwards brings him down
            for (int frame = levitate.frames.Length - 1; frame >= 0; frame--)
            {
                SetLift(frame);
                yield return Show(levitate, frame);
            }
            VisualLift = 0f;
            hitbox.offset = new Vector2(0f, 1f);
            feet.offset = new Vector2(0f, 0.25f);
        }

        private void SetLift(int frame)
        {
            VisualLift = Mathf.Max(0, frame - 2) / 12.8f;
            hitbox.offset = new Vector2(0f, 1f + VisualLift);
            feet.offset = new Vector2(0f, 0.25f + VisualLift);
        }

        private IEnumerator IceIntermission()
        {
            FacePlayer();
            int hold = Mathf.Clamp(frozenFrame, 0, iceShatter.frames.Length - 2);
            for (int frame = 0; frame <= hold; frame++) yield return Show(iceShatter, frame);
            Phase = EPhase.Frozen;
            // Siblings, never children: projectile hierarchy damage lookups must not reach the boss.
            for (int i = 0; i < summonPrefabs.Length; i++)
            {
                Vector2 position = (Vector2)transform.position + Rotate(Vector2.up, i * 360f / summonPrefabs.Length) * summonRadius;
                if (player && Vector2.Distance(position, player.transform.position) < 3.5f)
                {
                    Vector2 away = position - (Vector2)player.transform.position;
                    if (away.sqrMagnitude < 0.01f) away = Rotate(Vector2.up, i * 360f / summonPrefabs.Length);
                    position += away.normalized * 3.5f;
                }
                var arena = FindAnyObjectByType<BossArenaController>();
                if (arena) position = arena.ClampPosition(position);
                minions.Add(Instantiate(summonPrefabs[i], position, Quaternion.identity, transform.parent));
                yield return new WaitForSeconds(summonSpacingSeconds);
            }
            yield return new WaitUntil(() => minions.TrueForAll(enemy => !enemy || !enemy.activeInHierarchy));
            Phase = EPhase.Shattering;
            yield return RevealPhaseTwo(false);
        }

        private IEnumerator RevealPhaseTwo(bool skipIce)
        {
            var arena = FindAnyObjectByType<BossArenaController>();
            if (arena)
            {
                arena.BeginPhaseTwoPresentation();
                yield return arena.ZoomInOnBoss();
            }
            int first = skipIce ? 0 : Mathf.Clamp(frozenFrame, 0, iceShatter.frames.Length - 2) + 1;
            for (int frame = first; frame < iceShatter.frames.Length; frame++)
            {
                if (frame == 6) Phase = EPhase.Revealing; //pack's ChangeBossPhase: frame 7 (1240 ms)
                yield return Show(iceShatter, frame);
            }
            sprite.sprite = phaseTwoRestingPose ? phaseTwoRestingPose : redHair;
            if (arena) yield return arena.ZoomOutFromBoss();
            Phase = EPhase.RedHaired;
            nextDamageTime = Time.time;
            hitbox.enabled = true;
            OnPhaseTwoReady.Raise();
        }

        private IEnumerator Play(Clip clip, Action onFrame = null)
        {
            for (int frame = 0; frame < clip.frames.Length; frame++)
            {
                var wait = Show(clip, frame);
                onFrame?.Invoke();
                yield return wait;
            }
        }

        private WaitForSeconds Show(Clip clip, int frame)
        {
            sprite.sprite = clip.frames[frame];
            return new WaitForSeconds(clip.FrameSeconds(frame));
        }

        private void FacePlayer()
        {
            //the sheets are drawn facing right
            if (player && player.gameObject.activeInHierarchy)
                sprite.flipX = player.transform.position.x < transform.position.x;
        }
    }
}
