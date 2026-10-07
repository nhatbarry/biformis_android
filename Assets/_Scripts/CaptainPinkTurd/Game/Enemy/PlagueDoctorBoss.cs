using System;
using System.Collections;
using CaptainPinkTurd.Game.Player;
using UnityEngine;
using Random = UnityEngine.Random;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>
    /// The plague doctor (B's second personality) as Level 6's boss. For now it only shows the team's boss pack
    /// (Sprites/Enemies/Plague Doctor Boss, from Boss_Assets): it idles facing the player, then does one of its actions
    /// at random - casting on the spot, walking while throwing, raising his arms and floating up, or freezing in ice
    /// that turns red and shatters into the red-haired form. It has no attacks, health or collider yet.
    /// Each clip keeps the frame durations the pack exported (manifest.json), so the sheets play as drawn.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PlagueDoctorBoss : MonoBehaviour
    {
        [Serializable]
        public class Clip
        {
            public Sprite[] frames;
            [Tooltip("How long each frame shows, in milliseconds (the pack's manifest.json)")]
            public int[] frameMilliseconds;

            public float FrameSeconds(int frame) => frameMilliseconds[frame] / 1000f;
        }

        [Header("Clips")]
        [SerializeField] private Clip idle;
        [SerializeField] private Clip cast;
        [SerializeField] private Clip walkThrow;
        [Tooltip("Ice closes over him, turns red and shatters, leaving the red-haired form")]
        [SerializeField] private Clip iceShatter;
        [SerializeField] private Sprite redHair;
        [Tooltip("Raises his arms and floats up. The pack has no sheet for it (group 04): rebuilt from its concept image")]
        [SerializeField] private Clip levitate;

        [Header("Behaviour")]
        [SerializeField] private Vector2 idleSeconds = new(1f, 2.5f);
        [Tooltip("Chance of each action against the others; 0 leaves it out")]
        [SerializeField] private float castWeight = 1f;
        [SerializeField] private float walkThrowWeight = 1f;
        [SerializeField] private float iceShatterWeight = 0.5f;
        [SerializeField] private float levitateWeight = 1f;
        [Tooltip("Walk-and-throw loops per walk")]
        [SerializeField] private Vector2Int walkLoops = new(2, 3);
        [Tooltip("The walk sheet runs in place; the pack's preview moves him one art pixel per frame")]
        [SerializeField] private int walkPixelsPerFrame = 1;
        [Tooltip("How far he may walk either side of where he was placed")]
        [SerializeField] private float walkHalfWidth = 3f;
        [Tooltip("How long the red-haired form stays before he goes back to idling")]
        [SerializeField] private float redHairSeconds = 2f;
        [Tooltip("How long he floats before coming back down")]
        [SerializeField] private float hoverSeconds = 2f;
        [Tooltip("While he floats, the levitate clip's last frames loop, bobbing him up and down")]
        [SerializeField] private int hoverLoopFrames = 4;

        private enum EAction
        {
            Cast,
            WalkThrow,
            IceShatter,
            Levitate,
        }

        private SpriteRenderer sprite;
        private PlayerUnit player;
        private float homeX;
        private EAction? lastAction;

        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
        }

        private void OnEnable()
        {
            homeX = transform.position.x;
            player = FindAnyObjectByType<PlayerUnit>();
            StartCoroutine(Live());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        private IEnumerator Live()
        {
            while (true)
            {
                float idleUntil = Time.time + Random.Range(idleSeconds.x, idleSeconds.y);
                while (Time.time < idleUntil)
                {
                    yield return Play(idle, FacePlayer);
                }

                lastAction = PickAction();
                switch (lastAction)
                {
                    case EAction.Cast:
                        FacePlayer();
                        yield return Play(cast);
                        break;
                    case EAction.WalkThrow:
                        yield return WalkThrow();
                        break;
                    case EAction.IceShatter:
                        FacePlayer();
                        yield return Play(iceShatter);
                        sprite.sprite = redHair;
                        yield return new WaitForSeconds(redHairSeconds);
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
                EAction.IceShatter => iceShatterWeight,
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

        private IEnumerator WalkThrow()
        {
            int loops = Random.Range(walkLoops.x, walkLoops.y + 1);
            float step = walkPixelsPerFrame / walkThrow.frames[0].pixelsPerUnit;
            float distance = loops * walkThrow.frames.Length * step;

            //head the way there is room for the whole walk, picking at random when both sides have it
            float x = transform.position.x;
            bool leftFits = x - distance >= homeX - walkHalfWidth;
            bool rightFits = x + distance <= homeX + walkHalfWidth;
            int direction = leftFits && rightFits ? (Random.value < 0.5f ? -1 : 1) : rightFits ? 1 : -1;
            sprite.flipX = direction < 0;

            for (int loop = 0; loop < loops; loop++)
            {
                yield return Play(walkThrow, () =>
                {
                    var position = transform.position;
                    position.x = Mathf.Clamp(position.x + direction * step, homeX - walkHalfWidth, homeX + walkHalfWidth);
                    transform.position = position;
                });
            }
        }

        private IEnumerator Levitate()
        {
            yield return Play(levitate);

            int first = Mathf.Max(0, levitate.frames.Length - hoverLoopFrames);
            for (float end = Time.time + hoverSeconds; Time.time < end;)
            {
                for (int frame = first; frame < levitate.frames.Length && Time.time < end; frame++)
                    yield return Show(levitate, frame);
            }

            //the pack has no landing: the rise played backwards brings him down
            for (int frame = levitate.frames.Length - 1; frame >= 0; frame--)
                yield return Show(levitate, frame);
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
