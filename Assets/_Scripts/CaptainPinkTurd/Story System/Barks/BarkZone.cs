using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Game.Player;
using UnityEngine;

namespace CaptainPinkTurd.Story.Barks
{
    /// <summary>
    /// Shows a bark the first time the player walks into this trigger.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BarkZone : MonoBehaviour
    {
        [SerializeField] private BarkSource barkSource;
        [SerializeField] private BarkBubble bubble;
        [SerializeField] private string knot = "Corridor_Bark";
        [SerializeField] private LayerMask playerLayers;

        private bool fired;

        private void Awake()
        {
            //off the projectiles' damage layers, so bullets don't collide with this trigger (see EncounterSpawner)
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (fired || !playerLayers.Contains(other.gameObject.layer)) return;

            var player = other.GetComponentInParent<PlayerUnit>();
            if (!player) return;
            if (!barkSource.TryGetLine(knot, out var speaker, out var line)) return;

            fired = true;
            bubble.Show(player.transform, speaker, line);
        }
    }
}
