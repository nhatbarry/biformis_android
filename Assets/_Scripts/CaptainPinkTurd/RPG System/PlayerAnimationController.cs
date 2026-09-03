using CaptainPinkTurd.AnimationSystem;
using CaptainPinkTurd.Core.Attributes;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Variables;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Extensions;
using UnityEngine;
using ZLinq;
using Random = UnityEngine.Random;
using Vector2 = UnityEngine.Vector2;

namespace CaptainPinkTurd.RPG
{
    public class PlayerAnimationController : AnimationControllerBase
    {
        [Header("Player Animation Clips")] 
        [SerializeField] private SerializeKeyValuePair<EDirection2D, AnimationClip>[] idleAnimationClips; 
        [SerializeField] private SerializeKeyValuePair<EDirection2D, AnimationClip>[] walkAnimationClips;
        [Tooltip("The direction specify to flip the player sprite if there is any")]
        [SerializeField] private EDirection2D[] spriteFlipDirections; 
        
        [Header("Input Events")]
        [SerializeField] private Vector2VariableSO currentMovementInput;
        [SerializeField] private EDirectionMode directionMode;
        [SerializeField][ReadOnly] private EDirection2D playerCurrentDirectionState;
        
        private bool isMoving;
        private bool canChangeDirectionState = true;
        private int playingAnimationHash;
        
        public override int DefaultAnimationHash { get; set; }

        protected override void Awake()
        {
            base.Awake();
            
            //instead of Subscribe and Unsubscribe in OnEnable and OnDisable
            //we're doing this here to ensure that the event is constantly being called to update our playerCurrentDirectionState all the time
            currentMovementInput.OnValueChanged += OnMovementInputChangeEvent;
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // The Animator is rebound on enable, so nothing is playing yet whatever we last asked for.
            playingAnimationHash = 0;

            OnMovementInputChangeEvent(currentMovementInput.Value);
        }

        private void OnDestroy()
        {
            currentMovementInput.OnValueChanged -= OnMovementInputChangeEvent;
        }

        private Vector2 SnapDiagonal(Vector2 input, EDirectionMode mode)
        {
            //is player is already moving then there's no need to snap diagonal to get the right animation
            if (input.x == 0 || input.y == 0 || isMoving) return input;

            input = input.With(Mathf.Sign(input.x), Mathf.Sign(input.y));

            if (mode == EDirectionMode.FourDirectional)
            {
                if (Random.value < 0.5f)
                {
                    input.x = 0;
                }
                else
                {
                    input.y = 0;
                }
            }

            return input;
        }
        public void SetCanChangeDirectionState(bool value) => canChangeDirectionState = value;
        private void OnMovementInputChangeEvent(Vector2 input)
        {
            if(!canChangeDirectionState) return;
            
            input = SnapDiagonal(input, directionMode);

            UpdateDirectionState(input);

            CheckForSpriteFlip();
            if (input == Vector2.zero)
            {
                SetPlayerIdleAnimation();
            }
            else
            {
                SetPlayerWalkAnimation();
            }
        }

        /// <summary>
        /// Picks the facing that best matches the input.
        /// </summary>
        /// <remarks>
        /// A keyboard hands us exact cardinals like (1, 0), but an analog stick hands us things like
        /// (0.998, 0.021), which is equal to no direction at all. Comparing for equality therefore left the
        /// character facing whichever way it happened to be facing last - walk left, then flick the stick
        /// right, and it walked right while still facing left. Every candidate is a unit vector, so the
        /// largest dot product is the closest direction, and an exact cardinal still picks itself.
        /// </remarks>
        private void UpdateDirectionState(Vector2 input)
        {
            if (input == Vector2.zero) return; // no input keeps whatever we were facing, as before

            var directions = directionMode.GetDirections();
            var bestDot = float.NegativeInfinity;

            foreach (var dir in directions)
            {
                var candidate = dir.ToVector2();
                if (candidate == Vector2.zero) continue;

                var dot = Vector2.Dot(candidate, input);
                if (dot <= bestDot) continue;

                bestDot = dot;
                playerCurrentDirectionState = dir;
            }
        }

        private void CheckForSpriteFlip()
        {
            var spriteFlip = spriteFlipDirections.AsValueEnumerable().Contains(playerCurrentDirectionState);
            
            spriteRenderer.flipX = spriteFlip;
        }

        private void SetPlayerIdleAnimation()
        {
            if (idleAnimationClips.TryGetValue(playerCurrentDirectionState, out var idleAnim))
            {
                isMoving = false;
                PlayIfNotAlreadyPlaying(Animator.StringToHash(idleAnim.name));
            }
            else
            {
                Debug.LogWarning("Idle animation not found for direction: " + playerCurrentDirectionState);
            }
        }
        private void SetPlayerWalkAnimation()
        {
            if (walkAnimationClips.TryGetValue(playerCurrentDirectionState, out var walkAnim))
            {
                isMoving = true;
                PlayIfNotAlreadyPlaying(Animator.StringToHash(walkAnim.name));
            }
            else
            {
                Debug.LogWarning("Walk animation not found for direction: " + playerCurrentDirectionState);
            }
        }

        /// <summary>
        /// Starts an animation only when it is not the one already running.
        /// </summary>
        /// <remarks>
        /// PlayAnimation cross-fades from normalized time 0, so calling it again with the clip that is already
        /// playing restarts it from the first frame. A keyboard hides this: Move is a Dpad composite, so the
        /// input value only changes when a key goes down or up, and the animation is re-requested a handful of
        /// times. An analog stick changes value every single frame it moves, which re-requested the same walk
        /// clip every frame and pinned the character on frame one - the animation looked like it had been lost.
        /// </remarks>
        private void PlayIfNotAlreadyPlaying(int animationHash)
        {
            if (animationHash == playingAnimationHash) return;

            playingAnimationHash = animationHash;
            PlayAnimation(animationHash);
        }
    }
}