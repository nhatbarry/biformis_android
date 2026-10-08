#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Variables;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Game;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.UnitSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    public class BossPhaseTwoGameplayTests
    {
        private PlagueDoctorBoss boss;
        private BossPhaseTwoCombat combat;
        private BossPhaseTwoEffects effects;
        private PlayerUnit player;
        private UnitHealth health;
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
            var manager = Object.FindAnyObjectByType<GameManager>();
            if (manager) manager.UnlockDimension();
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        private IEnumerator Load()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 6");
            boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
            boss.GetComponent<BossRoaming>().enabled = false;
            combat = boss.GetComponent<BossPhaseTwoCombat>();
            effects = boss.GetComponent<BossPhaseTwoEffects>();
            var settings = new SerializedObject(combat);
            settings.FindProperty("automaticAttacks").boolValue = false;
            settings.ApplyModifiedProperties();
            player = Object.FindAnyObjectByType<PlayerUnit>();
            health = player.GetComponent<UnitHealth>();
            dash = (BoolVariableSO)new SerializedObject(player).FindProperty("isDashing").objectReferenceValue;
            boss.PreviewPhaseTwo();
            yield return StoryTestLoading.WaitFor(() => boss.Phase == PlagueDoctorBoss.EPhase.RedHaired, 15f, "phase-two reveal");
            yield return new WaitForSeconds(0.2f);
        }
        private void Place(Vector2 position, EColor colour)
        {
            GameManager.Instance.LockDimension(colour);
            player.rb.position = position;
            player.transform.position = new Vector3(position.x, position.y, player.transform.position.z);
            player.rb.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms();
        }
        private IEnumerator Finish() => StoryTestLoading.WaitFor(() => !combat.CurrentAction.HasValue, 8f, "attack recovery");

        [UnityTest, Timeout(90000)]
        public IEnumerator NativeWalkAnimatesAndEveryAttackPlaysInAllFourDirections()
        {
            yield return Load();
            dash.Value = true;
            var directional = boss.GetComponent<BossDirectionalArt>();
            var renderer = boss.GetComponent<SpriteRenderer>();
            var roaming = boss.GetComponent<BossRoaming>();
            Place((Vector2)boss.transform.position + Vector2.right * 5f, EColor.Blue);
            roaming.enabled = true;
            var walked = new HashSet<Sprite>();
            for (float end = Time.time + 0.7f; Time.time < end;)
            {
                walked.Add(renderer.sprite);
                yield return null;
            }
            Assert.Greater(walked.Count, 2, "walk plays source frames rather than wobbling a still sprite");
            roaming.enabled = false;
            var centre = Object.FindAnyObjectByType<BossArenaController>().PlayArea.center;
            foreach (var direction in new[] { Vector2.down, Vector2.right, Vector2.up, Vector2.left })
            {
                foreach (BossPhaseTwoCombat.EAction action in System.Enum.GetValues(typeof(BossPhaseTwoCombat.EAction)))
                {
                    effects.ClearAll();
                    boss.GetComponent<Rigidbody2D>().position = centre;
                    boss.transform.position = new Vector3(centre.x, centre.y, boss.transform.position.z);
                    Place(centre + direction * 3f, EColor.Blue);
                    Assert.IsTrue(combat.TryAttack(action, EColor.Red));
                    yield return StoryTestLoading.WaitFor(() => combat.CurrentFrame >= 2, 4f, "directional anticipation");
                    Assert.AreEqual(BossDirectionalArt.ViewFor(direction), directional.CurrentDirection);
                    var clip = directional.AttackClip(action, direction);
                    Assert.AreEqual(clip.frames[combat.CurrentFrame], renderer.sprite);
                    StringAssert.Contains("/" + directional.CurrentDirection + "/", AssetDatabase.GetAssetPath(renderer.sprite));
                    Capture("native-" + directional.CurrentDirection + "-" + action);
                    yield return Finish();
                }
            }
            Assert.AreEqual(10, health.CurrentHealth);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator RangedShotUsesTheFloorAndNewArtFacesAllFourDirections()
        {
            yield return Load();
            dash.Value = true;
            var bossArt = boss.GetComponent<SpriteRenderer>();
            foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                effects.ClearAll();
                Place((Vector2)boss.transform.position + direction * 2.2f, EColor.Blue);
                yield return null;
                var playerArt = player.GetComponentsInChildren<SpriteRenderer>().Single(art => art.name == "Blue");
                Assert.AreEqual("Character", bossArt.sortingLayerName);
                if (direction == Vector2.up) Assert.Less(playerArt.sortingOrder, bossArt.sortingOrder, "player above boss goes behind");
                if (direction == Vector2.down) Assert.Greater(playerArt.sortingOrder, bossArt.sortingOrder, "player below boss goes in front");
                var directional = boss.GetComponent<BossDirectionalArt>();
                Assert.AreEqual(BossDirectionalArt.ViewFor(direction), directional.CurrentDirection);
                StringAssert.Contains("Phase Two Top Down X2", AssetDatabase.GetAssetPath(directional.CurrentView));
                int before = effects.TotalShotsEmitted;
                Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.RangedCharge, EColor.Red));
                yield return StoryTestLoading.WaitFor(() => effects.TotalShotsEmitted == before + 1, 4f, "floor-directed shot");
                var streak = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Single(line => line.name == "Shot Streak Colour");
                Vector2 travel = (streak.GetPosition(1) - streak.GetPosition(0)).normalized;
                Assert.Greater(Vector2.Dot(travel, direction), 0.97f,
                    "aim is based on floor positions, including targets directly above the boss");
                if (direction == Vector2.up) Capture("top-down-north-shot");
                yield return Finish();
            }
            Assert.AreEqual(10, health.CurrentHealth);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator BossRoamsInBothPhasesAndHoldsOnlyForChargedAttacks()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 6");
            boss=Object.FindAnyObjectByType<PlagueDoctorBoss>();
            var tuning=new SerializedObject(boss);
            tuning.FindProperty("idleSeconds").vector2Value=new Vector2(100f,100f);tuning.ApplyModifiedProperties();
            var origin=boss.transform.position;
            yield return new WaitForSeconds(0.7f);
            Assert.Greater(Vector2.Distance(origin,boss.transform.position),0.3f,"hooded boss roams between attacks");
            yield return Load();
            var roaming=boss.GetComponent<BossRoaming>();roaming.enabled=true;
            Place((Vector2)boss.transform.position+Vector2.right*5f,EColor.Red);
            origin=boss.transform.position;
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Vector2.Distance(origin,boss.transform.position),0.3f,"phase two roams");
            Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.RangedCharge,EColor.Red));
            yield return new WaitForFixedUpdate();
            origin=boss.transform.position;
            yield return new WaitForSeconds(0.6f);
            Assert.Less(Vector2.Distance(origin,boss.transform.position),0.02f,"charge holds position");
            yield return Finish();
            origin=boss.transform.position;
            Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.HorizontalSlash,EColor.Red));
            yield return new WaitForSeconds(0.35f);
            Assert.Greater(Vector2.Distance(origin,boss.transform.position),0.15f,"slash continues roaming during warning");
        }

        [Test]
        public void BladeShaderAndPerFrameMaskPreserveTheApprovedArtwork()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab");
            var material = prefab.GetComponent<SpriteRenderer>().sharedMaterial;
            Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader));
            Assert.IsTrue(material.HasProperty("_BladeTint"));
            Assert.IsTrue(material.HasProperty("_HitEffectAmount"));
            var settings = new SerializedObject(prefab.GetComponent<BossPhaseTwoCombat>());
            foreach (var property in new[] { "horizontalSlash", "rangedCharge", "dashStab", "verticalSlash" })
            {
                var poses = settings.FindProperty(property + ".bladeEndpoints");
                Assert.AreEqual(settings.FindProperty(property + ".frames").arraySize, poses.arraySize);
                for (int i = 0; i < poses.arraySize; i++)
                {
                    var value = poses.GetArrayElementAtIndex(i).vector4Value;
                    Assert.Greater(Vector2.Distance(new Vector2(value.x, value.y), new Vector2(value.z, value.w)), 0.1f);
                }
            }
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator MeleeWarningPrecedesDamageAndColourAndDashProtectThePlayer()
        {
            yield return Load();
            Vector2 position = (Vector2)boss.transform.position + Vector2.right * 2.2f;
            Place(position, EColor.Red);
            Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.HorizontalSlash, EColor.Red));
            Assert.IsTrue(combat.WarningVisible);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(10, health.CurrentHealth, "warning cannot hurt");
            Capture("slash-red-warning");
            yield return Finish();
            Assert.AreEqual(10, health.CurrentHealth, "same-colour melee must be harmless");
            Place(position, EColor.Red);
            Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.HorizontalSlash, EColor.Blue));
            yield return new WaitForSeconds(0.15f);
            Capture("slash-blue-warning");
            yield return StoryTestLoading.WaitFor(() => health.CurrentHealth == 9, 5f, "opposite-colour slash");
            Assert.IsFalse(combat.WarningVisible);
            yield return Finish();
            Assert.AreEqual(9, health.CurrentHealth, "one slash must not apply repeated damage");
            yield return StoryTestLoading.WaitFor(() => !health.IsInvincibilityFrameOn, 5f, "damage cooldown");
            Place(position, EColor.Red);
            dash.Value = true;
            Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.HorizontalSlash, EColor.Blue));
            yield return Finish();
            Assert.AreEqual(9, health.CurrentHealth, "dash must protect from melee too");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator DashWorksAcrossArenaTracksTheWarningAndStabsThroughTheTarget()
        {
            yield return Load();
            Vector2 origin = boss.transform.position;
            Place(origin + Vector2.right * 7f, EColor.Red);
            Assert.IsTrue(combat.CanDashAtPlayer);
            Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.DashStab, EColor.Blue));
            Assert.IsTrue(combat.WarningVisible);
            yield return new WaitForSeconds(0.2f);
            Assert.Less(Vector4.Distance(boss.GetComponent<BossDirectionalArt>().KnifeColour,
                BossPhaseTwoEffects.Colour(EColor.Blue)), 0.01f, "the windup knife shows the incoming colour");
            Capture("dash-warning");
            Place(origin + new Vector2(3f, 2f), EColor.Red); //warning tracks until release
            Vector2 locked = player.rb.position;
            yield return StoryTestLoading.WaitFor(() => effects.TotalAfterimagesEmitted >= 12, 4f, "dense dash afterimages");
            Assert.Greater(effects.ActiveAfterimageCount, 8);
            Capture("dash-afterimages");
            yield return Finish();
            Assert.Less(Vector2.Distance(locked, combat.LockedTarget),0.05f);
            Assert.Greater(Vector2.Dot((Vector2)boss.transform.position - locked, (locked-origin).normalized), 0.4f);
            Assert.Less(Vector2.Distance(boss.transform.position, combat.LastDashEnd), 0.02f);
            Assert.AreEqual(9, health.CurrentHealth, "early sidesteps are tracked; react to colour or dash at release");
            Assert.IsTrue(boss.GetComponent<CircleCollider2D>().enabled);
            Assert.IsTrue(boss.GetComponent<BoxCollider2D>().enabled);

            boss.GetComponent<Rigidbody2D>().position = origin;
            boss.transform.position = origin;
            yield return StoryTestLoading.WaitFor(() => !health.IsInvincibilityFrameOn, 5f, "damage cooldown");
            Place(origin + Vector2.right * 3f, EColor.Red);
            Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.DashStab, EColor.Blue));
            yield return StoryTestLoading.WaitFor(() => health.CurrentHealth == 8, 5f, "dash crossing an opposite-colour player");
            yield return Finish();
            Assert.AreEqual(8, health.CurrentHealth);
            Assert.AreEqual(8, boss.CurrentHealth, "a stationary player must not automatically ram a dashing boss");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ChargedShotWarnsThenFiresOneInstantStreakWithLaunchImpactAndPauses()
        {
            yield return Load();
            Place((Vector2)boss.transform.position + Vector2.right * 6f, EColor.Blue);
            dash.Value = true;
            bool frozenAtRelease = false;
            combat.OnSkillCue.Subscribe(cue =>
            {
                if (cue == BossPhaseTwoCombat.ECue.SpawnRangedSkill) frozenAtRelease = HitStop.IsWaiting && Time.timeScale == 0f;
            });
            Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.RangedCharge, EColor.Red));
            Assert.IsTrue(combat.WarningVisible);
            yield return StoryTestLoading.WaitFor(() => combat.CurrentFrame == 3, 4f, "charge pose");
            Assert.IsTrue(effects.IsCharging);
            Assert.IsTrue(combat.WarningVisible, "the coloured ! remains throughout windup");
            Assert.Less(Vector4.Distance(combat.WarningColour, BossPhaseTwoEffects.Colour(EColor.Red)), 0.01f);
            Capture("ranged-charge");
            yield return StoryTestLoading.WaitFor(() => effects.TotalShotsEmitted == 1, 4f, "single-shot release");
            Assert.IsFalse(effects.IsCharging);
            Assert.IsFalse(combat.WarningVisible);
            Assert.IsTrue(frozenAtRelease);
            Assert.IsTrue(Object.FindAnyObjectByType<BossArenaController>().IsShaking);
            Assert.GreaterOrEqual(effects.ShotSpeed, 1000f);
            Vector2 target = combat.LockedTarget;
            var streak = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None).Single(line => line.name == "Shot Streak Colour");
            Assert.Greater(Vector3.Distance(streak.GetPosition(0), streak.GetPosition(1)), 20f);
            Assert.Less(Vector4.Distance(streak.startColor, BossPhaseTwoEffects.Colour(EColor.Red)), 0.01f);
            Place(player.rb.position + Vector2.up * 1.5f, EColor.Blue);
            yield return StoryTestLoading.WaitFor(() => !HitStop.IsWaiting, 3f, "launch hit-stop ends");
            Assert.AreEqual(target, combat.LockedTarget);
            Capture("single-shot-streak");
            Time.timeScale = 0f;
            var tint = streak.startColor;
            yield return new WaitForSecondsRealtime(0.35f);
            Assert.AreEqual(tint, streak.startColor, "a real pause freezes streak fade");
            Time.timeScale = 1f;
            boss.TakeDamage(new SDamageData(1, player.gameObject));
            yield return StoryTestLoading.WaitFor(() => effects.ActiveProjectileCount == 0, 4f, "ram clears phase-two projectiles");
            yield return Finish();
            Assert.AreEqual(1, effects.TotalShotsEmitted);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator ExtremelyFastShotSweepsThePlayerAndOnlyAcceptedDamageShakes()
        {
            yield return Load();
            Place((Vector2)boss.transform.position + new Vector2(4f, -1f), EColor.Red);
            var rules = boss.GetComponent<BossHazards>();
            var arena = Object.FindAnyObjectByType<BossArenaController>();
            Vector2 centre = rules.PlayerHitPosition;
            effects.FireShot(centre - Vector2.right * 5f, Vector2.right, EColor.Red);
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(10, health.CurrentHealth);
            Assert.AreEqual(0, rules.TotalPlayerImpacts);
            effects.FireShot(centre - Vector2.right * 5f, Vector2.right, EColor.Blue);
            yield return StoryTestLoading.WaitFor(() => health.CurrentHealth == 9, 3f, "swept opposite-colour hit");
            Assert.AreEqual(1, rules.TotalPlayerImpacts);
            Assert.IsTrue(HitStop.IsWaiting);
            Assert.IsTrue(arena.IsShaking);
            rules.HitPlayer(EColor.Blue);
            Assert.AreEqual(1, rules.TotalPlayerImpacts, "invincibility frames do not generate another impact");
            yield return StoryTestLoading.WaitFor(() => !HitStop.IsWaiting, 3f, "hit-stop recovery");
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(arena.IsShaking);
            Assert.AreEqual(1f, Time.timeScale);
            yield return StoryTestLoading.WaitFor(() => !health.IsInvincibilityFrameOn, 4f, "damage cooldown");
            dash.Value = true;
            effects.FireShot(rules.PlayerHitPosition - Vector2.right * 5f, Vector2.right, EColor.Blue);
            yield return new WaitForSeconds(0.15f);
            Assert.AreEqual(9, health.CurrentHealth);
            Assert.AreEqual(1, rules.TotalPlayerImpacts);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator OneVerticalSlashEmitsFourWideRandomColouredAuras()
        {
            yield return Load();
            Place((Vector2)boss.transform.position + Vector2.right * 6f, EColor.Blue);
            dash.Value = true;
            int slashEvents = 0;
            combat.OnSkillCue.Subscribe(cue => { if (cue == BossPhaseTwoCombat.ECue.SpawnSwordAura) slashEvents++; });
            Assert.IsTrue(combat.TryAttack(BossPhaseTwoCombat.EAction.VerticalSlash, EColor.Red));
            yield return StoryTestLoading.WaitFor(() => effects.TotalWavesEmitted == 1, 4f, "first aura");
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1, effects.TotalWavesEmitted, "pause must also stop the remaining aura schedule");
            Time.timeScale = 1f;
            yield return StoryTestLoading.WaitFor(() => effects.TotalWavesEmitted == 4, 5f, "four auras");
            Assert.AreEqual(1, slashEvents, "one animation strike must produce the entire burst");
            Assert.AreEqual(4, combat.LastAuraColours.Count);
            Assert.GreaterOrEqual(effects.WaveWidth, 5f);
            var waves = Object.FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                .Where(line => line.name == "Aura Colour").OrderBy(line => line.transform.parent.GetSiblingIndex()).ToArray();
            Assert.AreEqual(4, waves.Length);
            for (int i = 0; i < 4; i++) Assert.Less(Vector4.Distance(BossPhaseTwoEffects.Colour(combat.LastAuraColours[i]), waves[i].startColor), 0.009f);
            Capture("four-getsuga-waves");
            yield return Finish();
            Assert.AreEqual(10, health.CurrentHealth);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AuraUsesColourAndDashRulesAndCanBeDodgedSideways()
        {
            yield return Load();
            Vector2 position = (Vector2)boss.transform.position + new Vector2(3f, -1.5f);
            Place(position, EColor.Red);
            Vector2 centre = player.GetComponentInChildren<CircleCollider2D>().bounds.center;
            effects.FireWave(centre - Vector2.right * 3f, Vector2.right, EColor.Red);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(10, health.CurrentHealth);
            effects.ClearProjectiles();
            effects.FireWave(centre - Vector2.right * 3f, Vector2.right, EColor.Blue);
            yield return StoryTestLoading.WaitFor(() => health.CurrentHealth == 9, 5f, "opposite-colour aura");
            yield return StoryTestLoading.WaitFor(() => !health.IsInvincibilityFrameOn, 5f, "damage cooldown");
            dash.Value = true;
            centre = player.GetComponentInChildren<CircleCollider2D>().bounds.center;
            effects.FireWave(centre - Vector2.right * 3f, Vector2.right, EColor.Blue);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(9, health.CurrentHealth);
            effects.ClearProjectiles();
            dash.Value = false;
            Place(position, EColor.Red);
            centre = player.GetComponentInChildren<CircleCollider2D>().bounds.center;
            effects.FireWave(centre - Vector2.right * 3f, Vector2.right, EColor.Blue);
            Place(position + Vector2.up * 3.5f, EColor.Red);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(9, health.CurrentHealth, "moving outside the crescent must dodge it");
        }

        private static void Capture(string name)
        {
            var camera = Camera.main;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs/boss-phase2-combat-shots");
                File.WriteAllBytes($"Logs/boss-phase2-combat-shots/{name}.png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image);
            }
        }
        private void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && (trace.Contains("Boss") || trace.Contains("Health"))) exceptions.Add(message + "\n" + trace);
        }
    }
}
#endif
