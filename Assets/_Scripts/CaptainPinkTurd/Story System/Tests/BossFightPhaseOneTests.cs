#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Variables;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Game;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>Exercises real damage, colour, dash, warning time, pause, minion death and the exit lock in Level 6.</summary>
    public class BossFightPhaseOneTests
    {
        private PlagueDoctorBoss boss;
        private BossHazards hazards;
        private PlayerUnit player;
        private BoolVariableSO dash;
        private readonly List<string> exceptions = new();

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
            if (dash) dash.Value = false;
            HitStop.Abort();
            Time.timeScale = 1f;
            Assert.IsEmpty(exceptions, string.Join("\n\n", exceptions));
        }

        private IEnumerator Load(bool runAttacks = false)
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 6");
            boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
            hazards = boss.GetComponent<BossHazards>();
            player = Object.FindAnyObjectByType<PlayerUnit>();
            dash = (BoolVariableSO)new SerializedObject(player).FindProperty("isDashing").objectReferenceValue;
            dash.Value = false;
            if (!runAttacks)
            {
                boss.GetComponent<BossRoaming>().enabled = false;
                var settings = new SerializedObject(boss);
                settings.FindProperty("idleSeconds").vector2Value = new Vector2(100f, 100f);
                settings.FindProperty("castWeight").floatValue = 0f;
                settings.FindProperty("walkThrowWeight").floatValue = 0f;
                settings.FindProperty("levitateWeight").floatValue = 0f;
                settings.ApplyModifiedProperties();
            }
            yield return new WaitForSeconds(0.3f);
            hazards.ClearHazards();
        }

        [Test]
        public void BossUsesTheExistingEnemyBulletArtAndHitReferences()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab");
            var hazardSettings = new SerializedObject(root.GetComponent<BossHazards>());
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/[ Final ]/Bullet/R2.png"),
                hazardSettings.FindProperty("redProjectile").objectReferenceValue);
            Assert.AreEqual(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/[ Final ]/Bullet/B2.png"),
                hazardSettings.FindProperty("blueProjectile").objectReferenceValue);
            var enemy = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Luneblade/Reaper.prefab");
            var reference = new SerializedObject(enemy.GetComponent<LunebladeEnemy>());
            var settings = new SerializedObject(root.GetComponent<PlagueDoctorBoss>());
            Assert.AreEqual(reference.FindProperty("knockbackForce").floatValue, settings.FindProperty("knockbackForce").floatValue);
            Assert.AreEqual(reference.FindProperty("hitStopDuration").floatValue, settings.FindProperty("hitStopDuration").floatValue);
            Assert.AreEqual(reference.FindProperty("damagedSfx.clip").objectReferenceValue, settings.FindProperty("damagedSfx.clip").objectReferenceValue);
            Assert.AreEqual(reference.FindProperty("impactShockwavePrefab").objectReferenceValue, settings.FindProperty("impactShockwavePrefab").objectReferenceValue);
            Assert.AreEqual(reference.FindProperty("shakeProfile").objectReferenceValue, settings.FindProperty("shakeProfile").objectReferenceValue);
            Assert.IsTrue(root.GetComponent<SpriteRenderer>().sharedMaterial.HasProperty("_HitEffectAmount"));
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator WalkingIntoBossHitsRecoilsAndCannotPassThroughItsFeet()
        {
            yield return Load();
            var feet = boss.GetComponent<CircleCollider2D>();
            Assert.IsFalse(feet.isTrigger, "the feet must physically block, not just report trigger overlap");
            var settings = new SerializedObject(boss);
            settings.FindProperty("damageCooldown").floatValue = 1000f; //one real hit, then test the solid contact without another recoil
            settings.ApplyModifiedProperties();
            var circle = player.GetComponentInChildren<CircleCollider2D>();
            Vector2 offset = circle.bounds.center - player.transform.position;
            player.rb.position = (Vector2)feet.bounds.center - Vector2.up * 2f - offset;
            Physics2D.SyncTransforms();
            hazards.FireProjectile((Vector2)boss.transform.position + Vector2.right * 8f, Vector2.right, EColor.Red, 1f);
            var thumb = TouchHud.PushStick(Vector2.up);
            yield return StoryTestLoading.WaitFor(() => boss.CurrentHealth == 14, 8f, "ordinary walk contact damage");
            TouchHud.LetGoOfStick(thumb);
            float contactY = circle.bounds.center.y;
            Assert.IsTrue(HitStop.IsWaiting, "ram must use the old enemies' hit-stop");
            Assert.Greater(Object.FindObjectsByType<ShockwaveScreen>(FindObjectsSortMode.None).Length, 0, "ram must spawn the old impact shockwave");
            yield return new WaitForSecondsRealtime(0.1f);
            var block = new MaterialPropertyBlock();
            boss.GetComponent<SpriteRenderer>().GetPropertyBlock(block);
            Assert.Greater(block.GetFloat("_HitEffectAmount"), 0.1f, "the hit must be visible as a flash");
            Capture("boss-contact-impact");
            yield return new WaitForSeconds(0.7f);
            Assert.Less(circle.bounds.center.y, contactY - 0.5f, "normal enemy knockback must push the player back");
            Assert.AreEqual(0, hazards.ActiveProjectileCount, "successful ram must clear the bullets after hit-stop");

            player.rb.position = (Vector2)feet.bounds.center - Vector2.up * 2f - offset;
            Physics2D.SyncTransforms();
            thumb = TouchHud.PushStick(Vector2.up);
            yield return new WaitForSeconds(2f);
            TouchHud.LetGoOfStick(thumb);
            Assert.Less(circle.bounds.center.y, feet.bounds.center.y - 0.3f, "holding move into the boss must not walk through it");
            Assert.GreaterOrEqual(Physics2D.Distance(circle, feet).distance, -0.03f, "solid contact must resolve the overlap");
            Assert.AreEqual(14, boss.CurrentHealth, "remaining in one contact must not drain repeated HP");
            yield return null;
            boss.GetComponent<SpriteRenderer>().GetPropertyBlock(block);
            Assert.AreEqual(0f, block.GetFloat("_HitEffectAmount"), 0.001f, "the flash must reset");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator RammingRemovesOneOfSevenPointsAndTheArenaHasNoWalls()
        {
            yield return Load();
            Assert.AreEqual(15, boss.MaxHealth);
            Assert.AreEqual(7, boss.PhaseHealth);
            Assert.AreEqual("7/7", boss.GetComponent<BossHealthBar>().DisplayedHealth);
            yield return new WaitForSeconds(1.2f);
            foreach (float aspect in new[] { 20f / 9f, 21f / 9f })
            {
                float halfHeight = 6.5f; // boss arena preserves floor depth across phone aspect ratios
                float cameraY = Camera.main.transform.position.y;
                float highestHealth = boss.transform.position.y + 2.85f + 12f / 12.8f + 0.4f;
                Assert.Less(highestHealth, cameraY + halfHeight, "the levitating boss health must fit a wide phone");
                Assert.Greater(player.transform.position.y, cameraY - halfHeight + 0.4f, "player feet must also remain visible");
            }
            var maps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(maps.First(map => map.name.Trim() == "Ground").cellBounds.size.x, 40);
            Assert.GreaterOrEqual(maps.First(map => map.name.Trim() == "Ground").cellBounds.size.y, 28);
            Assert.AreEqual(0, maps.First(map => map.name.Trim() == "Collision").GetUsedTilesCount());
            var wallShape = maps.First(map => map.name.Trim() == "Collision").GetComponent<CompositeCollider2D>();
            if (wallShape) Assert.AreEqual(0, wallShape.pathCount, "the old baked wall outline must also be gone");
            Assert.IsNull(GameObject.Find("Encounter Door"), "a zero-wave encounter must not open the exit");
            Assert.IsNull(Object.FindAnyObjectByType<Door>());
            dash.Value = true;
            player.rb.position = new Vector2(60f, 16f); //beyond the old floor and room perimeter
            Physics2D.SyncTransforms();
            yield return new WaitForSeconds(2f);
            var floor = maps.First(map => map.name.Trim() == "Ground");
            Assert.IsTrue(floor.HasTile(floor.WorldToCell(player.transform.position)));
            Assert.IsTrue(Object.FindAnyObjectByType<BossArenaController>().PlayArea.Contains(player.transform.position),
                "movement must stop at the fixed arena's edge");
            var viewport = Camera.main.WorldToViewportPoint(player.transform.position);
            Assert.IsTrue(viewport.x > 0.02f && viewport.x < 0.98f && viewport.y > 0.02f && viewport.y < 0.98f,
                "the bounded player must remain inside the fixed camera frame");

            var circle = player.GetComponentInChildren<CircleCollider2D>();
            Vector2 offset = circle.bounds.center - player.transform.position;
            player.rb.position = (Vector2)boss.GetComponent<BoxCollider2D>().bounds.center - offset;
            Physics2D.SyncTransforms();
            yield return StoryTestLoading.WaitFor(() => boss.CurrentHealth == 14, 4f, "ram damage to boss");
            yield return null;
            Assert.AreEqual(6, boss.PhaseHealth);
            Assert.AreEqual("6/7", boss.GetComponent<BossHealthBar>().DisplayedHealth);
            Assert.IsNull(Object.FindAnyObjectByType<Door>());
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator IceWaitsForTheLastOfFiveEnemiesBeforeShowingTheRedHair()
        {
            yield return Load();
            dash.Value = true;
            var settings = new SerializedObject(boss);
            settings.FindProperty("damageCooldown").floatValue = 0f;
            settings.ApplyModifiedProperties();
            int ready = 0, deaths = 0;
            boss.OnPhaseTwoReady.Subscribe(() => ready++);
            boss.OnDeath.Subscribe(_ => deaths++);
            for (int hit = 0; hit < 7; hit++) boss.TakeDamage(new SDamageData(1, player.gameObject));
            Assert.AreEqual(8, boss.CurrentHealth);
            Assert.IsTrue(boss.IsFrozen);
            boss.TakeDamage(new SDamageData(100, player.gameObject));
            Assert.AreEqual(8, boss.CurrentHealth, "ice is invulnerable");
            yield return StoryTestLoading.WaitFor(() => boss.SummonedEnemies.Count == 5, 20f, "five summons");
            Assert.AreEqual(5, boss.SummonedEnemies.Select(enemy => enemy.name).Distinct().Count());
            Assert.AreEqual(3, boss.SummonedEnemies.Count(enemy => enemy.GetComponent<LunebladeEnemy>()));
            Assert.AreEqual(PlagueDoctorBoss.EPhase.Frozen, boss.Phase);
            Assert.AreEqual("0/7", boss.GetComponent<BossHealthBar>().DisplayedHealth);
            Capture("frozen-five-enemies");

            for (int i = 0; i < 4; i++)
            {
                var health = boss.SummonedEnemies[i].GetComponent<IDamageable>();
                health.TakeDamage(new SDamageData(health.MaxHealth, player.gameObject));
            }
            yield return StoryTestLoading.WaitFor(() => boss.SummonedEnemies.Take(4).All(enemy => !enemy || !enemy.activeInHierarchy),
                10f, "first four enemies dead");
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(PlagueDoctorBoss.EPhase.Frozen, boss.Phase, "one enemy still holds the ice closed");
            Assert.AreEqual(0, ready);
            Assert.AreEqual(settings.FindProperty("iceShatter.frames").GetArrayElementAtIndex(2).objectReferenceValue,
                boss.GetComponent<SpriteRenderer>().sprite, "blue ice must not crack while one summon remains");
            var last = boss.SummonedEnemies[4].GetComponent<IDamageable>();
            last.TakeDamage(new SDamageData(last.MaxHealth, player.gameObject));
            yield return StoryTestLoading.WaitFor(() => boss.Phase == PlagueDoctorBoss.EPhase.RedHaired,
                20f, "ice shatter and red hair");
            yield return null;
            Assert.AreEqual(1, ready);
            Assert.AreEqual(0, deaths, "phase one is not boss death");
            Assert.AreEqual(8, boss.PhaseHealth);
            Assert.AreEqual("8/8", boss.GetComponent<BossHealthBar>().DisplayedHealth);
            StringAssert.Contains("Phase Two Top Down X2", AssetDatabase.GetAssetPath(boss.GetComponent<SpriteRenderer>().sprite));
            Assert.IsNull(Object.FindAnyObjectByType<Door>());
            Assert.AreEqual(0, hazards.ActiveProjectileCount);
            Assert.AreEqual(0, hazards.ActiveStrikeCount);
            Capture("red-hair-eight-health");
            var placeholder = boss.GetComponent<BossPhaseTwoCombat>();
            Assert.IsNotNull(placeholder);
            yield return StoryTestLoading.WaitFor(() => placeholder.CurrentAction.HasValue, 5f,
                "phase-two action preview after the last summon dies");
            Assert.AreEqual(PlagueDoctorBoss.EPhase.RedHaired, boss.Phase);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator ProjectilesAndLightningRespectColourDashWarningsAndPause()
        {
            yield return Load();
            player.OnColorChangeEvents(EColor.Red);
            var health = player.GetComponent<IDamageable>();
            Vector2 centre = player.GetComponentInChildren<CircleCollider2D>().bounds.center;
            hazards.FireProjectile(centre - Vector2.right * 2f, Vector2.right, EColor.Red, 10f);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(10, health.CurrentHealth, "same-colour projectile must pass through");
            dash.Value = true;
            hazards.FireProjectile(centre - Vector2.right * 2f, Vector2.right, EColor.Blue, 10f);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(10, health.CurrentHealth, "dash must protect from the other colour");
            dash.Value = false;
            hazards.FireProjectile(centre - Vector2.right * 2f, Vector2.right, EColor.Blue, 10f);
            yield return StoryTestLoading.WaitFor(() => health.CurrentHealth == 9, 5f, "opposite-colour projectile hit");
            yield return new WaitForSeconds(1f);
            hazards.ClearHazards();

            centre = player.GetComponentInChildren<CircleCollider2D>().bounds.center;
            hazards.WarnLightning(centre, EColor.Blue, 0);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(9, health.CurrentHealth, "a warning cannot hurt");
            Capture("lightning-warning");
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.7f);
            Assert.AreEqual(9, health.CurrentHealth, "a paused warning must not strike");
            Assert.AreEqual(1, hazards.ActiveStrikeCount);
            player.rb.position += Vector2.right * 3f;
            Physics2D.SyncTransforms();
            Time.timeScale = 1f;
            yield return new WaitForSeconds(hazards.WarningSeconds + 0.7f);
            Assert.AreEqual(9, health.CurrentHealth, "moving out of the locked marker must dodge the strike");

            centre = player.GetComponentInChildren<CircleCollider2D>().bounds.center;
            hazards.WarnLightning(centre, EColor.Red, 1);
            yield return new WaitForSeconds(hazards.WarningSeconds + 0.7f);
            Assert.AreEqual(9, health.CurrentHealth, "same-colour lightning must not hurt");
            hazards.WarnLightning(centre, EColor.Blue, 1);
            yield return StoryTestLoading.WaitFor(() => health.CurrentHealth == 8, 5f, "opposite-colour lightning impact");
            Capture("lightning-impact");
            yield return new WaitForSeconds(1f);
            dash.Value = true;
            hazards.WarnLightning(centre, EColor.Blue, 0);
            yield return new WaitForSeconds(hazards.WarningSeconds + 0.7f);
            Assert.AreEqual(8, health.CurrentHealth, "dash must also dodge lightning");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator LiveBossUsesAllThreeProjectilePatterns()
        {
            yield return Load(true);
            dash.Value = true;
            var settings = new SerializedObject(boss);
            settings.FindProperty("idleSeconds").vector2Value = new Vector2(0.1f, 0.1f);
            settings.FindProperty("castWeight").floatValue = 1f;
            settings.FindProperty("walkThrowWeight").floatValue = 0f;
            settings.FindProperty("levitateWeight").floatValue = 0f;
            settings.FindProperty("walkLoops").vector2IntValue = new Vector2Int(1, 1);
            settings.ApplyModifiedProperties();
            var seen = new HashSet<PlagueDoctorBoss.EPattern>();
            int most = 0;
            int previousTotal = hazards.TotalProjectilesEmitted;
            float end = Time.time + 35f;
            while (Time.time < end && seen.Count < 3)
            {
                if (hazards.ActiveProjectileCount > 0 && hazards.TotalProjectilesEmitted != previousTotal)
                {
                    if (seen.Add(boss.CurrentPattern)) Capture("projectiles-" + boss.CurrentPattern);
                    most = Mathf.Max(most, hazards.ActiveProjectileCount);
                }
                previousTotal = hazards.TotalProjectilesEmitted;
                yield return null;
            }
            Assert.AreEqual(3, seen.Count, "stream, circle and grouped barrages must all occur");
            Assert.Greater(hazards.TotalProjectilesEmitted, 50);
            Assert.Greater(most, 20);
            Assert.LessOrEqual(most, 640, "projectile allocation is bounded for Android");
        }

        private static void Capture(string name)
        {
            // World-space health, projectile batches and telegraphs are rendered together for visual QA.
            var camera = Camera.main;
            var oldTarget = camera.targetTexture;
            var oldPosition = camera.transform.position;
            var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                var boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
                camera.transform.position = boss.transform.position + new Vector3(0f, 1.5f, -10f);
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs/boss-phase1-shots");
                File.WriteAllBytes($"Logs/boss-phase1-shots/{name}.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                camera.transform.position = oldPosition;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image);
            }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception && (stackTrace.Contains("Boss") || stackTrace.Contains("Luneblade") ||
                stackTrace.Contains("BiformisEmitter") || stackTrace.Contains("PlayerUnit")))
                exceptions.Add(condition + "\n" + stackTrace);
        }
    }
}
#endif
