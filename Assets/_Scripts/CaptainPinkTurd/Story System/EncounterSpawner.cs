using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Game.Player;
using UnityEngine;
using UnityEngine.Events;

namespace CaptainPinkTurd.Story
{
    /// <summary>
    /// A room's fight: when the player walks into this trigger, enemies arrive in waves of a fixed size at the room's
    /// spawn points (never right next to the player). Once the last wave is gone, onCleared fires - opening the next
    /// gate or the level's door.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class EncounterSpawner : MonoBehaviour
    {
        [Tooltip("Each spawn picks one of these at random; list a prefab twice to make it more likely")]
        [SerializeField] private GameObject[] enemyPrefabs;
        [SerializeField] private int enemiesPerWave = 3;
        [SerializeField] private int waves = 1;
        [Tooltip("Floor positions inside the room")]
        [SerializeField] private Vector2[] spawnPoints;
        [SerializeField] private float minDistanceFromPlayer = 3.5f;
        [SerializeField] private float minDistanceBetweenSpawns = 1.5f;
        [SerializeField] private float secondsBetweenSpawns = 0.3f;
        [SerializeField] private float secondsBetweenWaves = 1.5f;
        [SerializeField] private LayerMask playerLayers;
        [SerializeField] private UnityEvent onCleared;

        private readonly List<GameObject> alive = new();
        private Transform spawnedHolder;
        private bool started;

        public bool Started => started;
        public bool Cleared { get; private set; }
        public IReadOnlyList<Vector2> SpawnPoints => spawnPoints;

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

                var used = new List<Vector2>();
                for (int i = 0; i < enemiesPerWave; i++)
                {
                    var point = PickSpawnPoint(used);
                    used.Add(point);
                    var prefab = enemyPrefabs[Random.Range(0, enemyPrefabs.Length)];
                    alive.Add(Instantiate(prefab, point, Quaternion.identity, spawnedHolder));
                    yield return new WaitForSeconds(secondsBetweenSpawns);
                }

                //killed enemies are destroyed (or disabled, if something pooled them)
                yield return new WaitUntil(() => alive.TrueForAll(e => !e || !e.activeInHierarchy));
                alive.Clear();
            }

            Cleared = true;
            onCleared?.Invoke();
        }

        private Vector2 PickSpawnPoint(List<Vector2> used)
        {
            var player = FindAnyObjectByType<PlayerUnit>();
            Vector2 playerPosition = player ? player.transform.position : Vector2.positiveInfinity;

            //prefer points away from the player and from this wave's other spawns; relax the rules if the room is small
            for (int pass = 0; pass < 3; pass++)
            {
                float fromPlayer = pass == 0 ? minDistanceFromPlayer : pass == 1 ? minDistanceFromPlayer * 0.5f : 0f;
                float fromOthers = pass < 2 ? minDistanceBetweenSpawns : 0f;
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    var candidate = spawnPoints[Random.Range(0, spawnPoints.Length)];
                    if (Vector2.Distance(candidate, playerPosition) < fromPlayer) continue;
                    if (used.Exists(u => Vector2.Distance(u, candidate) < fromOthers)) continue;
                    return candidate;
                }
            }
            return spawnPoints[Random.Range(0, spawnPoints.Length)];
        }
    }
}
