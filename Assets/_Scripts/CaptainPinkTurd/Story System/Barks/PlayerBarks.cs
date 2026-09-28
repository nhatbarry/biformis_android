using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Game.Player;
using UnityEngine;

namespace CaptainPinkTurd.Story.Barks
{
    /// <summary>
    /// The player mutters while playing: one line when hurt, and another every so often.
    /// </summary>
    public class PlayerBarks : MonoBehaviour, IGameEventSOListener<Unit>
    {
        [SerializeField] private BarkSource barkSource;
        [SerializeField] private BarkBubble bubble;
        [SerializeField] private VoidEvent onPlayerDamaged;

        [Header("Knots")]
        [SerializeField] private string hurtKnot = "Bark_Hurt";
        [SerializeField] private string idleKnot = "Bark_Pain";

        [Header("Timing")]
        [SerializeField] private Vector2 idleIntervalRange = new(15f, 25f);
        [Tooltip("Keeps lines from piling up when the player is hit several times in a row")]
        [SerializeField] private float minSecondsBetweenBarks = 4f;

        private Transform player;
        private float nextIdleBarkTime;
        private float lastBarkTime = float.NegativeInfinity;

        private void OnEnable()
        {
            if (onPlayerDamaged) onPlayerDamaged.Subscribe(this);
        }

        private void OnDisable()
        {
            if (onPlayerDamaged) onPlayerDamaged.Unsubscribe(this);
        }

        private void Start()
        {
            var playerUnit = FindAnyObjectByType<PlayerUnit>();
            if (playerUnit) player = playerUnit.transform;
            ScheduleIdleBark();
        }

        private void Update()
        {
            if (Time.time < nextIdleBarkTime) return;

            TryBark(idleKnot);
            ScheduleIdleBark();
        }

        public void OnEventRaised(Unit data) => TryBark(hurtKnot);

        private void TryBark(string knot)
        {
            if (!player || !player.gameObject.activeInHierarchy) return;
            if (Time.time - lastBarkTime < minSecondsBetweenBarks) return;
            if (!barkSource.TryGetLine(knot, out var speaker, out var line)) return;

            lastBarkTime = Time.time;
            bubble.Show(player, speaker, line);
        }

        private void ScheduleIdleBark()
        {
            nextIdleBarkTime = Time.time + Random.Range(idleIntervalRange.x, idleIntervalRange.y);
        }
    }
}
