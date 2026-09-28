using CaptainPinkTurd.Core.Attributes;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.SpawnSystem.Procedural;
using UnityEngine;

namespace CaptainPinkTurd.SpawnSystem.Wave
{
    [RequireComponent(typeof(ProceduralObjectSpawner))]
    public class WaveSpawner : MonoBehaviour
    {
        [SerializeField] private int numberOfWaves = 3;
        [SerializeField][Range(1, 10)] private int minSpawnNumber = 1;
        [SerializeField][Range(1, 10)] private int maxSpawnNumber = 3;
        [SerializeField] private bool loopToFirstWave = true;

        [ShowIf(nameof(loopToFirstWave), false)]
        [SerializeField] private VoidEvent onLastWaveEnd;
        
        private ProceduralObjectSpawner proceduralSpawner;
        private float timeBetweenSpawns;
        private int currentWave;

        private void Awake()
        {
            proceduralSpawner = GetComponent<ProceduralObjectSpawner>();
        }

        private void OnEnable()
        {
            currentWave = 0;
            proceduralSpawner.OnObjectDespawn.Subscribe(WaveIncrement);
        }
        private void OnDisable()
        {
            proceduralSpawner.OnObjectDespawn.Unsubscribe(WaveIncrement);
        }
        private void Start()
        {
            WaveIncrement();
        }
        
        private void Spawn()
        {
            //guard against the despawn event re-entering during scene unload/teardown,
            //when this GameObject is already inactive and StartCoroutine would throw
            //activeInHierarchy, not isActiveAndEnabled: while a scene is torn down the latter can still read true
            //for a behaviour whose OnDisable hasn't run yet, though its hierarchy is already inactive
            if (!enabled || !gameObject.activeInHierarchy) return;

            int numberToSpawn = Random.Range(minSpawnNumber, maxSpawnNumber + 1);
            StartCoroutine(proceduralSpawner.SpawnObjects(numberToSpawn));
        }

        private void WaveIncrement()
        {
            if (proceduralSpawner.CurrentActiveObjects > 0) return;
            
            if(currentWave == numberOfWaves && !loopToFirstWave)
            {
                onLastWaveEnd.Raise();
            }
            else
            {
                currentWave = (currentWave + 1) % (numberOfWaves + 1); //need to offset or else wave will automatically restart when reach the final wave
                Spawn();
            }
        }
    }
}