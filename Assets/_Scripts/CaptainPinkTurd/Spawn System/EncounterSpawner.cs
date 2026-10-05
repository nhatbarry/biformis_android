using System.Collections;
using CaptainPinkTurd.Core.Extensions;
using UnityEngine;
using UnityEngine.Events;

namespace CaptainPinkTurd.SpawnSystem
{
    /// <summary>
    /// A room's fight: when the player walks into this trigger, enemies arrive in waves of a fixed size at the room's
    /// spawn points (never right next to the player). Once the last wave is gone, onCleared fires - opening the next
    /// gate or the level's door.
    /// </summary>
    [RequireComponent(typeof(Collider2D), typeof(PositionBasedSpawner))]
    public class EncounterSpawner : MonoBehaviour
    {
        [SerializeField] private int waves = 1;
        [SerializeField] private float minDistanceFromPlayer = 3.5f;
        [SerializeField] private float minDistanceBetweenSpawns = 1.5f;
        [SerializeField] private float secondsBetweenWaves = 1.5f;
        [SerializeField] private LayerMask playerLayers;
        [SerializeField] private UnityEvent onCleared;
        
        private PositionBasedSpawner positionBasedSpawner;
        private Transform spawnedHolder;
        private bool started;

        public bool Started => started;
        public bool Cleared { get; private set; }

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
            //projectiles hit every collider on their damage layers (Default included, triggers included) and then search
            //the hit object's hierarchy for something damageable: on Default this room trigger would be "hit" by every
            //bullet in the room and hand the damage to whichever spawned enemy it found. Map Bound uses this layer too.
            gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            //spawned enemies live beside this trigger, never under it, so a hit on it can't reach them
            spawnedHolder = new GameObject($"{name} Enemies").transform;
            spawnedHolder.SetParent(transform.parent, false);
            
            positionBasedSpawner = GetComponent<PositionBasedSpawner>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (started || !playerLayers.Contains(other.gameObject.layer)) return;
            
            Begin();
        }

        public void Begin()
        {
            if (started) return;
            
            started = true;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            for (int wave = 0; wave < waves; wave++)
            {
                if (wave > 0) yield return new WaitForSeconds(secondsBetweenWaves);

                yield return positionBasedSpawner.SpawnAllPair();

                //killed enemies are destroyed (or disabled, if something pooled them)
                yield return new WaitUntil(() => positionBasedSpawner.SpawnedObjects.TrueForAll(e => !e || !e.isActiveAndEnabled));
                positionBasedSpawner.SpawnedObjects.Clear();
            }

            Cleared = true;
            onCleared?.Invoke();
        }
    }
}