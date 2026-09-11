using CaptainPinkTurd.Core.DesignPattern.State;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.UI;
using CaptainPinkTurd.UnitSystem;
using UnityEngine;

namespace CaptainPinkTurd.Game.Enemy
{
    public abstract class EnemyUnitBase : UnitBase
    {
        [Header("Enemy Unit Base Properties")]
        [SerializeField] internal ObjectLineOfSightSystem targetDetectionRange;
        [SerializeField] private VoidEvent onEnemyDamaged;
        [SerializeField] internal float speed = 10f;
        
        public StateMachine StateMachine { get; protected set; }
        public EnemyUnitInfo EnemyInfo => unitInfo as EnemyUnitInfo;
        public GameObject Target { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            StateMachineSetup();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            
            targetDetectionRange?.OnObjectDetect.Subscribe(TargetDetectEvents);
            targetDetectionRange?.OnObjectLoseSight.Subscribe(TargetLoseSightEvents);
            
            StateMachineSetup();
        }

        protected override void OnDisable()
        {
            if(!gameObject.scene.isLoaded) return;
            
            base.OnDisable();
            
            targetDetectionRange?.OnObjectDetect.Unsubscribe(TargetDetectEvents);
            targetDetectionRange?.OnObjectLoseSight.Unsubscribe(TargetLoseSightEvents);
        }

        protected virtual void Update()
        {
            StateMachine?.Update();
        }

        protected virtual void FixedUpdate()
        {
            StateMachine?.FixedUpdate();
        }
        
        protected abstract void StateMachineSetup();
        
        private void TargetDetectEvents(GameObject target)
        {
            Target = target;
        }
        private void TargetLoseSightEvents(GameObject target)
        {
            Target = null;
        }

        public override void OnDamaged(SDamageData damageData)
        {
            onEnemyDamaged.Raise();
        }

        public override void OnDeath(SDamageData damageData)
        {
            StopAllCoroutines();

            var source = damageData.Source;
            if (source.transform.TryGetComponentInHierarchy(out PlayerUnit playerUnit))
            {
                playerUnit.OnDamageableKill.Raise();
            }
            
            if (!gameObject.activeInHierarchy) return;
            ObjectPoolManager.Instance.ReturnObjectToPool(gameObject);
            
            UiManager.Instance.AddKillCount();
        }

        private void OnValidate()
        {
            if (!EnemyInfo)
            {
                Debug.LogWarning("Unit Info is either not set or not assigned as type of EnemyUnitInfo, set UnitInfo to null instead");
                unitInfo = null;
            }
        }
    }
}