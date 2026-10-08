using CaptainPinkTurd.Game.Player;
using UnityEngine;

namespace CaptainPinkTurd.Game.Enemy
{
    /// <summary>Shared movement in both combat phases, independent of the attack animation timeline.</summary>
    [RequireComponent(typeof(PlagueDoctorBoss))]
    public class BossRoaming : MonoBehaviour
    {
        private PlagueDoctorBoss boss;
        private BossPhaseTwoCombat combat;
        private BossArenaController arena;
        private Rigidbody2D body;
        private PlayerUnit player;
        private Vector2 destination;
        private float nextDestination;

        private void Awake()
        {
            boss = GetComponent<PlagueDoctorBoss>();
            combat = GetComponent<BossPhaseTwoCombat>();
            body = GetComponent<Rigidbody2D>();
        }
        private void Start()
        {
            arena = FindAnyObjectByType<BossArenaController>();
            player = FindAnyObjectByType<PlayerUnit>();
        }
        private void FixedUpdate()
        {
            if (!arena || !player || !player.gameObject.activeInHierarchy || arena.IsPresentingPhaseTwo ||
                (boss.Phase != PlagueDoctorBoss.EPhase.Hooded && boss.Phase != PlagueDoctorBoss.EPhase.RedHaired) ||
                (combat && combat.HoldsPosition)) return;
            if (Time.time >= nextDestination || Vector2.Distance(body.position, destination) < 0.35f)
            {
                // Mix pursuit with flanking, allowing melee opportunities without a fixed patrol route.
                destination = arena.ClampPosition((Vector2)player.transform.position + Random.insideUnitCircle * Random.Range(0.6f, 3f), 0.8f);
                nextDestination = Time.time + Random.Range(0.65f, 1.8f);
            }
            float speed = boss.Phase == PlagueDoctorBoss.EPhase.Hooded ? 0.9f : 1.4f;
            body.MovePosition(Vector2.MoveTowards(body.position, destination, speed * Time.fixedDeltaTime));
        }
    }
}
