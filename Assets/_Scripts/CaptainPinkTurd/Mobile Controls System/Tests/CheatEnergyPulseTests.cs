using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.UnitSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.MobileControls.Tests
{
    public sealed class CheatEnergyPulseTests
    {
        private readonly List<GameObject> objects = new();
        private Scene map, otherMap, poolScene;
        private CheatPulseTestPlayer player;
        private CheatEnergyPulse pulse;
        private Gamepad gamepad;

        [SetUp]
        public void SetUp()
        {
            HitStop.Abort();
            Time.timeScale = 1f;
            CheatSettings.Enabled = false;
            InteractPrompt.HideCutsceneControls();
            otherMap = default;
            poolScene = default;
            map = SceneManager.CreateScene("Level Cheat Energy Tests");
            if (!SoundManager.HasInstance) Create<SoundManager>("Test Sound Manager", Vector2.zero, map);
            player = Create<CheatPulseTestPlayer>("Player", Vector2.zero, map);
            pulse = CheatEnergyPulse.EnsureAttached(player);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            CheatSettings.Enabled = false;
            InteractPrompt.HideCutsceneControls();
            HitStop.Abort();
            Time.timeScale = 1f;
            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            gamepad = null;
            foreach (var visual in Object.FindObjectsByType<CheatPulseVisual>(FindObjectsSortMode.None))
                if (visual.gameObject.scene == map) Object.DestroyImmediate(visual.gameObject);
            foreach (var root in objects) if (root) Object.DestroyImmediate(root);
            objects.Clear();
            if (otherMap.IsValid()) yield return SceneManager.UnloadSceneAsync(otherMap);
            if (poolScene.IsValid()) yield return SceneManager.UnloadSceneAsync(poolScene);
            if (map.IsValid()) yield return SceneManager.UnloadSceneAsync(map);
            map = otherMap = poolScene = default;
        }

        [Test]
        public void HitsEveryLiveEnemyAcrossTheMapOnceRegardlessOfColourOrColliders()
        {
            var red = Enemy("Red enemy", new Vector2(30f, 10f));
            var blue = Enemy("Blue enemy", new Vector2(-40f, -12f));
            red.gameObject.layer = LayerMask.NameToLayer("Red");
            blue.gameObject.layer = LayerMask.NameToLayer("Blue");
            red.gameObject.AddComponent<CircleCollider2D>();
            red.gameObject.AddComponent<BoxCollider2D>();
            var hazard = Create<CheatPulseTestHazard>("Damageable hazard", new Vector2(1f, 0f), map);
            var inactive = Enemy("Pooled inactive enemy", new Vector2(2f, 0f));
            inactive.gameObject.SetActive(false);
            var dead = Enemy("Dead enemy", new Vector2(-2f, 0f));
            dead.Health = 0;

            CheatSettings.Enabled = true;
            Assert.IsTrue(pulse.TryActivate());

            Assert.AreEqual(4, red.CurrentHealth, "the radius must cover the whole map, beyond the old three units");
            Assert.AreEqual(4, blue.CurrentHealth);
            Assert.AreEqual(1, red.Hits, "two colliders must still mean one HP");
            Assert.AreEqual(player.gameObject, red.LastDamage.Source, "normal player kill credit must be retained");
            Assert.IsTrue(red.LastDamage.IgnoreInvincibility);
            Assert.AreEqual(5, hazard.CurrentHealth, "a damageable object is not automatically an enemy");
            Assert.AreEqual(5, inactive.CurrentHealth);
            Assert.AreEqual(0, dead.Hits);
            Assert.AreEqual(10, player.CurrentHealth, "the energy must never damage its caster");
        }

        [Test]
        public void CurrentMapIncludesActivePooledEnemiesAndExcludesAnotherLoadedLevel()
        {
            otherMap = SceneManager.CreateScene("Level Other Cheat Tests");
            poolScene = SceneManager.CreateScene("Core Cheat Pool Tests");
            var other = Create<CheatPulseTestEnemy>("Enemy in another map", Vector2.right, otherMap);
            var activePooled = Create<CheatPulseTestEnemy>("Active pool enemy", Vector2.up * 15f, poolScene);
            activePooled.SetSpawnedFromPool(true);
            var holder = CreateRoot("Pooled Objects", Vector2.zero, poolScene);
            var hordePooled = Create<CheatPulseTestEnemy>("Horde pool enemy", Vector2.down * 15f, poolScene);
            hordePooled.transform.SetParent(holder.transform);
            var dormant = Create<CheatPulseTestEnemy>("Inactive pool enemy", Vector2.up * 17f, poolScene);
            dormant.SetSpawnedFromPool(true);
            dormant.gameObject.SetActive(false);

            CheatSettings.Enabled = true;
            Assert.IsTrue(pulse.TryActivate());
            Assert.AreEqual(4, activePooled.CurrentHealth, "Core-scene pooled enemies still belong to the played map");
            Assert.AreEqual(4, hordePooled.CurrentHealth, "Endless Horde's unmarked pool entries must also be included");
            Assert.AreEqual(5, dormant.CurrentHealth);
            Assert.AreEqual(5, other.CurrentHealth, "an additive transition must not hit a different map");
        }

        [Test]
        public void InteriorWallsDoNotShieldEnemiesAndTheOuterMapBoundaryAbsorbsTheWave()
        {
            var behind = Enemy("Behind wall", Vector2.right * 8f);
            var open = Enemy("Open side", Vector2.left * 20f);
            var wall = CreateRoot("Wall", new Vector2(2f, 0f), map);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(0.5f, 12f);
            var boundary = CreateRoot("Map Bound", Vector2.zero, map).AddComponent<BoxCollider2D>();
            boundary.size = new Vector2(50f, 40f);
            boundary.isTrigger = true;
            CheatSettings.Enabled = true;

            Assert.IsTrue(pulse.TryActivate());
            Assert.AreEqual(4, behind.CurrentHealth, "interior walls must not protect enemies from the map-wide cheat");
            Assert.AreEqual(4, open.CurrentHealth);
            Assert.AreEqual(10, player.CurrentHealth);
            Assert.AreEqual(1, Object.FindObjectsByType<CheatPulseVisual>(FindObjectsSortMode.None).Length,
                "one activation should create its visible wave");
            var visual = Object.FindAnyObjectByType<CheatPulseVisual>();
            var distances = (float[])typeof(CheatPulseVisual).GetField("walls", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(visual);
            Assert.AreEqual(25f, distances[0], 0.01f, "rightward energy stops at the map perimeter, not the wall at x=2");
            Assert.AreEqual(20f, distances[CheatPulseVisual.Segments / 4], 0.01f,
                "a trigger containing the player must produce the positive ray exit, not a zero-distance hit");
        }

        [Test]
        public void IrregularTransformedMapPolygonUsesItsRealPerimeter()
        {
            var root = CreateRoot("Map Bound", new Vector2(1f, 0f), map);
            var boundary = root.AddComponent<PolygonCollider2D>();
            boundary.isTrigger = true;
            boundary.offset = new Vector2(-1f, 0f);
            boundary.points = new[] { new Vector2(-6f, -4f), new Vector2(6f, -4f),
                new Vector2(6f, 2f), new Vector2(2f, 6f), new Vector2(-6f, 6f) };
            CheatSettings.Enabled = true;
            Assert.IsTrue(pulse.TryActivate());
            var visual = Object.FindAnyObjectByType<CheatPulseVisual>();
            var distances = (float[])typeof(CheatPulseVisual).GetField("walls", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(visual);
            Assert.AreEqual(6f, distances[0], 0.01f);
            Assert.AreEqual(4f * Mathf.Sqrt(2f), distances[CheatPulseVisual.Segments / 8], 0.01f,
                "the diagonal ray must hit the sloped edge rather than the polygon's rectangular bounds");
        }

        [Test]
        public void DisabledPauseCutsceneAndDeadPlayerCannotFire()
        {
            var enemy = Enemy("Enemy", Vector2.right * 10f);
            Assert.IsFalse(pulse.TryActivate(), "cheat is off by default");
            CheatSettings.Enabled = true;
            Time.timeScale = 0f;
            Assert.IsFalse(pulse.TryActivate(), "opening pause must disable attacks");
            Time.timeScale = 1f;
            InteractPrompt.ShowCutsceneControls(CaptainPinkTurd.Core.Enum.EColor.Red);
            Assert.IsFalse(pulse.TryActivate());
            InteractPrompt.HideCutsceneControls();
            player.Health = 0;
            Assert.IsFalse(pulse.TryActivate());
            Assert.AreEqual(0, enemy.Hits);
        }

        [Test]
        public void CheatBypassesLegacyInvincibilityAndStillRaisesNormalDamageAndDeathEvents()
        {
            var root = CreateRoot("Legacy unit", Vector2.right * 8f, map);
            root.SetActive(false);
            var unit = root.AddComponent<CheatPulseHealthUnit>();
            var health = root.AddComponent<UnitHealth>();
            Set(health, "unit", unit);
            Set(health, "maxHealth", 3);
            Set(health, "invincibilityFrameDuration", 10f);
            root.SetActive(true);

            health.TakeDamage(new SDamageData(1, player.gameObject));
            Assert.AreEqual(2, health.CurrentHealth);
            health.TakeDamage(new SDamageData(1, player.gameObject));
            Assert.AreEqual(2, health.CurrentHealth, "ordinary damage must keep its existing invincibility rule");
            CheatSettings.Enabled = true;
            Assert.IsTrue(pulse.TryActivate());
            Assert.AreEqual(1, health.CurrentHealth);
            Assert.IsTrue(pulse.TryActivate());
            Assert.AreEqual(0, health.CurrentHealth);
            Assert.AreEqual(3, unit.DamageEvents);
            Assert.AreEqual(1, unit.DeathEvents);
            Assert.IsTrue(pulse.TryActivate());
            Assert.AreEqual(1, unit.DeathEvents, "a dead enemy must not die twice");
        }

        [UnityTest]
        public IEnumerator TheVirtualGamepadSkullBindingFiresTheGameplayAction()
        {
            var enemy = Enemy("Enemy", Vector2.right * 10f);
            CheatSettings.Enabled = true;
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            yield return null;
            yield return null;
            Assert.AreEqual(4, enemy.CurrentHealth, "the skull path must reach the real player action");
            yield return null;
            Assert.AreEqual(4, enemy.CurrentHealth, "holding a button is one press, not one hit per frame");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
        }

        private CheatPulseTestEnemy Enemy(string name, Vector2 position) => Create<CheatPulseTestEnemy>(name, position, map);
        private T Create<T>(string name, Vector2 position, Scene scene) where T : Component
            => CreateRoot(name, position, scene).AddComponent<T>();
        private GameObject CreateRoot(string name, Vector2 position, Scene scene)
        {
            var root = new GameObject(name);
            root.transform.position = position;
            SceneManager.MoveGameObjectToScene(root, scene);
            objects.Add(root);
            return root;
        }
        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    // Deliberately inert actors isolate targeting/input from the unrelated movement, audio and animation setup.
    public sealed class CheatPulseTestPlayer : PlayerUnit, IDamageable
    {
        public int Health = 10;
        public int CurrentHealth => Health;
        public int MaxHealth => 10;
        public GameEvent<SDamageData> OnTakeDamage { get; } = new();
        GameEvent<SDamageData> IDamageable.OnDeath { get; } = new();
        public Transform GetTransform() => transform;
        public void TakeDamage(SDamageData damage) => Health -= damage.Amount;
        protected override void Awake() { }
        protected override void OnEnable() => CheatEnergyPulse.EnsureAttached(this);
        protected override void OnDisable() { }
        protected override void Start() { }
        private void OnDestroy() { }
    }

    public sealed class CheatPulseTestEnemy : EnemyUnitBase, IDamageable
    {
        public int Health = 5;
        public int Hits;
        public SDamageData LastDamage;
        public int CurrentHealth => Health;
        public int MaxHealth => 5;
        public GameEvent<SDamageData> OnTakeDamage { get; } = new();
        GameEvent<SDamageData> IDamageable.OnDeath { get; } = new();
        public Transform GetTransform() => transform;
        public void TakeDamage(SDamageData damage) { Hits++; LastDamage = damage; Health -= damage.Amount; }
        protected override void Awake() { }
        protected override void OnEnable() { }
        protected override void OnDisable() { }
        protected override void Start() { }
        protected override void StateMachineSetup() { }
        private void OnDestroy() { }
    }

    public sealed class CheatPulseHealthUnit : EnemyUnitBase
    {
        public int DamageEvents, DeathEvents;
        public override void OnDamaged(SDamageData damage) => DamageEvents++;
        public override void OnDeath(SDamageData damage) => DeathEvents++;
        protected override void Awake() { }
        protected override void OnEnable() { }
        protected override void OnDisable() { }
        protected override void Start() { }
        protected override void StateMachineSetup() { }
        private void OnDestroy() { }
    }

    public sealed class CheatPulseTestHazard : MonoBehaviour, IDamageable
    {
        public int CurrentHealth { get; private set; } = 5;
        public int MaxHealth => 5;
        public GameEvent<SDamageData> OnTakeDamage { get; } = new();
        public GameEvent<SDamageData> OnDeath { get; } = new();
        public Transform GetTransform() => transform;
        public void TakeDamage(SDamageData damage) => CurrentHealth -= damage.Amount;
    }
}
