#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CaptainPinkTurd.Core.Localization;
using CaptainPinkTurd.Scene.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// Guards the ink scripts against typos that would only show up mid-cutscene: every knot the story runs must exist
    /// in both languages, and every tag must be one the dialogue manager or cutscene stage understands.
    /// </summary>
    public class StoryContentTests
    {
        private const string StoryDataPath = "Assets/Game Data/Story/Story Data.asset";
        private const string InkVi = "Assets/Ink Dialogue/Biformis_Story.json";
        private const string InkEn = "Assets/Ink Dialogue/Biformis_Story_EN.json";

        private static readonly string[] BarkKnots = { "Bark_Hurt", "Bark_Pain", "Corridor_Bark" };
        private static readonly Dictionary<string, string[]> StageTagValues = new()
        {
            ["bg"] = new[] { "white", "black", "hospital", "past" },
            ["fx"] = new[] { "shake", "flash", "red", "fade_black", "fade_white", "fade_in" },
            ["sfx"] = new[] { "beep", "stop", "thud" },
        };
        private static readonly string[] CastIds = { "A", "B", "Teen", "Mom", "Doctor", "Box", "none" };

        private static StoryData Data => AssetDatabase.LoadAssetAtPath<StoryData>(StoryDataPath);

        [TestCase(InkVi)]
        [TestCase(InkEn)]
        public void EveryKnotTheStoryUsesRunsWithKnownTags(string inkPath)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(inkPath).text;
            var knots = Data.Steps.Where(s => s.type == EStoryStepType.Cutscene).Select(s => s.knotName).Concat(BarkKnots);

            foreach (var knot in knots)
            {
                var story = new Ink.Runtime.Story(json);
                story.ChoosePathString(knot);
                int lines = 0;
                while (story.canContinue)
                {
                    if (story.Continue().Trim().Length > 0) lines++;
                    foreach (var tag in story.currentTags) AssertKnownTag(knot, tag);
                }
                Assert.Greater(lines, 0, $"{inkPath}: knot {knot} has no lines");
            }
        }

        [Test]
        public void BothLanguagesHaveTheSameLineCountPerKnot()
        {
            foreach (var knot in Data.Steps.Where(s => s.type == EStoryStepType.Cutscene).Select(s => s.knotName))
            {
                Assert.AreEqual(CountLines(InkVi, knot), CountLines(InkEn, knot), $"knot {knot} differs between languages");
            }
        }

        [Test]
        public void EveryStoryStepSceneIsInTheBuild()
        {
            Assert.Greater(Data.Steps.Count, 0);
            foreach (var step in Data.Steps)
            {
                Assert.IsTrue(Application.CanStreamedLevelBeLoaded(step.sceneName), $"{step.sceneName} is not in the build settings");
            }
        }

        private static int CountLines(string inkPath, string knot)
        {
            var story = new Ink.Runtime.Story(AssetDatabase.LoadAssetAtPath<TextAsset>(inkPath).text);
            story.ChoosePathString(knot);
            int lines = 0;
            while (story.canContinue) if (story.Continue().Trim().Length > 0) lines++;
            return lines;
        }

        private static void AssertKnownTag(string knot, string tag)
        {
            int separator = tag.IndexOf(':');
            Assert.Greater(separator, 0, $"{knot}: tag '{tag}' is not key:value");
            string key = tag[..separator].Trim();
            string value = tag[(separator + 1)..].Trim();

            switch (key)
            {
                case "speaker":
                    Assert.IsTrue(Localization.TryGet("speaker." + value, out _), $"{knot}: speaker {value} has no name in the localization table");
                    break;
                case "cast":
                    foreach (var id in value.Split(',')) Assert.Contains(id.Trim(), CastIds, $"{knot}: unknown actor {id}");
                    break;
                default:
                    Assert.IsTrue(StageTagValues.ContainsKey(key), $"{knot}: unknown tag {tag}");
                    Assert.Contains(value, StageTagValues[key], $"{knot}: unknown {key} value {value}");
                    break;
            }
        }
    }
}
#endif
