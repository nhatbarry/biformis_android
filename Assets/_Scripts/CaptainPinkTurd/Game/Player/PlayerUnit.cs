using System;
using BulletHell;
using CaptainPinkTurd.AnimationSystem;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Variables;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Core.Utils;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.UnitSystem;
using DG.Tweening;
using UnityEngine;

namespace CaptainPinkTurd.Game.Player
{
    [RequireComponent(typeof(Target))]
    public class PlayerUnit : UnitBase
    {
        [Header("Player Unit Properties")]
        [SerializeField] private SerializeKeyValuePair<EColor, PlayerStateInfo>[] playerStates;
        [SerializeField] private int invincibilityLayer;
        [SerializeField] private LayerMask enemyLayers;
        [SerializeField] private BasicVfxAnimationController colorSwitchVfx;
        [SerializeField] private BasicVfxAnimationController explosionVfx;
        [SerializeField] private float delayOnDeath = 1f;
        
        [Header("Player Events")]
        [SerializeField] private VoidEvent onPlayerDamaged;
        [SerializeField] private VoidEvent onPlayerDeath;
        [SerializeField] private BoolVariableSO isDashing;
        
        private int currentColorLayer;

        protected override void OnEnable()
        {
            base.OnEnable();

            isDashing.OnValueChanged += OnDash;
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            
            isDashing.OnValueChanged -= OnDash;
        }

        private void OnDash(bool isDashing)
        {
            ApplyLayer(isDashing ? invincibilityLayer : currentColorLayer);
        }
        public void OnColorChangeEvents(EColor color)
        {
            if(playerStates.TryGetValue(color, out var playerState))
            {
                foreach (var state in playerStates)
                {
                    state.Value.model.SetActive(false);
                }
                colorSwitchVfx.gameObject.SetActive(true);
                playerState.model.SetActive(true);

                currentColorLayer = playerState.layerValue;

                // if the player color-swaps mid-dash, don't drop the phase layer —
                // it'll be restored to currentColorLayer when the dash ends
                if (!isDashing.Value) ApplyLayer(currentColorLayer);
            }
            else
            {
                Debug.LogError($"Player State for dimension {color} not found");
            }
        }
        private void ApplyLayer(int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in transform)
            {
                child.gameObject.layer = layer;
            }
        }

        public override void OnDamaged(SDamageData damageData)
        {
            onPlayerDamaged.Raise();
            
            if(unitHealth.CurrentHealth <= 0) return;
            
            transform.DOShakePosition(gameObjectShakeProfile.useWithHitStop ? hitStopDuration : gameObjectShakeProfile.defaultShakeDuration,
                    gameObjectShakeProfile.shakeStrength,
                    gameObjectShakeProfile.vibration,
                    gameObjectShakeProfile.randomness,
                    gameObjectShakeProfile.snapping,
                    gameObjectShakeProfile.fadeOut)
                .SetUpdate(UpdateType.Normal, true);

            HitStop.Stop(hitStopDuration);
        }

        public override void OnDeath(SDamageData damageData)
        {
            StopAllCoroutines();
            
            onPlayerDeath.Raise();
            
            HitStop.Stop(deathHitStopDuration, () =>
            {
                ObjectPoolManager.Instance.SpawnObject(explosionVfx.gameObject, transform.position,
                    Quaternion.identity,
                    ObjectPoolManager.PoolType.VFX);

                var source = damageData.Source;
                if (source.transform.TryGetComponentInHierarchy(out EnemyUnitBase enemyUnit))
                {
                    enemyUnit.OnDamageableKill.Raise();
                }

                gameObject.SetActive(false);

                if (delayOnDeath < 0) return;

                //order matter for this one cause the game over popup needs to be enabled first to get the high score
                GameManager.Instance.StartCoroutine(CoroutineUtils.WaitForSeconds(delayOnDeath,
                    GameManager.Instance.OnGameOver.Raise));
            });
        }
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!enemyLayers.Contains(other.gameObject.layer)) return;
            if (!other.gameObject.transform.TryGetComponentInHierarchy(out IDamageable enemyDamageable)) return;
            
            enemyDamageable.TakeDamage(new SDamageData(1, gameObject));
        }
    }

    [Serializable]
    public struct PlayerStateInfo
    {
        public GameObject model;
        public int layerValue;
    }
}