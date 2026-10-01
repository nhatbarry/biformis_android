#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
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
        private static readonly string[] CastIds = { "A", "B", "B_Bed", "B_Floor", "Level3", "Teen", "Villain", "Mom", "Doctor", "Box", "none" };
        //clips of the actors with a StageActorAnimation in the Story Cutscene scene (the Aseprite tags)
        private static readonly Dictionary<string, string[]> AnimClips = new()
        {
            ["B_Bed"] = new[] { "sleep", "wake", "pant", "sit_idle" },
            ["B_Floor"] = new[] { "sleep", "wake", "pant", "idle" },
            ["Teen"] = new[] { "idle", "vanish" },
            ["Villain"] = new[] { "appear", "idle", "walk", "stab" },
        };

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

        [TestCase(InkVi)]
        [TestCase(InkEn)]
        public void TheWhiteRoomEndsWithBWakingOnTheNextLevelsFloor(string inkPath)
        {
            //B's last line comes after the stab and the cut to Level 3's floor: its staging rides on that line
            var story = new Ink.Runtime.Story(AssetDatabase.LoadAssetAtPath<TextAsset>(inkPath).text);
            story.ChoosePathString("WhiteRoom_1");
            var lastLineTags = new List<string>();
            while (story.canContinue)
            {
                if (story.Continue().Trim().Length > 0) lastLineTags = new List<string>(story.currentTags);
            }
            string tags = string.Join(" ", lastLineTags);
            Assert.Contains("speaker:B", lastLineTags, $"{inkPath}: last line tags: {tags}");
            Assert.Contains("anim:Villain:stab", lastLineTags, $"{inkPath}: last line tags: {tags}");
            Assert.Contains("cast:Level3,B_Floor", lastLineTags, $"{inkPath}: last line tags: {tags}");
            Assert.Less(lastLineTags.IndexOf("anim:Villain:stab"), lastLineTags.IndexOf("cast:Level3,B_Floor"), "the cut comes after the stab");
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

        private static bool IsNumber(string text) => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

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
                case "move":
                    var move = value.Split(':');
                    Assert.That(move.Length is 2 or 3 && move.Skip(1).All(IsNumber), $"{knot}: move tag '{tag}' is not move:Actor:x[:seconds]");
                    Assert.Contains(move[0], CastIds, $"{knot}: unknown actor {move[0]}");
                    break;
                case "wait":
                    Assert.IsTrue(IsNumber(value), $"{knot}: wait tag '{tag}' is not wait:seconds");
                    break;
                case "bg":
                    var bg = value.Split(':');
                    Assert.That(bg.Length is 1 or 2 && (bg.Length == 1 || IsNumber(bg[1])), $"{knot}: bg tag '{tag}' is not bg:colour[:seconds]");
                    Assert.IsTrue(StageTagValues["bg"].Contains(bg[0]) || (bg[0].Length == 6 && ColorUtility.TryParseHtmlString("#" + bg[0], out _)),
                        $"{knot}: unknown backdrop colour {bg[0]}");
                    break;
                case "anim":
                    var parts = value.Split(':');
                    Assert.That(parts.Length is 2 or 3, $"{knot}: anim tag '{tag}' is not anim:Actor:clip[:seconds]");
                    if (parts.Length == 3)
                        Assert.IsTrue(float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out _), $"{knot}: anim delay {parts[2]} is not a number");
                    Assert.IsTrue(AnimClips.ContainsKey(parts[0]), $"{knot}: actor {parts[0]} has no animation");
                    Assert.Contains(parts[1], AnimClips[parts[0]], $"{knot}: {parts[0]} has no clip {parts[1]}");
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
