using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.TopDownControllerSystem;
using UnityEngine;

namespace CaptainPinkTurd.Game
{
    public enum RoundUpType
    {
        Ceil,
        Floor
    }
    
    public class MovingWallTrap : BiformisWall
    {
        [Header("Movement")]
        [SerializeField] private float speed;
        [SerializeField] private EDirection2D moveDirection; 
        [SerializeField] private SerializeKeyValuePair<EDirection2D, RoundUpType>[] roundUpTypeOnStopForDirections;
        [SerializeField] private bool moveOnStart;
        [SerializeField] private SoundData movingSfx;
        [SerializeField] private SoundData crushSfx;
        [SerializeField] private LayerMask crushKillLayers;
        
        private const float HeadOnContactThreshold = 0.5f;

        private Vector3 startPoint;
        private SoundBuilder movingSfxBuilder;
        
        private bool isMoving;
        private bool isBacktrack;
        
        protected override void Awake()
        {
            base.Awake();
            
            EnableSortingGroups(false);

            startPoint = transform.position;
        }
        private void Start()
        {
            isMoving = moveOnStart;
            movingSfxBuilder = SoundManager.Instance.CreateSoundBuilder();
        }

        private void FixedUpdate()
        {
            if (!isMoving) return;
            
            Vector2 moveDir = GetMoveDirection();
            Vector2 newPos = rb.position + moveDir * (speed * Time.fixedDeltaTime);

            rb.MovePosition(newPos);

            if (!isBacktrack) return;

            var distanceToStart = Vector3.Distance(rb.position, startPoint);

            if (distanceToStart > 0.5f) return;
            
            CheckIfPlayerIsCrushed();
            OnMovingStop();
            
            EnableSortingGroups(false);
            rb.position = startPoint;
        }

        private Vector2 GetMoveDirection()
        {
            return moveDirection.ToVector2();
        }
        
        private void OnMovingStop()
        {
            var x = transform.localPosition.x;
            var y = transform.localPosition.y;

            if (roundUpTypeOnStopForDirections.TryGetValue(moveDirection, out var roundUpType))
            {
                switch (roundUpType)
                {
                    case RoundUpType.Ceil:
                        x = Mathf.CeilToInt(x);
                        break;
                    case RoundUpType.Floor:
                        x = Mathf.FloorToInt(x);
                        break;
                }
            }
            
            transform.localPosition = new Vector3(x, y, 0);
            
            isMoving = false;
            movingSfxBuilder.StopCurrentSoundEmitter();
            
            moveDirection = moveDirection.GetOpposite();
            isBacktrack = !isBacktrack;
        }
        private void CheckIfPlayerIsCrushed()
        {
            if (!player || !player.transform.TryGetComponentInHierarchy(out IDamageable damageable)) return;
            
            SoundManager.Instance.CreateSoundBuilder().WithPosition(rb.position).WithRandomPitch().Play(crushSfx);
            damageable.TakeDamage(new SDamageData(damageable.MaxHealth, gameObject));
        }
        private bool IsHeadOnContact(Collision2D other)
        {
            Vector2 moveDir = GetMoveDirection();

            for (int i = 0; i < other.contactCount; i++)
            {
                //sign-agnostic: the normal's side depends on which collider is "first", only its alignment with the movement matters
                if (Mathf.Abs(Vector2.Dot(other.GetContact(i).normal, moveDir)) > HeadOnContactThreshold) return true;
            }

            return false;
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (!blockingLayer.Contains(other.gameObject.layer) || !isMoving) return;
            //Debug.Log($"Collision with {other.gameObject.name}");

            if (!IsHeadOnContact(other)) return; //a flush edge or corner grazed while sliding is not an obstacle

            OnMovingStop();

            CheckIfPlayerIsCrushed();
        }

        protected override void OnCollisionStay2D(Collision2D other)
        {
            base.OnCollisionStay2D(other);
            
            if (!isMoving || !player || !player.transform.TryGetComponentInHierarchy(out PlayerFreeMovementTopDownController2D playerController)) return;

            Vector2 pushDir = GetMoveDirection();
            Vector2 dirToPlayer = ((Vector2)player.transform.position - rb.position).normalized;

            if (Vector2.Dot(pushDir, dirToPlayer) <= 0) return;
            
            playerController.AddExternalVelocity(pushDir);
        }

        protected override void OnTriggerStay2D(Collider2D other) //wall could potentially call this twice because it have 2 colliders
        {
            base.OnTriggerStay2D(other);
            
            if (crushKillLayers.Contains(other.gameObject.layer) && 
                other.gameObject.transform.TryGetComponentInHierarchy(out IDamageable damageable) &&
                isMoving)
            {
                SoundManager.Instance.CreateSoundBuilder().WithPosition(rb.position).WithRandomPitch().Play(crushSfx);
                damageable.TakeDamage(new SDamageData(damageable.MaxHealth, gameObject));
            }
            
            if (!tangibleLayer.Contains(other.gameObject.layer) || moveOnStart || isMoving) return;
            
            isMoving = true;
            movingSfxBuilder.WithPosition(rb.position).WithRandomPitch().Play(movingSfx);
            EnableSortingGroups(true);
        }
    }
}