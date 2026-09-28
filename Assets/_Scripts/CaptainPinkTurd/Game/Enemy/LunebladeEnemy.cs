using System.Collections;
using CaptainPinkTurd.AnimationSystem;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.BulletHell;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Game.Player;
using UnityEngine;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>
    /// The Luneblade characters as Biformis enemies. They cycle red/blue like every other enemy, and their own attack
    /// follows the bullet rule: it only hurts a player of the other colour (and never a dashing one). The player
    /// destroys them the usual way, by ramming them.
    ///   Chaser  (Reaper): runs at the player and slashes up close.
    ///   Bruiser (Axion) : closes to mid range, then smashes, firing its current-colour emitter at the player.
    ///   Blinker (Riven) : vanishes in smoke, reappears near the player and lunges.
    /// Animated straight from sprite-sheet frames, so it has no Animator.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class LunebladeEnemy : BiformisEmitterController
    {
        public enum EBehaviour
        {
            Chaser,
            Bruiser,
            Blinker,
        }

        [Header("Behaviour")]
        [SerializeField] private EBehaviour behaviour;
        [SerializeField] private float moveSpeed = 3f;
        [Tooltip("Starts its attack once the player is this close")]
        [SerializeField] private float attackRange = 1.4f;
        [Tooltip("A melee hit lands if the player is within this distance")]
        [SerializeField] private float hitRadius = 1.3f;
        [SerializeField] private float recoverSeconds = 0.7f;
        [Tooltip("Stands still this long after spawning, so the player can see it arrive")]
        [SerializeField] private float spawnSeconds = 0.8f;
        [SerializeField] private BasicVfxAnimationController spawnVfx;
        [SerializeField] private SoundData attackSfx;

        [Header("Blinker")]
        [SerializeField] private Vector2 blinkDistance = new(2.5f, 3.5f);
        [SerializeField] private float lungeSpeed = 10f;
        [SerializeField] private float lungeSeconds = 0.22f;
        [Tooltip("What a blink may not land in or see through")]
        [SerializeField] private LayerMask blinkBlockers = 1 << 0 | 1 << 6 | 1 << 7;

        [Header("Sprites")]
        [SerializeField] private float framesPerSecond = 12f;
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private Sprite[] runFrames;
        [SerializeField] private Sprite[] attackFrames;
        [Tooltip("Frame of the attack on which the hit (or the shot) happens")]
        [SerializeField] private int attackHitFrame = 5;
        [SerializeField] private Sprite[] vanishFrames;
        [SerializeField] private Sprite[] appearFrames;
        [SerializeField] private Sprite[] lungeFrames;

        [Header("Colour")]
        [SerializeField] private Color redTint = new(1f, 0.72f, 0.72f);
        [SerializeField] private Color blueTint = new(0.7f, 1f, 0.95f);

        public override int DefaultAnimationHash { get; set; }

        private Rigidbody2D body;
        private SpriteRenderer sprite;
        private PlayerUnit player;
        private IDamageable playerHealth;
        private int redLayer, blueLayer;

        private Sprite[] frames;
        private bool loopFrames;
        private float frameTime;

        private bool PlayerAvailable => player && player.gameObject.activeInHierarchy;
        private Vector2 PlayerPosition => player.transform.position;
        private float PlayerDistance => Vector2.Distance(body.position, PlayerPosition);

        protected override void Awake()
        {
            base.Awake();
            body = GetComponent<Rigidbody2D>();
            sprite = GetComponent<SpriteRenderer>();
            redLayer = LayerMask.NameToLayer("Red");
            blueLayer = LayerMask.NameToLayer("Blue");
            Coll.isTrigger = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable(); //colour cycle and health; pooled copies come through here again on every spawn

            Coll.enabled = true;
            sprite.enabled = true;
            body.linearVelocity = Vector2.zero;
            player = FindAnyObjectByType<PlayerUnit>();
            playerHealth = player ? player.GetComponent<IDamageable>() : null;
            StartCoroutine(Live());
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAllCoroutines();
            if (body) body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;

            frameTime += Time.deltaTime;
            int index = (int)(frameTime * framesPerSecond);
            sprite.sprite = frames[loopFrames ? index % frames.Length : Mathf.Min(index, frames.Length - 1)];
        }

        protected override void OnColorChangeEvent(EColor color)
        {
            base.OnColorChangeEvent(color);

            //these characters only ever fire on purpose, from their attack
            redEmitter.AutoFire = false;
            blueEmitter.AutoFire = false;
            if (sprite) sprite.color = color == EColor.Red ? redTint : blueTint;
        }

        // ------------------------------------------------------------------ behaviour

        private IEnumerator Live()
        {
            if (spawnVfx) spawnVfx.gameObject.SetActive(true);
            SoundManager.Instance.CreateSoundBuilder().WithPosition(transform.position).WithRandomPitch().Play(startUpSfx);
            Play(idleFrames, true);
            yield return new WaitForSeconds(spawnSeconds);

            while (true)
            {
                if (!PlayerAvailable)
                {
                    Stop();
                    yield return null;
                    continue;
                }

                switch (behaviour)
                {
                    case EBehaviour.Chaser:
                        yield return Approach();
                        yield return Attack(melee: true, shoot: false);
                        break;
                    case EBehaviour.Bruiser:
                        yield return Approach();
                        yield return Attack(melee: true, shoot: true);
                        break;
                    case EBehaviour.Blinker:
                        yield return Blink();
                        yield return Lunge();
                        break;
                }

                Stop();
                Play(idleFrames, true);
                yield return new WaitForSeconds(recoverSeconds);
            }
        }

        private IEnumerator Approach()
        {
            Play(runFrames, true);
            while (PlayerAvailable && PlayerDistance > attackRange)
            {
                var direction = (PlayerPosition - body.position).normalized;
                body.linearVelocity = direction * moveSpeed;
                Face(PlayerPosition);
                yield return new WaitForFixedUpdate();
            }
            Stop();
        }

        private IEnumerator Attack(bool melee, bool shoot)
        {
            if (!PlayerAvailable) yield break;

            Face(PlayerPosition);
            Play(attackFrames, false);
            yield return new WaitForSeconds(attackHitFrame / framesPerSecond);
            if (!PlayerAvailable) yield break;

            SoundManager.Instance.CreateSoundBuilder().WithPosition(transform.position).WithRandomPitch().Play(attackSfx);
            if (melee) TryHitPlayer(hitRadius);
            if (shoot) CurrentEmitter.FireProjectile((PlayerPosition - body.position).normalized, 0f);

            float rest = (attackFrames.Length - attackHitFrame) / framesPerSecond;
            if (rest > 0f) yield return new WaitForSeconds(rest);
        }

        private IEnumerator Blink()
        {
            Play(idleFrames, true);
            yield return new WaitForSeconds(0.5f);

            //can't be rammed while in smoke
            Coll.enabled = false;
            Play(vanishFrames, false);
            yield return new WaitForSeconds(vanishFrames.Length / framesPerSecond);

            if (PlayerAvailable && TryFindBlinkSpot(out var spot))
            {
                body.position = spot;
                transform.position = spot;
            }

            Face(PlayerAvailable ? PlayerPosition : body.position);
            Play(appearFrames, false);
            yield return new WaitForSeconds(appearFrames.Length / framesPerSecond);
            Coll.enabled = true;

            Play(idleFrames, true);
            yield return new WaitForSeconds(0.25f); //a beat to react before the lunge
        }

        private IEnumerator Lunge()
        {
            if (!PlayerAvailable) yield break;

            var direction = (PlayerPosition - body.position).normalized;
            Face(PlayerPosition);
            Play(lungeFrames, true);
            SoundManager.Instance.CreateSoundBuilder().WithPosition(transform.position).WithRandomPitch().Play(attackSfx);

            bool hit = false;
            for (float t = 0f; t < lungeSeconds; t += Time.fixedDeltaTime)
            {
                body.linearVelocity = direction * lungeSpeed;
                if (!hit && PlayerAvailable) hit = TryHitPlayer(0.7f);
                yield return new WaitForFixedUpdate();
            }
            Stop();
        }

        private bool TryFindBlinkSpot(out Vector2 spot)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                var direction = Random.insideUnitCircle.normalized;
                spot = PlayerPosition + direction * Random.Range(blinkDistance.x, blinkDistance.y);

                if (Physics2D.OverlapCircle(spot, 0.45f, blinkBlockers)) continue;
                if (Physics2D.Linecast(PlayerPosition, spot, blinkBlockers)) continue; //stay in the player's room
                return true;
            }
            spot = body.position;
            return false;
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// Same rule as the bullets: a red enemy hurts a blue player and vice versa. A dashing player is on the
        /// invincibility layer, so is never hurt.
        /// </summary>
        private bool TryHitPlayer(float radius)
        {
            if (playerHealth == null || PlayerDistance > radius) return false;

            int hurtableLayer = currentColor == EColor.Red ? blueLayer : redLayer;
            if (player.gameObject.layer != hurtableLayer) return false;

            playerHealth.TakeDamage(new SDamageData(1, gameObject));
            return true;
        }

        private void Play(Sprite[] newFrames, bool loop)
        {
            if (newFrames == null || newFrames.Length == 0) return;
            if (frames == newFrames && loopFrames && loop) return;

            frames = newFrames;
            loopFrames = loop;
            frameTime = 0f;
            sprite.sprite = frames[0];
        }

        private void Face(Vector2 target)
        {
            //the sheets are drawn facing right
            sprite.flipX = target.x < body.position.x;
        }

        private void Stop()
        {
            body.linearVelocity = Vector2.zero;
        }
    }
}
