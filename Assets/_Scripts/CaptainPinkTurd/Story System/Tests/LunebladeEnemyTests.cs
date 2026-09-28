#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BulletHell;
using CaptainPinkTurd.BulletHell;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// Drops each Luneblade enemy into the open Level 2 arena (loaded the way the game loads it, from Core) and checks
    /// it does its job: the Reaper closes in, the Axion fires, the Riven blinks next to the player. Fails on any
    /// exception thrown from the enemy's own code.
    /// </summary>
    public class LunebladeEnemyTests
    {
        private const string PrefabDir = "Assets/Prefabs/Enemies/Luneblade";

        private readonly List<string> enemyExceptions = new();

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ReaperRunsUpToThePlayer()
        {
            yield return LoadArena();
            var player = Object.FindAnyObjectByType<PlayerUnit>().transform;
            var reaper = Spawn("Reaper", player.position + new Vector3(-5f, 3f));

            float closest = float.MaxValue;
            yield return Watch(6f, () => closest = Mathf.Min(closest, Vector2.Distance(reaper.transform.position, player.position)));

            Assert.Less(closest, 2f, "the Reaper never got close to the player");
            AssertNoEnemyExceptions();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator AxionFiresAtThePlayer()
        {
            yield return LoadArena();
            var player = Object.FindAnyObjectByType<PlayerUnit>().transform;
            var axion = Spawn("Axion", player.position + new Vector3(-4f, 1.5f));
            var emitters = axion.GetComponentsInChildren<ProjectileEmitterBiformis>();

            int mostBullets = 0;
            yield return Watch(8f, () => mostBullets = Mathf.Max(mostBullets, emitters.Sum(e => e.ActiveProjectileCount)));

            Assert.Greater(mostBullets, 0, "the Axion never fired");
            AssertNoEnemyExceptions();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RivenBlinksNextToThePlayer()
        {
            yield return LoadArena();
            var player = Object.FindAnyObjectByType<PlayerUnit>().transform;
            var riven = Spawn("Riven", player.position + new Vector3(-6f, 5f));

            Vector2 last = riven.transform.position;
            float biggestJump = 0f, closest = float.MaxValue;
            yield return Watch(7f, () =>
            {
                Vector2 now = riven.transform.position;
                biggestJump = Mathf.Max(biggestJump, Vector2.Distance(now, last));
                closest = Mathf.Min(closest, Vector2.Distance(now, player.position));
                last = now;
            });

            Assert.Greater(biggestJump, 1.5f, "the Riven never blinked");
            Assert.Less(closest, 4f, "the Riven never appeared near the player");
            AssertNoEnemyExceptions();
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RoomEnemiesDontKillEachOther()
        {
            //regression: every bullet in a room used to "hit" the room's fight trigger (Default layer, and queries hit
            //triggers even from inside them) and the damage went to whichever spawned enemy sat under that trigger
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 3");
            yield return StoryTestLoading.WaitFor(() => Object.FindObjectsByType<BiformisEmitterController>(FindObjectsSortMode.None).Length >= 4, 15f, "the first wave");

            int spawned = Object.FindObjectsByType<BiformisEmitterController>(FindObjectsSortMode.None).Length;
            int fewest = spawned;
            //the player stands still, so nothing but friendly fire could bring the count down
            yield return Watch(10f, () => fewest = Mathf.Min(fewest, Object.FindObjectsByType<BiformisEmitterController>(FindObjectsSortMode.None).Length));

            Assert.AreEqual(spawned, fewest, "enemies died without the player touching them");
            AssertNoEnemyExceptions();
        }

        // ------------------------------------------------------------------ helpers

        private static IEnumerator LoadArena()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 2");

            //the level's own fight would get in the way; physics callbacks reach disabled scripts, so switch the objects
            //off, and clear anything it already spawned (spawns don't live under the fight's trigger)
            foreach (var encounter in Object.FindObjectsByType<EncounterSpawner>(FindObjectsSortMode.None))
                encounter.gameObject.SetActive(false);
            foreach (var enemy in Object.FindObjectsByType<BiformisEmitterController>(FindObjectsSortMode.None))
                Object.Destroy(enemy.gameObject);
            yield return new WaitForSeconds(0.5f);
        }

        private static GameObject Spawn(string name, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{name}.prefab");
            Assert.IsNotNull(prefab, $"{name} prefab missing");
            return Object.Instantiate(prefab, position, Quaternion.identity);
        }

        private static IEnumerator Watch(float seconds, Action sample)
        {
            for (float end = Time.time + seconds; Time.time < end;)
            {
                LogAssert.ignoreFailingMessages = true;
                sample();
                yield return null;
            }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception && stackTrace.Contains(nameof(LunebladeEnemy))) enemyExceptions.Add(condition + "\n" + stackTrace);
        }

        private void AssertNoEnemyExceptions() => Assert.IsEmpty(enemyExceptions, string.Join("\n\n", enemyExceptions));
    }
}
#endif
