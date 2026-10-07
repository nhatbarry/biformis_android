#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Variables;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// The plague doctor boss in Level 6: its sheets import at the size the team's pack gives them (a re-import that
    /// shrinks a wide sheet or changes a pivot breaks the pixels), and in the live level it plays each of its actions.
    /// </summary>
    public class PlagueDoctorBossTests
    {
        private const string PrefabPath = "Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab";

        private readonly List<string> bossExceptions = new();
        private BoolVariableSO dash;

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
        }

        // clip, cell size, pixels per unit, pivot - cells and pivots as Boss_Assets/README.md gives them; pixels per
        // unit 2.5 times smaller than its 512 / 32, so the boss stands 2.5 times as big
        private static readonly (string clip, int cell, float ppu, Vector2 pivot)[] Sheets =
        {
            ("idle", 512, 204.8f, new Vector2(0.5f, 0f)),
            ("cast", 48, 12.8f, new Vector2(0.5f, 0f)),
            ("walkThrow", 48, 12.8f, new Vector2(0.5f, 0f)),
            ("levitate", 64, 12.8f, new Vector2(0.5f, 0.0625f)),
        };

        [Test]
        public void BossSheetsImportAtThePacksSize()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "boss prefab missing");
            var boss = new SerializedObject(prefab.GetComponent<PlagueDoctorBoss>());

            foreach (var (clip, cell, ppu, pivot) in Sheets)
            {
                var frames = boss.FindProperty($"{clip}.frames");
                var durations = boss.FindProperty($"{clip}.frameMilliseconds");
                Assert.Greater(frames.arraySize, 0, $"{clip} has no frames");
                Assert.AreEqual(frames.arraySize, durations.arraySize, $"{clip}: every frame needs a duration");

                for (int i = 0; i < frames.arraySize; i++)
                {
                    var sprite = frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite;
                    Assert.IsNotNull(sprite, $"{clip} frame {i} lost its sprite");
                    Assert.Greater(durations.GetArrayElementAtIndex(i).intValue, 0, $"{clip} frame {i} has no duration");
                    Assert.AreEqual(new Rect(i * cell, 0, cell, cell), sprite.rect, $"{clip} frame {i} isn't its cell of the sheet");
                    Assert.AreEqual(cell * frames.arraySize, sprite.texture.width, $"{clip}'s sheet was resized on import (max size?)");
                    Assert.AreEqual(ppu, sprite.pixelsPerUnit, $"{clip} pixels per unit");
                    Assert.AreEqual(pivot * cell, sprite.pivot, $"{clip} frame {i} pivot");
                    Assert.AreEqual(FilterMode.Point, sprite.texture.filterMode, $"{clip} must not be filtered");
                }
            }

            var redHair = boss.FindProperty("redHair").objectReferenceValue as Sprite;
            Assert.IsNotNull(redHair, "red-haired pose missing");
            Assert.AreEqual(12.8f, redHair.pixelsPerUnit);
            Assert.AreEqual(new Vector2(32f, 4f), redHair.pivot, "the red-haired pose must stand where the shatter ends");

            var transformation = boss.FindProperty("iceShatter.frames");
            var timing = boss.FindProperty("iceShatter.frameMilliseconds");
            Assert.AreEqual(12, transformation.arraySize);
            Assert.AreEqual(2, boss.FindProperty("frozenFrame").intValue, "complete blue ice must hold before red cracks");
            int total = 0;
            for (int i = 0; i < 12; i++)
            {
                var sprite = (Sprite)transformation.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.AreEqual(new Rect(i * 414, 0, 414, 442), sprite.rect);
                Assert.AreEqual(4968, sprite.texture.width, "the new sheet must not shrink on import");
                Assert.AreEqual(85.33f, sprite.pixelsPerUnit, 0.001f);
                Assert.AreEqual(new Vector2(192f, 93f), sprite.pivot);
                total += timing.GetArrayElementAtIndex(i).intValue;
            }
            Assert.AreEqual(2440, total);
            Assert.AreEqual(100, timing.GetArrayElementAtIndex(7).intValue, "aura lasts exactly 100 ms");
            Assert.AreEqual(transformation.GetArrayElementAtIndex(11).objectReferenceValue,
                boss.FindProperty("phaseTwoRestingPose").objectReferenceValue, "rest at the aura-free knife pose");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator BossPlaysEachActionInLevel6()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 6");
            var boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
            Assert.IsNotNull(boss, "Level 6 has no boss");
            var player = Object.FindAnyObjectByType<PlayerUnit>();
            dash = new SerializedObject(player).FindProperty("isDashing").objectReferenceValue as BoolVariableSO;
            dash.Value = true; //this test watches animations, not player survival
            var sprite = boss.GetComponent<SpriteRenderer>();
            var settings = new SerializedObject(boss);
            settings.FindProperty("idleSeconds").vector2Value = new Vector2(0.2f, 0.2f);
            settings.ApplyModifiedProperties();

            float home = boss.transform.position.x;
            var playArea = Object.FindAnyObjectByType<BossArenaController>().PlayArea;

            //only one action allowed at a time, so the next pick is that one
            Only(settings, "castWeight");
            yield return WaitForClip(sprite, Frames(settings, "cast"), "cast");

            Only(settings, "walkThrowWeight");
            yield return WaitForClip(sprite, Frames(settings, "walkThrow"), "walk and throw");
            float left = home, right = home;
            yield return Watch(2f, () =>
            {
                left = Mathf.Min(left, boss.transform.position.x);
                right = Mathf.Max(right, boss.transform.position.x);
            });
            Assert.Greater(right - left, 0.2f, "the boss didn't walk while throwing");
            Assert.GreaterOrEqual(left, playArea.xMin, "the boss walked out of the arena");
            Assert.LessOrEqual(right, playArea.xMax, "the boss walked out of the arena");

            Only(settings, "levitateWeight");
            var levitate = settings.FindProperty("levitate.frames");
            var risen = levitate.GetArrayElementAtIndex(levitate.arraySize - 1).objectReferenceValue as Sprite;
            var standing = levitate.GetArrayElementAtIndex(0).objectReferenceValue as Sprite;
            yield return WaitForClip(sprite, Frames(settings, "levitate"), "levitate");
            yield return StoryTestLoading.WaitFor(() => sprite.sprite == risen, 10f, "the top of the rise");
            //it comes back down the same way
            yield return StoryTestLoading.WaitFor(() => sprite.sprite == standing, 10f, "the landing");

            Assert.IsEmpty(bossExceptions, string.Join("\n\n", bossExceptions));
        }

        // ------------------------------------------------------------------ helpers

        private static void Only(SerializedObject settings, string weight)
        {
            foreach (var name in new[] { "castWeight", "walkThrowWeight", "levitateWeight" })
                settings.FindProperty(name).floatValue = name == weight ? 1f : 0f;
            settings.ApplyModifiedProperties();
        }

        private static HashSet<Sprite> Frames(SerializedObject settings, string clip)
        {
            var frames = settings.FindProperty($"{clip}.frames");
            return new HashSet<Sprite>(Enumerable.Range(0, frames.arraySize)
                .Select(i => frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite));
        }

        private static IEnumerator WaitForClip(SpriteRenderer sprite, HashSet<Sprite> frames, string what)
        {
            //long enough for an ice shatter already under way to finish first
            yield return StoryTestLoading.WaitFor(() =>
            {
                LogAssert.ignoreFailingMessages = true;
                return frames.Contains(sprite.sprite);
            }, 20f, what);
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
            if (type == LogType.Exception && stackTrace.Contains(nameof(PlagueDoctorBoss))) bossExceptions.Add(condition + "\n" + stackTrace);
        }
    }
}
#endif
