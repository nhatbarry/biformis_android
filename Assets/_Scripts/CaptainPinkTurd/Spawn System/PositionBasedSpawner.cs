using System;
using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.Base;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Utilities;
using UnityEngine;

namespace CaptainPinkTurd.SpawnSystem
{
    public class PositionBasedSpawner : MonoBehaviour
    {
        [SerializeField] private SerializeKeyValuePair<Transform, GameObjectBase>[] spawnedObjectPositionPair;
        [SerializeField] private float secondsBetweenSpawns = 0.3f;
        [SerializeField] private SoundData spawnSfx;
        
        public readonly List<GameObjectBase> SpawnedObjects = new();
        
        /// <summary>
        /// Fired before each spawn. Listeners may only set Cancel to true: one "no" skips it, whatever order they run in.
        /// </summary>
        public event Action<SpawnRequest> OnSpawning;
        /// <summary>
        /// Fired right after each spawn, in the same frame, so listeners reach the object before its first Update.
        /// </summary>
        public event Action<GameObjectBase> OnObjectSpawned;

        
        public IEnumerator SpawnAllPair()
        {
            SoundManager.Instance.CreateSoundBuilder().WithPosition(transform.position).WithRandomPitch().Play(spawnSfx);
            
            foreach (var objectPositionPair in spawnedObjectPositionPair)
            {
                var request = new SpawnRequest(objectPositionPair.Value);
                OnSpawning?.Invoke(request);
                //a skipped spawn moves straight on to the next one, without the wait
                if (request.Cancel) continue;
                
                var spawnObj = ObjectPoolManager.Instance.SpawnObject(
                    objectPositionPair.Value.gameObject, objectPositionPair.Key.position, Quaternion.identity).GetComponent<GameObjectBase>();
                spawnObj.SetSpawnedFromPool(true);
                SpawnedObjects.Add(spawnObj);
                OnObjectSpawned?.Invoke(spawnObj);
                
                yield return new WaitForSeconds(secondsBetweenSpawns);
            }
        }
    }
}