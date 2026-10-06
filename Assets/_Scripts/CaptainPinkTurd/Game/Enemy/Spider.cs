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
        //handed over by the encounter that spawned this spider; cleared when it goes back to the pool
        private PathCreator encounterPath;
        private bool pathInitialized;
        private SpriteRenderer sr;
        private AnimationClip currentIdleAnim;
        private AnimationClip currentMoveAnim;
        private readonly EndOfPathInstruction[] closedPathInstruction = { EndOfPathInstruction.Loop, EndOfPathInstruction.Reverse };
        private EndOfPathInstruction endOfPathInstruction;
        
        private float initialDistanceTravelledOffset;
        private bool runAnimationIsPlaying;
        private bool isRunning;

        public PathCreator CurrentPath => pathCreator;
        
        /// <summary>
        /// Called by the encounter that spawned this spider, right after spawning it and before its first Update.
        /// </summary>
        public void AssignPath(PathCreator path) => encounterPath = path;
        
        protected override void Awake()
        {
            base.Awake();
            
            sr = GetComponent<SpriteRenderer>();
            Coll.isTrigger = true;
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            //the path is picked on the first Update: Instantiate/SetActive run OnEnable before the spawner gets this
            //spider back, so an encounter can only hand its path over after this
            ToggleSpider(false);
            pathInitialized = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            
            if (pathCreator) pathCreator.pathUpdated -= OnPathChanged;
            pathCreator = null;
            //a pooled spider keeps its fields: when reused, it mustn't walk the last encounter's path
            encounterPath = null;
        }

        void Update()
        {
            if (!pathInitialized) InitializePath();
            
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
            pathInitialized = true;
            
            if (encounterPath)
            {
                pathCreator = encounterPath;
            }
            else if (assignedPath)
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
            
            //an encounter's spider starts where it was spawned, on the nearest point of its path; others anywhere along theirs
            initialDistanceTravelledOffset = encounterPath
                ? pathCreator.path.GetClosestDistanceAlongPath(transform.position)
                : Random.Range(0f, pathCreator.path.length);
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