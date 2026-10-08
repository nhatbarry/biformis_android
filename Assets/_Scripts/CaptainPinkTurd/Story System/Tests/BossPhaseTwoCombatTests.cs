#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Core.Interfaces;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Variables;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Game;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    public class BossPhaseTwoCombatTests
    {
        private readonly List<string> exceptions = new();
        private static readonly string[] Properties = { "horizontalSlash", "rangedCharge", "dashStab", "verticalSlash" };
        private static readonly float[] Ppu = { 106.24f, 101.97f, 108.37f, 96.85f };
        private static readonly int[][] Timing =
        {
            new[] { 160, 110, 160, 50, 60, 90, 100, 110, 220 },
            new[] { 180, 120, 140, 220, 260, 60, 60, 140, 220 },
            new[] { 180, 110, 180, 40, 50, 60, 140, 130, 260 },
            new[] { 180, 120, 160, 320, 100, 40, 100, 140, 240 },
        };

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
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        [Test]
        public void NativeTopDownPackKeepsAllTwentyClipsAndSourceFrameTimings()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab");
            var settings = new SerializedObject(prefab.GetComponent<BossPhaseTwoCombat>());
            Assert.IsFalse(settings.FindProperty("previewPhaseTwoOnStart").boolValue, "normal play must still start with phase one");
            for (int action = 0; action < Properties.Length; action++)
            {
                var frames = settings.FindProperty(Properties[action] + ".frames");
                var timing = settings.FindProperty(Properties[action] + ".frameMilliseconds");
                Assert.AreEqual(Timing[action].Length, frames.arraySize);
                CollectionAssert.AreEqual(Timing[action], Enumerable.Range(0, timing.arraySize)
                    .Select(i => timing.GetArrayElementAtIndex(i).intValue));
                for (int i = 0; i < frames.arraySize; i++)
                {
                    var sprite = frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    Assert.IsNotNull(sprite);
                    Assert.AreEqual(Ppu[action], sprite.pixelsPerUnit, 0.001f);
                    Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                    Assert.Greater(sprite.bounds.size.y, i==0 ? 2f : 1.5f, "standing pose retains scale; crouched dash frames may be shorter");
                    Assert.Less(sprite.bounds.size.y, 6f, "full native canvas retains its transparent padding without an extra scale multiplier");
                }
            }
            var art = new SerializedObject(prefab.GetComponent<BossDirectionalArt>()).FindProperty("directions");
            Assert.AreEqual(4, art.arraySize);
            int count = 0;
            for (int direction = 0; direction < 4; direction++)
            {
                var set = art.GetArrayElementAtIndex(direction);
                foreach (var property in new[] { "walk", "horizontalSlash", "rangedCharge", "dashStab", "verticalSlash" })
                {
                    var clip = set.FindPropertyRelative(property);
                    Assert.AreEqual(9, clip.FindPropertyRelative("frames").arraySize);
                    int total = 0;
                    for (int frame = 0; frame < 9; frame++)
                    {
                        var sprite = clip.FindPropertyRelative("frames").GetArrayElementAtIndex(frame).objectReferenceValue as Sprite;
                        Assert.IsNotNull(sprite);
                        StringAssert.Contains("Phase Two Top Down X2", AssetDatabase.GetAssetPath(sprite));
                        Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode);
                        Assert.Greater(clip.FindPropertyRelative("bladeWidths").GetArrayElementAtIndex(frame).floatValue, 0f);
                        total += clip.FindPropertyRelative("frameMilliseconds").GetArrayElementAtIndex(frame).intValue;
                        count++;
                    }
                    Assert.AreEqual(property == "walk" ? 800 : property == "horizontalSlash" ? 1060 : property == "dashStab" ? 1150 : 1400, total);
                }
            }
            Assert.AreEqual(180, count);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator AllFourCombatActionsKeepTheirOriginalFrameCuesAndPause()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 6");
            var boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
            var placeholder = boss.GetComponent<BossPhaseTwoCombat>();
            var combatSettings = new SerializedObject(placeholder);
            combatSettings.FindProperty("automaticAttacks").boolValue = false;
            combatSettings.ApplyModifiedProperties();
            var player = Object.FindAnyObjectByType<PlayerUnit>();
            var health = player.GetComponent<IDamageable>();
            var hazards = boss.GetComponent<BossHazards>();
            var sprite = boss.GetComponent<SpriteRenderer>();
            var cues = new List<BossPhaseTwoCombat.ECue>();
            var cueFrames = new List<int>();
            placeholder.OnSkillCue.Subscribe(cue => { cues.Add(cue); cueFrames.Add(placeholder.CurrentFrame); });
            boss.PreviewPhaseTwo();
            Vector3 origin = boss.transform.position;
            yield return null;
            Assert.AreEqual("8/8", boss.GetComponent<BossHealthBar>().DisplayedHealth);
            Assert.IsFalse(boss.GetComponent<BoxCollider2D>().enabled, "animation preview does not enable damage reception");
            Assert.IsFalse(boss.GetComponent<CircleCollider2D>().isTrigger, "solid feet stay present");
            boss.TakeDamage(new SDamageData(1, player.gameObject));
            Assert.AreEqual(8, boss.CurrentHealth);
            yield return StoryTestLoading.WaitFor(() => boss.Phase == PlagueDoctorBoss.EPhase.RedHaired,
                15f, "mask reveal and zoom out");
            Assert.IsTrue(boss.GetComponent<BoxCollider2D>().enabled);
            Assert.IsFalse(boss.IsInvulnerable, "the remaining eight points are now playable");
            int playerHealth = health.CurrentHealth;
            var dash = (BoolVariableSO)new SerializedObject(player).FindProperty("isDashing").objectReferenceValue;
            dash.Value = true;
            foreach (BossPhaseTwoCombat.EAction action in System.Enum.GetValues(typeof(BossPhaseTwoCombat.EAction)))
            {
                player.rb.position = (Vector2)boss.transform.position + Vector2.right * 3f;
                Physics2D.SyncTransforms();
                Assert.IsTrue(placeholder.TryAttack(action, EColor.Red));
                yield return StoryTestLoading.WaitFor(() => placeholder.CurrentAction == action && placeholder.CurrentFrame >= 3,
                    15f, action.ToString());
                if (action == BossPhaseTwoCombat.EAction.HorizontalSlash)
                {
                    var pausedSprite = sprite.sprite;
                    int pausedFrame = placeholder.CurrentFrame, cueCount = cues.Count;
                    Time.timeScale = 0f;
                    yield return new WaitForSecondsRealtime(0.4f);
                    Assert.AreEqual(pausedSprite, sprite.sprite);
                    Assert.AreEqual(pausedFrame, placeholder.CurrentFrame);
                    Assert.AreEqual(cueCount, cues.Count);
                    Time.timeScale = 1f;
                }
                Capture(action.ToString(), boss);
                yield return StoryTestLoading.WaitFor(() => placeholder.CurrentAction == null, 5f, "return to red-haired pose");
            }
            CollectionAssert.AreEqual(new[]
            {
                BossPhaseTwoCombat.ECue.SlashHit, BossPhaseTwoCombat.ECue.SpawnRangedSkill,
                BossPhaseTwoCombat.ECue.BeginDash, BossPhaseTwoCombat.ECue.StabHit,
                BossPhaseTwoCombat.ECue.EndDash, BossPhaseTwoCombat.ECue.SpawnSwordAura,
            }, cues);
            CollectionAssert.AreEqual(new[] { 4, 6, 3, 4, 6, 5 }, cueFrames);
            Assert.AreNotEqual(origin, boss.transform.position, "the dash must now move the boss");
            Assert.AreEqual(playerHealth, health.CurrentHealth);
            Assert.AreEqual(1, boss.GetComponent<BossPhaseTwoEffects>().TotalShotsEmitted);
            Assert.AreEqual(4, boss.GetComponent<BossPhaseTwoEffects>().TotalWavesEmitted);
            Assert.AreEqual(0, hazards.TotalProjectilesEmitted);
            Assert.AreEqual(0, hazards.TotalStrikesWarned);
            Assert.IsNull(Object.FindAnyObjectByType<Door>());
            dash.Value = false;
        }

        private static void Capture(string name, PlagueDoctorBoss boss)
        {
            var camera = Camera.main;
            var oldTarget = camera.targetTexture;
            var oldPosition = camera.transform.position;
            var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.transform.position = boss.transform.position + new Vector3(0f, 1.5f, -10f);
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs/boss-phase2-shots");
                File.WriteAllBytes($"Logs/boss-phase2-shots/{name}.png", image.EncodeToPNG());
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

        private void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && trace.Contains("Boss")) exceptions.Add(message + "\n" + trace);
        }
    }
}
#endif
