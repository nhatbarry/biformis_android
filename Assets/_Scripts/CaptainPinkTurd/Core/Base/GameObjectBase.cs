using System;
using CaptainPinkTurd.Core.Attributes;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Enum;
using UnityEngine;
using UnityEngine.Events;

namespace CaptainPinkTurd.Core.Base
{
    public abstract class GameObjectBase : MonoBehaviour
    {
        [Header("Game Object Base Events")]
        [SerializeField] private SerializeKeyValuePair<EPriority, UnityEvent> onAwakeEvents;
        [SerializeField] private SerializeKeyValuePair<EPriority, UnityEvent> onEnableEvents;
        [SerializeField] private SerializeKeyValuePair<EPriority, UnityEvent> onDisableEvents;
        [SerializeField] private SerializeKeyValuePair<EPriority, UnityEvent> onStartEvents;
        
        [Header("Debug")]
        [SerializeField][ReadOnly] private bool spawnedFromPool;
        
        public GameEvent OnAwakeEvents = new GameEvent();
        public GameEvent OnEnableEvents = new GameEvent();
        [Tooltip("Remember to manually clear this before you subscribe")]
        public GameEvent OnDisableEvents = new GameEvent();
        public GameEvent OnStartEvents = new GameEvent();
        
        public bool SpawnedFromPool => spawnedFromPool;
        
        protected virtual void Awake()
        {
            OnAwakeEvents.Subscribe(onAwakeEvents.Value.Invoke, onAwakeEvents.Key);
            OnStartEvents.Subscribe(onStartEvents.Value.Invoke, onStartEvents.Key);
            OnEnableEvents.Subscribe(onEnableEvents.Value.Invoke, onEnableEvents.Key);
            OnDisableEvents.Subscribe(onDisableEvents.Value.Invoke, onDisableEvents.Key);
            
            OnAwakeEvents.Raise();
        }
        protected virtual void OnEnable()
        {
            OnEnableEvents.Raise();
        }

        protected virtual void OnDisable()
        {
            OnDisableEvents.Raise();
        }

        protected virtual void Start()
        {
            OnStartEvents.Raise();
        }
        public void SetSpawnedFromPool(bool spawnedFromPool)
        {
            this.spawnedFromPool = spawnedFromPool;
        }

        private void OnDestroy()
        {
            OnAwakeEvents.Unsubscribe(onAwakeEvents.Value.Invoke);
            OnStartEvents.Unsubscribe(onStartEvents.Value.Invoke);
            OnEnableEvents.Unsubscribe(onEnableEvents.Value.Invoke);
            OnDisableEvents.Unsubscribe(onDisableEvents.Value.Invoke);
        }
    }
}