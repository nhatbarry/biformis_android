using CaptainPinkTurd.AnimationSystem;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Core.InputPaths;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaptainPinkTurd.Game
{
    /// <summary>
    /// The level's exit. Once open, the player goes through by standing at it and pressing Interact (E, or the touch
    /// HUD's "!" button, which lights up while the player is in reach) - walking into it no longer takes them through.
    /// </summary>
    public class Door : AnimationControllerBase
    {
        [Header("Door Configs")]
        [SerializeField] private LayerMask playerLayers;
        [SerializeField] private VoidEvent onDoorEnter;
        [SerializeField] private SoundData openSfx;
        [SerializeField] private SoundData enterSfx;
        [Tooltip("How far from the door's opening (its trigger) any of the player's colliders may be for Interact to enter")]
        [SerializeField] private float interactRange = 1.25f;

        [Header("Door Animations")]
        [SerializeField] private AnimationClip doorCloseAnimation;
        [SerializeField] private AnimationClip doorOpeningAnimation;
        [SerializeField] private AnimationClip doorOpenAnimation;

        public override int DefaultAnimationHash { get; set; }

        private InputAction interactAction;
        private Collider2D opening;
        private bool isOpen;
        private bool openRequested;
        private bool playerInReach;
        private bool entered;

        public bool IsOpen => isOpen;
        public bool PlayerInReach => playerInReach;

        protected override void OnEnable()
        {
            base.OnEnable();

            //its own action, like GameManager's dimension switch: the E key and the touch HUD's interact button
            interactAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/e");
            interactAction.AddBinding(MobileControlPaths.Interact);
            interactAction.performed += OnInteract;
            interactAction.Enable();
        }
        protected override void OnDisable()
        {
            base.OnDisable();

            interactAction.performed -= OnInteract;
            interactAction.Dispose();
            interactAction = null;
            SetInReach(false);
        }

        protected override void Start()
        {
            base.Start();

            //OpenDoor can run before this Start (a level with no fight opens its door on load); closing here
            //would dispose the opening animation's timer and leave the door shut for good
            if (openRequested) return;

            PlayAnimation(Animator.StringToHash(doorCloseAnimation.name));
            isOpen = false;
        }

        private void Update() => SetInReach(isOpen && !entered && IsPlayerNear());

        public void OpenDoor()
        {
            openRequested = true;
            SoundManager.Instance.CreateSoundBuilder()
                .WithPosition(transform.position).WithRandomPitch().Play(openSfx);

            PlayAnimation(Animator.StringToHash(doorOpeningAnimation.name), onAnimationEnd: () =>
            {
                isOpen = true;
                PlayAnimation(Animator.StringToHash(doorOpenAnimation.name), isClamp: true);
            });
        }

        /// <summary>Any of the player's colliders within interactRange of the door's opening.</summary>
        public bool IsPlayerNear()
        {
            if (!opening) opening = GetComponent<Collider2D>();
            Vector2 centre = opening ? opening.bounds.center : transform.position;
            return Physics2D.OverlapCircle(centre, interactRange, playerLayers);
        }

        private void OnInteract(InputAction.CallbackContext context)
        {
            if (!playerInReach || entered) return;
            entered = true;
            SetInReach(false);

            SoundManager.Instance.CreateSoundBuilder().WithPosition(transform.position).WithRandomPitch().Play(enterSfx);
            onDoorEnter.Raise();
        }

        private void SetInReach(bool reach)
        {
            if (reach == playerInReach) return;
            playerInReach = reach;
            InteractPrompt.SetInReach(this, reach);
        }

        private void OnDrawGizmosSelected()
        {
            var trigger = GetComponent<Collider2D>();
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(trigger ? trigger.bounds.center : transform.position, interactRange);
        }
    }
}
