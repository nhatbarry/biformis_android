using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.Core.Attributes;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using UnityEngine;

namespace CaptainPinkTurd.Core.Utilities
{
    public class AreaDamage : MonoBehaviour
    {
        [Header("Area Damage Configs")]
        [SerializeField] private LayerMask damageableLayers;
        [SerializeField] private bool manualConfig;
        [ShowIf(nameof(manualConfig))] public int Damage = 1;
        [ShowIf(nameof(manualConfig))] public float TickRate = .1f;
        
        public bool IsManualConfig => manualConfig;
        
        private readonly HashSet<IDamageable> damageables = new();

        private void OnDisable()
        {
            damageables.Clear();
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (!damageableLayers.Contains(other.gameObject.layer) ||
                !other.TryGetComponentInHierarchy(out IDamageable damageable)) return;
            
            damageables.Add(damageable);
            StartCoroutine(DealDamage());
        }

        private void OnTriggerExit(Collider other)
        {
            if (!damageableLayers.Contains(other.gameObject.layer) ||
                !other.TryGetComponentInHierarchy(out IDamageable damageable)) return;
            
            damageables.Remove(damageable);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!damageableLayers.Contains(other.gameObject.layer) ||
                !other.TryGetComponentInHierarchy(out IDamageable damageable)) return;
            
            damageables.Add(damageable);
            StartCoroutine(DealDamage());
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!damageableLayers.Contains(other.gameObject.layer) ||
                !other.TryGetComponentInHierarchy(out IDamageable damageable)) return;
            
            damageables.Remove(damageable);
        }

        private IEnumerator DealDamage()
        {
            WaitForSeconds Wait = new WaitForSeconds(TickRate);

            while (damageables != null)
            {
                foreach (var damageable in damageables)
                {
                    damageable.TakeDamage(new SDamageData(Damage, gameObject));
                }
                yield return Wait;
            }
        }
    }
}