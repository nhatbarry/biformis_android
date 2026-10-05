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
        
        public IEnumerator SpawnAllPair()
        {
            SoundManager.Instance.CreateSoundBuilder().WithPosition(transform.position).WithRandomPitch().Play(spawnSfx);
            
            foreach (var objectPositionPair in spawnedObjectPositionPair)
            {
                var spawnObj = ObjectPoolManager.Instance.SpawnObject(
                    objectPositionPair.Value.gameObject, objectPositionPair.Key.position, Quaternion.identity).GetComponent<GameObjectBase>();
                spawnObj.SetSpawnedFromPool(true);
                SpawnedObjects.Add(spawnObj);
                
                yield return new WaitForSeconds(secondsBetweenSpawns);
            }
        }
    }
}