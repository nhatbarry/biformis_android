using System;
using CaptainPinkTurd.Game.Player;
using UnityEngine;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>Plays the supplied four-direction walk and attack sprites at their native frame durations.</summary>
    [DefaultExecutionOrder(100)]
    public class BossDirectionalArt : MonoBehaviour
    {
        public enum EView { Down, Right, Up, Left }
        [Serializable]
        public class DirectionClips
        {
            public EView view;
            public PlagueDoctorBoss.Clip walk, horizontalSlash, rangedCharge, dashStab, verticalSlash;
        }
        [SerializeField] private DirectionClips[] directions;
        private PlagueDoctorBoss boss;
        private BossPhaseTwoCombat combat;
        private SpriteRenderer art;
        private PlayerUnit player;
        private Vector2 previousPosition;
        private float walkElapsed;
        private float lastMovedAt = -1f;
        private MaterialPropertyBlock properties;
        private static readonly int BladeOn = Shader.PropertyToID("_BladeOn");
        private static readonly int BladeTint = Shader.PropertyToID("_BladeTint");
        public EView CurrentDirection { get; private set; }
        public bool HasAnimations => directions != null && directions.Length == 4;
        public SpriteRenderer[] VisibleRenderers { get; private set; }
        public Sprite CurrentView => art ? art.sprite : null;
        public Color KnifeColour
        {
            get { art.GetPropertyBlock(properties); return properties.GetColor(BladeTint); }
        }
        private void Awake()
        {
            boss = GetComponent<PlagueDoctorBoss>();
            combat = GetComponent<BossPhaseTwoCombat>();
            art = GetComponent<SpriteRenderer>();
            properties = new MaterialPropertyBlock();
            VisibleRenderers = new[] { art };
            previousPosition = transform.position;
        }
        public static EView ViewFor(Vector2 direction) => Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
            ? (direction.x < 0f ? EView.Left : EView.Right) : (direction.y > 0f ? EView.Up : EView.Down);
        public PlagueDoctorBoss.Clip AttackClip(BossPhaseTwoCombat.EAction action, Vector2 aim)
        {
            CurrentDirection = ViewFor(aim);
            var set = Array.Find(directions, value => value.view == CurrentDirection);
            return action switch
            {
                BossPhaseTwoCombat.EAction.HorizontalSlash => set.horizontalSlash,
                BossPhaseTwoCombat.EAction.RangedCharge => set.rangedCharge,
                BossPhaseTwoCombat.EAction.DashStab => set.dashStab,
                _ => set.verticalSlash,
            };
        }
        private void LateUpdate()
        {
            Vector2 position = transform.position;
            Vector2 movement = position - previousPosition;
            previousPosition = position;
            if (!HasAnimations || (boss.Phase != PlagueDoctorBoss.EPhase.RedHaired && boss.Phase != PlagueDoctorBoss.EPhase.Dead)) return;
            art.enabled = true;
            art.flipX = false;
            if (combat.CurrentAction.HasValue || Time.deltaTime <= 0f) return;
            if (movement.sqrMagnitude > 0.00001f)
            {
                lastMovedAt = Time.time;
                CurrentDirection = ViewFor(movement);
            }
            // Physics does not move the body on every render frame. Keep the walk between FixedUpdate ticks.
            bool moving = Time.time - lastMovedAt < 0.12f && boss.Phase != PlagueDoctorBoss.EPhase.Dead;
            if (moving)
            {
                walkElapsed = (walkElapsed + Time.deltaTime) % 0.8f;
            }
            else
            {
                walkElapsed = 0f;
                if (!player) player = FindAnyObjectByType<PlayerUnit>();
                if (player && boss.Phase != PlagueDoctorBoss.EPhase.Dead)
                    CurrentDirection = ViewFor((Vector2)player.transform.position - position);
            }
            var clip = Array.Find(directions, value => value.view == CurrentDirection).walk;
            float end = 0f;
            int frame = 0;
            for (; frame < clip.frames.Length - 1; frame++)
            {
                end += clip.FrameSeconds(frame);
                if (walkElapsed < end) break;
            }
            art.sprite = clip.frames[frame];
            art.GetPropertyBlock(properties);
            properties.SetFloat(BladeOn, 0f);
            art.SetPropertyBlock(properties);
        }
    }
}
