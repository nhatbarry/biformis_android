using CaptainPinkTurd.AnimationSystem;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.BulletHell;
using CaptainPinkTurd.Core.Attributes;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Extensions;
using PathCreation;
using UnityEngine;

namespace CaptainPinkTurd.Game.Enemy
{
    public class Spider : BiformisEmitterController
    {
        [Header("Spider Movement Configs")]
        [SerializeField] private float speed = 5;
        [SerializeField] private bool rotateAlongPath;
        [SerializeField][ReadOnly] private float distanceTravelled;
        [Tooltip("Optional: follow this path. When empty, a random path in the scene is picked")]
        [SerializeField] private PathCreator assignedPath;
        
        [Header("Spider Animation Clips")]
        [SerializeField] private SerializeKeyValuePair<EColor, AnimationClip>[] idleAnimationClips;
        [SerializeField] private SerializeKeyValuePair<EColor, AnimationClip>[] moveAnimationClips;
        [SerializeField] private BasicVfxAnimationController spawnVfx;
        
        private PathCreator pathCreator;
        private SpriteRenderer sr;
        private AnimationClip currentIdleAnim;
        private AnimationClip currentMoveAnim;
        private readonly EndOfPathInstruction[] closedPathInstruction = { EndOfPathInstruction.Loop, EndOfPathInstruction.Reverse };
        private EndOfPathInstruction endOfPathInstruction;
        
        private float initialDistanceTravelledOffset;
        private bool runAnimationIsPlaying;
        private bool isRunning;
        
        protected override void Awake()
        {
            base.Awake();
            
            sr = GetComponent<SpriteRenderer>();
            Coll.isTrigger = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            ToggleSpider(false);
            InitializePath();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            pathCreator.pathUpdated -= OnPathChanged;
            pathCreator = null;
        }

        void Update()
        {
            //having a damageSource means Spider is currently being impacted by taking damage, so it should stop its movement for a moment
            if (!pathCreator || !isRunning || damageSource) return;

            if (!runAnimationIsPlaying)
            {
                runAnimationIsPlaying = true;
                PlayAnimation(Animator.StringToHash(currentMoveAnim.name),
                    onAnimationEnd: () => runAnimationIsPlaying = false);
            }
            
            distanceTravelled += speed * Time.deltaTime;
            transform.position = pathCreator.path.GetPointAtDistance(distanceTravelled, endOfPathInstruction);
            
            if (rotateAlongPath)
            {
                transform.rotation = pathCreator.path.GetRotationAtDistance(distanceTravelled, endOfPathInstruction);
            }
        }

        private void ToggleSpider(bool on)
        {
            sr.enabled = on;
            ToggleEmitter(on);
            isRunning = on;
        }
        private void InitializePath()
        {
            if (assignedPath)
            {
                pathCreator = assignedPath;
            }
            else
            {
                var paths = FindObjectsByType<PathCreator>(FindObjectsSortMode.None);
                
                if(paths.Length <= 0)
                {
                    Debug.LogError("No path found in scene");
                    return;
                }
                
                int randomPathIndex = Random.Range(0, paths.Length);
                pathCreator = paths[randomPathIndex];
            }
            
            if (pathCreator.bezierPath.IsClosed)
            {
                int randomClosedPathIndex = Random.Range(0, closedPathInstruction.Length);
                endOfPathInstruction = closedPathInstruction[randomClosedPathIndex];
            }
            else
            {
                endOfPathInstruction = EndOfPathInstruction.Reverse;
            }
            
            pathCreator.pathUpdated += OnPathChanged;
            
            initialDistanceTravelledOffset = Random.Range(0f, pathCreator.path.length);
            distanceTravelled = initialDistanceTravelledOffset;
            transform.position = pathCreator.path.GetPointAtDistance(distanceTravelled, endOfPathInstruction);
            
            //spawnVfx.OnAnimationEnd.Subscribe(() => ToggleSpider(true));
            spawnVfx.gameObject.SetActive(true);
            ToggleSpider(true);
            SoundManager.Instance.CreateSoundBuilder()
                .WithPosition(transform.position).WithRandomPitch().Play(startUpSfx);
        }
        
        // If the path changes during the game, update the distance travelled so that the follower's position on the new path
        // is as close as possible to its position on the old path
        void OnPathChanged()
        {
            distanceTravelled = pathCreator.path.GetClosestDistanceAlongPath(transform.position);
        }
        protected override void OnColorChangeEvent(EColor color)
        {
            base.OnColorChangeEvent(color);

            if (idleAnimationClips.TryGetValue(color, out var idleAnim))
            {
                currentIdleAnim = idleAnim;
            }
            else
            {
                Debug.LogError($"No idle animation found for color: {color}");
            }
            if (moveAnimationClips.TryGetValue(color, out var moveAnim))
            {
                currentMoveAnim = moveAnim;
            }
            else
            {
                Debug.LogError($"No spawn animation found for color: {color}");
            }
        }
    }
}