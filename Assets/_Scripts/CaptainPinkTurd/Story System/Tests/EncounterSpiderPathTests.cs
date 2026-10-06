#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;
using CaptainPinkTurd.Core.Base;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.SpawnSystem;
using NUnit.Framework;
using PathCreation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// A spider spawned by a room's fight walks one of that room's own paths, starting where it was spawned; a room
    /// without paths skips its spiders and still spawns the rest. Runs in Level Story 3, whose Encounter 2 has a
    /// paths group, with test-built encounters so the level's own spawn lists don't matter.
    /// </summary>
    public class EncounterSpiderPathTests
    {
        private const string SpiderPrefab = "Assets/Prefabs/Enemies/Spiders/Spider.prefab";
        private const string TurretPrefab = "Assets/Prefabs/Enemies/Turrets/Turret.prefab";

        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [UnityTest, Timeout(120000)]
        public IEnumerator SpiderWalksAPathOfItsOwnEncounter()
        {
            yield return LoadLevel3();
            var group = TakeOutPathsGroup("Encounter 2", "Encounter 2 Paths");
            var groupPaths = group.GetComponentsInChildren<PathCreator>();
            Assert.IsNotEmpty(groupPaths, "Encounter 2 Paths has no paths");
            ClearLevelFights();

            //a scene-wide path would almost never pass near this point, so being near it proves the nearest-point start
            var path = groupPaths[0].path;
            var spawnPoint = path.GetPointAtDistance(path.length * 0.3f);
            var encounter = BuildEncounter(group, (spawnPoint, SpiderPrefab));
            encounter.Begin();

            var spawner = encounter.GetComponent<PositionBasedSpawner>();
            yield return StoryTestLoading.WaitFor(() => spawner.SpawnedObjects.Count > 0, 5f, "the spider to spawn");
            var spider = spawner.SpawnedObjects[0].GetComponent<Spider>();
            yield return StoryTestLoading.WaitFor(() => spider.CurrentPath, 2f, "the spider to pick a path");

            CollectionAssert.Contains(groupPaths, spider.CurrentPath, "the spider walks a path outside its encounter's group");
            Assert.Less(Vector2.Distance(spider.transform.position, spawnPoint), 2f, "the spider didn't start near its spawn point");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator EncounterWithoutPathsSkipsSpidersOnly()
        {
            yield return LoadLevel3();
            ClearLevelFights();

            var player = Object.FindAnyObjectByType<Game.Player.PlayerUnit>().transform.position;
            var encounter = BuildEncounter(null,
                (player + new Vector3(-4f, 2f), SpiderPrefab),
                (player + new Vector3(4f, 2f), TurretPrefab));
            LogAssert.Expect(LogType.Warning, new Regex("has no paths group"));
            encounter.Begin();

            var spawner = encounter.GetComponent<PositionBasedSpawner>();
            yield return StoryTestLoading.WaitFor(() => spawner.SpawnedObjects.Count > 0, 5f, "the turret to spawn");
            yield return new WaitForSeconds(1f);

            Assert.AreEqual(1, spawner.SpawnedObjects.Count, "expected only the turret");
            Assert.IsNull(spawner.SpawnedObjects[0].GetComponent<Spider>(), "the spider spawned without a paths group");
            Assert.IsEmpty(Object.FindObjectsByType<Spider>(FindObjectsSortMode.None), "a spider is in the level");
        }

        // ------------------------------------------------------------------ helpers

        private static IEnumerator LoadLevel3()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 3");
        }

        //moves a room's paths group to the scene root, so it stays active when the level's fights are switched off
        private static Transform TakeOutPathsGroup(string encounterName, string groupName)
        {
            var encounter = Object.FindObjectsByType<EncounterSpawner>(FindObjectsSortMode.None).FirstOrDefault(e => e.name == encounterName);
            Assert.IsNotNull(encounter, $"{encounterName} missing");
            var group = encounter.transform.Find(groupName);
            Assert.IsNotNull(group, $"{groupName} missing under {encounterName}");
            group.SetParent(null, true);
            return group;
        }

        //the level's own fights would spawn into the test; physics callbacks reach disabled scripts, so switch the
        //objects off, and clear anything already spawned (spawns don't live under the fight's trigger)
        private static void ClearLevelFights()
        {
            foreach (var encounter in Object.FindObjectsByType<EncounterSpawner>(FindObjectsSortMode.None))
                encounter.gameObject.SetActive(false);
            foreach (var enemy in Object.FindObjectsByType<BulletHell.BiformisEmitterController>(FindObjectsSortMode.None))
                Object.Destroy(enemy.gameObject);
        }

        //built inactive so the serialized fields are in place before Awake reads them; far from the player so its
        //trigger never fires
        private static EncounterSpawner BuildEncounter(Transform pathsGroup, params (Vector3 position, string prefab)[] spawns)
        {
            var go = new GameObject("Test Encounter");
            go.SetActive(false);
            go.transform.position = new Vector3(1000f, 1000f);
            go.AddComponent<BoxCollider2D>();
            var encounter = go.AddComponent<EncounterSpawner>();
            var spawner = go.GetComponent<PositionBasedSpawner>();

            var spawnerData = new SerializedObject(spawner);
            var pairs = spawnerData.FindProperty("spawnedObjectPositionPair");
            pairs.arraySize = spawns.Length;
            for (int i = 0; i < spawns.Length; i++)
            {
                var point = new GameObject($"Test Spawn Point {i}").transform;
                point.position = spawns[i].position;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spawns[i].prefab);
                Assert.IsNotNull(prefab, $"{spawns[i].prefab} missing");

                var pair = pairs.GetArrayElementAtIndex(i);
                pair.FindPropertyRelative("Key").objectReferenceValue = point;
                pair.FindPropertyRelative("Value").objectReferenceValue = prefab.GetComponent<GameObjectBase>();
            }
            spawnerData.ApplyModifiedPropertiesWithoutUndo();

            var encounterData = new SerializedObject(encounter);
            encounterData.FindProperty("pathsGroup").objectReferenceValue = pathsGroup;
            encounterData.ApplyModifiedPropertiesWithoutUndo();

            go.SetActive(true);
            return encounter;
        }
    }
}
#endif
