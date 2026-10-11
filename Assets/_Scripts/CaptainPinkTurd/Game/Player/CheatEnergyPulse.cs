using System.Collections.Generic;
using CaptainPinkTurd.BulletHell;
using CaptainPinkTurd.Core.Base;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.UI.Popup;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace CaptainPinkTurd.Game.Player
{
    /// <summary>The optional skull button: one immediate HP hit per live enemy, across the current map.</summary>
    [DisallowMultipleComponent]
    public sealed class CheatEnergyPulse : MonoBehaviour
    {
        private PlayerUnit player;
        private IDamageable playerHealth;
        private PopupManager popupManager;
        private InputAction pulseAction;
        private readonly HashSet<IDamageable> targets = new();

        public static CheatEnergyPulse EnsureAttached(PlayerUnit unit)
        {
            return unit.TryGetComponent<CheatEnergyPulse>(out var pulse)
                ? pulse : unit.gameObject.AddComponent<CheatEnergyPulse>();
        }

        private void Awake()
        {
            player = GetComponent<PlayerUnit>();
            playerHealth = GetComponent<IDamageable>();
            pulseAction = new InputAction("Cheat Energy Pulse", InputActionType.Button, "<Keyboard>/k");
            pulseAction.AddBinding(MobileControlPaths.CheatPulse);
        }

        private void OnEnable()
        {
            pulseAction.performed += OnPulse;
            pulseAction.Enable();
        }

        private void OnDisable()
        {
            pulseAction.performed -= OnPulse;
            pulseAction.Disable();
        }

        private void OnDestroy() => pulseAction.Dispose();

        private void OnPulse(InputAction.CallbackContext context) => TryActivate();

        /// <summary>Damage is immediate across the map; only its outer perimeter absorbs the visual ring.</summary>
        public bool TryActivate()
        {
            if (!CanActivate()) return false;

            Physics2D.SyncTransforms();
            Vector2 origin = transform.position;
            Scene map = gameObject.scene;
            targets.Clear();
            foreach (var component in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!component.isActiveAndEnabled || component is not IDamageable target || target.CurrentHealth <= 0)
                    continue;
                if (!IsMapEnemy(component, map)) continue;
                targets.Add(target); // The health owner, rather than its possibly multiple colliders.
            }

            var boundary = CheatPulseBoundary.ForMap(map);
            float radius = boundary.MaximumDistance(origin) + 1f;
            foreach (var target in targets)
                radius = Mathf.Max(radius, Vector2.Distance(origin, target.GetTransform().position) + 1f);
            var wallDistances = new float[CheatPulseVisual.Segments];
            for (int i = 0; i < wallDistances.Length; i++)
            {
                Vector2 direction = CheatPulseVisual.Direction(i);
                wallDistances[i] = boundary.ExitDistance(origin, direction, radius);
            }
            CheatPulseVisual.Spawn(map, origin, radius, wallDistances);

            var damage = new SDamageData(1, player.gameObject, ignoreInvincibility: true);
            foreach (var target in targets)
            {
                // Death callbacks may immediately return another target to its pool.
                var owner = target as MonoBehaviour;
                if (!owner || !owner.isActiveAndEnabled || target.CurrentHealth <= 0) continue;
                target.TakeDamage(damage);
            }
            return true;
        }

        private bool CanActivate()
        {
            if (!CheatSettings.Enabled || !isActiveAndEnabled || !player || !player.isActiveAndEnabled ||
                playerHealth == null || playerHealth.CurrentHealth <= 0 || Time.timeScale <= 0f ||
                !gameObject.scene.name.StartsWith("Level") || InteractPrompt.CutsceneControls)
                return false;
            // Both the boss entrance and ice reveal hold the player's body while hiding the touch canvas.
            if (player.rb && (player.rb.constraints & RigidbodyConstraints2D.FreezePosition) == RigidbodyConstraints2D.FreezePosition)
                return false;
            if (!popupManager) popupManager = FindAnyObjectByType<PopupManager>();
            return !popupManager || !popupManager.AnyPopupShowing();
        }

        private static bool IsMapEnemy(MonoBehaviour component, Scene map)
        {
            bool enemy = component is BiformisEmitterController || component is PlagueDoctorBoss ||
                         component.GetComponentInParent<EnemyUnitBase>();
            if (!enemy || component.GetComponentInParent<PlayerUnit>()) return false;
            Scene ownerScene = component.gameObject.scene;
            if (!ownerScene.isLoaded) return false;
            if (ownerScene == map) return true;
            // Position/Horde spawners parent active enemies under Core's shared pool. Those belong to the
            // currently played map even though their GameObject.scene is Core. Inactive pool entries never qualify.
            var pooled = component.GetComponentInParent<GameObjectBase>();
            // Horde spawns also use this shared holder, but do not set the legacy SpawnedFromPool marker.
            bool activePool = pooled && pooled.SpawnedFromPool || component.transform.root.name == "Pooled Objects";
            return activePool && !ownerScene.name.StartsWith("Level");
        }

    }
}
