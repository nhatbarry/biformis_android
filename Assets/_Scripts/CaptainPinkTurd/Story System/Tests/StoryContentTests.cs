#if UNITY_EDITOR
using System;
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
            ["layout"] = new[] { "top", "bottom" },
            ["music"] = new[] { "ending", "stop" },
        };
        private static readonly string[] CastIds = { "A", "B", "B_Bed", "B_Floor", "Level3", "Teen", "Villain", "Mom", "Doctor", "Box", "none",
            "Room", "Lever", "Trapdoor", "CageFront", "Spotlight", "Captives", "Villain_Op", "Level1", "Shaft", "B_Fall", "Hospital",
            "Track", "B_FB", "A_FB", "Vignette", "Dungeon", "DRoom", "Door", "Gap", "Bed_D", "Bed_Empty", "Vent", "TeenB", "Villain_D",
            "Caption", "DarkDungeon", "DarkRoom", "A4", "B4", "Bedroom", "WallShadow", "BSit", "Drown", "Montage", "CloseUp",
            "EndGlow", "EndWorld", "A_End", "B_End", "BossEnd", "ABMerged" };
        //clips of the actors with a StageActorAnimation in the Story Cutscene scene (the Aseprite tags)
        private static readonly Dictionary<string, string[]> AnimClips = new()
        {
            ["B_Bed"] = new[] { "sleep", "wake", "pant", "sit_idle" },
            ["B_Floor"] = new[] { "sleep", "wake", "pant", "idle" },
            ["Teen"] = new[] { "idle", "vanish" },
            ["Villain"] = new[] { "appear", "idle", "walk", "stab" },
            ["Villain_Op"] = new[] { "appear_remote", "idle_remote", "talk_remote", "walk_remote", "press_remote", "pull_lever" },
            ["Lever"] = new[] { "idle", "open" },
            ["Trapdoor"] = new[] { "idle", "open" },
            ["Captives"] = new[] { "idle", "struggle", "merge", "merged", "merged_red" },
            ["B_Fall"] = new[] { "fall", "land", "lie", "getup", "idle" },
            ["Mom"] = new[] { "idle", "talk", "cry" },
            ["Doctor"] = new[] { "idle", "talk" },
            ["Door"] = new[] { "idle", "bang" },
            ["Gap"] = new[] { "glow", "glow_fade", "blood_seep", "blood_still" },
            ["Vent"] = new[] { "closed", "open" },
            ["TeenB"] = new[] { "idle", "idle_faded", "fade", "vanish" },
            ["Villain_D"] = new[] { "appear", "idle", "talk", "walk", "touch_door" },
            ["A4"] = new[] { "idle", "run" },
            ["B4"] = new[] { "idle", "run" },
            ["Bedroom"] = new[] { "idle", "bang" },
            ["BSit"] = new[] { "hug", "rock", "up", "shiver", "slam", "down" },
            ["A_FB"] = new[] { "idle", "run", "reach", "wait", "hold" },
            ["B_FB"] = new[] { "idle", "run", "hesitate", "reach", "touch", "hold" },
            ["Drown"] = new[] { "intro", "loop" },
            ["Montage"] = new[] { "m1", "m2", "m3", "m4" },
            ["CloseUp"] = new[] { "hold_loop" },
            ["A_End"] = new[] { "idle", "run" },
            ["B_End"] = new[] { "idle", "run" },
            ["BossEnd"] = new[] { "breathe", "dust" },
            ["ABMerged"] = new[] { "flicker", "split" },
        };
        //who speaks each line of the team's preview GIFs (the lines themselves are the user's, copied verbatim)
        private static readonly string[] AfterLevel3Speakers =
        {
            "B", "A", "B", "A", "B", "B", "A", "B", //the school track
            "TeenB", "B", "TeenB", "B", "TeenB", "B", "Villain", "B", "Villain", "A", "B", "A", "Villain", "A", "Villain", //the dungeon
        };
        private static readonly string[] Level4EndSpeakers = { "A", "A", "B", "A", "B", "A", "B", "B", "A", "Doctor", "Mom", "Doctor" };
        //the Scene_HoiUc_AnhEm preview's eight lines (placeholders until the final dialogue), L2 in two parts like the preview
        private static readonly string[] BrothersMemorySpeakers = { "A", "A", "A", "B", "A", "A", "B", "A", "A" };

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
        public void TheEndingOpensWhereTheGlassShatterLeftTheBossAndThePlayer(string inkPath)
        {
            //Level 6's glass shatter slides the boss and the player to these places, then the knot opens on them
            var story = new Ink.Runtime.Story(AssetDatabase.LoadAssetAtPath<TextAsset>(inkPath).text);
            story.ChoosePathString("Ending");
            //the opening's tags-only line rides on the first line (the boss's)
            string first = story.Continue();
            var tags = story.currentTags;
            var boss = Presentation.GlassShatter.BossStagePosition;
            var player = Presentation.GlassShatter.PlayerStagePosition;
            Assert.Contains(FormattableString.Invariant($"move:BossEnd:{boss.x},{boss.y}"), tags, $"{inkPath}: {string.Join(" ", tags)}");
            Assert.Contains(FormattableString.Invariant($"move:ABMerged:{player.x},{player.y}"), tags, $"{inkPath}: {string.Join(" ", tags)}");

            //five lines each for the boss and A&B, taking turns, then A calls B home
            var speakers = new List<string> { tags.First(tag => tag.StartsWith("speaker:"))["speaker:".Length..] };
            Assert.IsNotEmpty(first.Trim());
            while (story.canContinue)
            {
                if (story.Continue().Trim().Length > 0)
                    speakers.Add(story.currentTags.First(tag => tag.StartsWith("speaker:"))["speaker:".Length..]);
            }
            var expected = Enumerable.Range(0, 10).Select(i => i % 2 == 0 ? "Villain" : "AB").Append("A");
            CollectionAssert.AreEqual(expected, speakers, $"{inkPath}: speakers {string.Join(", ", speakers)}");
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

        [TestCase(InkVi)]
        [TestCase(InkEn)]
        public void TheOpeningEndsWithBGettingUpOnLevel1(string inkPath)
        {
            //the GIF's dialogue ends on the villain's line; the lever, the fall and Level 1's floor are a tags-only
            //line after it, which ink must still hand over (and no "system starting up" line follows any more)
            var story = new Ink.Runtime.Story(AssetDatabase.LoadAssetAtPath<TextAsset>(inkPath).text);
            story.ChoosePathString("Intro");
            var tagsAfterLastLine = new List<string>();
            string lastSpeaker = null;
            while (story.canContinue)
            {
                if (story.Continue().Trim().Length > 0)
                {
                    tagsAfterLastLine.Clear();
                    foreach (var tag in story.currentTags)
                        if (tag.StartsWith("speaker:")) lastSpeaker = tag["speaker:".Length..];
                }
                else tagsAfterLastLine.AddRange(story.currentTags);
            }
            string tags = string.Join(" ", tagsAfterLastLine);
            Assert.AreEqual("Villain", lastSpeaker, $"{inkPath}: the opening's last line isn't the villain's");
            Assert.Contains("anim:Villain_Op:pull_lever", tagsAfterLastLine, $"{inkPath}: tags after the last line: {tags}");
            Assert.Contains("cast:Level1,Shaft,B_Fall", tagsAfterLastLine, $"{inkPath}: tags after the last line: {tags}");
            Assert.Contains("anim:B_Fall:land", tagsAfterLastLine, $"{inkPath}: tags after the last line: {tags}");
        }

        [TestCase(InkVi)]
        [TestCase(InkEn)]
        public void TheSceneAfterLevel3FollowsItsGif(string inkPath)
        {
            var lines = Lines(inkPath, "AfterLevel3");
            CollectionAssert.AreEqual(AfterLevel3Speakers, lines.Take(AfterLevel3Speakers.Length).Select(l => l.speaker).ToArray(),
                $"{inkPath}: speakers of the after-Level-3 scene");
            int lastVillain = AfterLevel3Speakers.Length - 1;
            Assert.Contains("anim:Villain_D:touch_door", lines[lastVillain].tags, $"{inkPath}: the villain touches the door before 'then die'");
            //after "then die": the knocks fade, blood seeps, the villain walks back to the empty bed, beep... then the hospital
            var after = lines[lastVillain + 1].tags;
            string tags = string.Join(" ", after);
            Assert.AreEqual("Mom", lines[lastVillain + 1].speaker, $"{inkPath}: the hospital's first line should follow");
            foreach (var tag in new[] { "knock:Door:0.9", "anim:Gap:blood_seep", "move:Dungeon:-320:2.1", "cast:Caption", "cast:Hospital,Mom,Doctor" })
                Assert.Contains(tag, after, $"{inkPath}: staging before the hospital: {tags}");
            Assert.Less(after.IndexOf("cast:Caption"), after.IndexOf("cast:Hospital,Mom,Doctor"), "the caption comes before the hospital");
        }

        [TestCase(InkVi)]
        [TestCase(InkEn)]
        public void TheEndOfLevel4FollowsItsGif(string inkPath)
        {
            var lines = Lines(inkPath, "Level4_End");
            CollectionAssert.AreEqual(Level4EndSpeakers, lines.Take(Level4EndSpeakers.Length).Select(l => l.speaker).ToArray(),
                $"{inkPath}: speakers of the end of Level 4");
            //the box is handed over by the player before the first line of the past
            var first = lines[0].tags;
            string tags = string.Join(" ", first);
            foreach (var tag in new[] { "reach:B4:A4:-56", "hold", "attach:Box:B4:-18,4", "cast:Bedroom,WallShadow,BSit" })
                Assert.Contains(tag, first, $"{inkPath}: staging before the past: {tags}");
            Assert.Less(first.IndexOf("reach:B4:A4:-56"), first.IndexOf("hold"), "the hold waits for the reach");
            Assert.Less(first.IndexOf("hold"), first.IndexOf("cast:Bedroom,WallShadow,BSit"), "the past starts after the box");
            //"Anh! Anh ơi!" comes after B beats his head; the hospital after the beeps
            Assert.Contains("anim:BSit:down", lines[8].tags, $"{inkPath}: A's last cry comes after the slams");
            var hospital = lines[9].tags;
            Assert.Less(hospital.IndexOf("cast:Caption"), hospital.IndexOf("cast:Hospital,Mom,Doctor"), "the beeps come before the hospital");
            Assert.Contains("anim:Mom:cry", lines[12].tags, $"{inkPath}: Mom cries after the doctor's last line");
        }

        [TestCase(InkVi)]
        [TestCase(InkEn)]
        public void TheBrothersMemoryFollowsItsPack(string inkPath)
        {
            var lines = Lines(inkPath, "WhiteRoom_3");
            CollectionAssert.AreEqual(BrothersMemorySpeakers, lines.Take(BrothersMemorySpeakers.Length).Select(l => l.speaker).ToArray(),
                $"{inkPath}: speakers of the brothers' memory");
            //track -> drowning (panel at the top, off A) -> track -> montage -> track
            Assert.Contains("cast:Track,B_FB,A_FB,Vignette", lines[0].tags, $"{inkPath}: it opens on the track");
            Assert.Contains("cast:Drown", lines[1].tags, $"{inkPath}: the drowning comes with L2a");
            Assert.Contains("layout:top", lines[1].tags, $"{inkPath}: the drowning's lines stand at the top");
            Assert.Contains("layout:bottom", lines[3].tags, $"{inkPath}: back on the track the panel goes down again");
            Assert.Contains("anim:Montage:m1", lines[4].tags, $"{inkPath}: the montage comes with L4");
            var montage = lines[5].tags;
            Assert.Less(montage.IndexOf("anim:Montage:m2"), montage.IndexOf("anim:Montage:m3"), "m2 before m3");
            Assert.Less(montage.IndexOf("anim:Montage:m4"), montage.IndexOf("cast:Track,B_FB,A_FB,Vignette"), "m4 before the track");
            foreach (var line in lines.Take(BrothersMemorySpeakers.Length))
            {
                int cut = line.tags.FindIndex(t => t.StartsWith("pixel:cut"));
                if (cut >= 0) Assert.Less(cut, line.tags.FindIndex(t => t.StartsWith("cast:")), $"{inkPath}: the pixel cut must freeze the stage before the cast changes: {line.text}");
            }
            //after the last line: A holds out a hand first, B hesitates, then the player walks B over and takes it
            var hands = lines[BrothersMemorySpeakers.Length].tags;
            int aReach = hands.IndexOf("anim:A_FB:reach"), hesitate = hands.IndexOf("anim:B_FB:hesitate");
            int reach = hands.IndexOf("reach:B_FB:A_FB:-120:stage.hold"), hold = hands.IndexOf("hold");
            int bReach = hands.IndexOf("anim:B_FB:reach"), closeUp = hands.IndexOf("cast:CloseUp");
            Assert.That(aReach >= 0 && aReach < hesitate && hesitate < reach && reach < hold && hold < bReach && bReach < closeUp,
                $"{inkPath}: hand-holding order: {string.Join(" ", hands)}");
            Assert.AreEqual(BrothersMemorySpeakers.Length + 1, lines.Count, $"{inkPath}: nothing should follow the close-up");
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

        /// <summary>
        /// The lines a cutscene shows, as DialogueManager groups them: a blank (tags-only) line's tags go with the next
        /// line, and lines without a speaker tag keep the previous speaker.
        /// </summary>
        private static List<(string speaker, string text, List<string> tags)> Lines(string inkPath, string knot)
        {
            var story = new Ink.Runtime.Story(AssetDatabase.LoadAssetAtPath<TextAsset>(inkPath).text);
            story.ChoosePathString(knot);
            var lines = new List<(string, string, List<string>)>();
            var pending = new List<string>();
            string speaker = null;
            while (story.canContinue)
            {
                string text = story.Continue().Trim();
                pending.AddRange(story.currentTags);
                if (text.Length == 0) continue;
                foreach (var tag in pending)
                    if (tag.StartsWith("speaker:")) speaker = tag["speaker:".Length..];
                lines.Add((speaker, text, pending));
                pending = new List<string>();
            }
            if (pending.Count > 0) lines.Add((speaker, "", pending));
            return lines;
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
            if (tag.Trim() == "hold") return; //waits for the stage, see the struggle tag

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
                    Assert.That(move.Length is 2 or 3 && move[1].Split(',').Length <= 2 && move[1].Split(',').All(IsNumber) && move.Skip(2).All(IsNumber),
                        $"{knot}: move tag '{tag}' is not move:Actor:x[,y][:seconds]");
                    Assert.Contains(move[0], CastIds, $"{knot}: unknown actor {move[0]}");
                    break;
                case "struggle":
                    var struggle = value.Split(':');
                    Assert.That(struggle.Length == 2 && int.TryParse(struggle[1], out int count) && count > 0, $"{knot}: struggle tag '{tag}' is not struggle:Actor:count");
                    Assert.Contains("struggle", AnimClips[struggle[0]], $"{knot}: {struggle[0]} has no struggle clip");
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
                case "flip":
                case "banging":
                    var toggle = value.Split(':');
                    Assert.That(toggle.Length == 2 && toggle[1] is "on" or "off", $"{knot}: {key} tag '{tag}' is not {key}:Actor:on|off");
                    Assert.Contains(toggle[0], CastIds, $"{knot}: unknown actor {toggle[0]}");
                    if (key == "banging") Assert.Contains("bang", AnimClips[toggle[0]], $"{knot}: {toggle[0]} has no bang clip");
                    break;
                case "knock":
                    var knock = value.Split(':');
                    Assert.That(knock.Length == 2 && IsNumber(knock[1]), $"{knot}: knock tag '{tag}' is not knock:Actor:level");
                    Assert.Contains("bang", AnimClips[knock[0]], $"{knot}: {knock[0]} has no bang clip");
                    break;
                case "fade":
                    var fade = value.Split(':');
                    Assert.That(fade.Length is 1 or 2 && (fade.Length == 1 || IsNumber(fade[1])) &&
                                (fade[0] == "in" || (fade[0].Length == 6 && ColorUtility.TryParseHtmlString("#" + fade[0], out _))),
                        $"{knot}: fade tag '{tag}' is not fade:RRGGBB|in[:seconds]");
                    break;
                case "attach":
                    var attach = value.Split(':');
                    Assert.Contains(attach[0], CastIds, $"{knot}: unknown actor {attach[0]}");
                    Assert.That((attach.Length == 2 && attach[1] == "none") ||
                                (attach.Length == 3 && CastIds.Contains(attach[1]) && attach[2].Split(',').Length == 2 && attach[2].Split(',').All(IsNumber)),
                        $"{knot}: attach tag '{tag}' is not attach:Actor:Target:dx,dy or attach:Actor:none");
                    break;
                case "reach":
                    var reach = value.Split(':');
                    Assert.That(reach.Length is 3 or 4 && CastIds.Contains(reach[1]) && IsNumber(reach[2]), $"{knot}: reach tag '{tag}' is not reach:Walker:Target:maxX[:promptKey]");
                    if (reach.Length == 4)
                        Assert.IsTrue(Localization.TryGet(reach[3], out _) && Localization.TryGet(reach[3] + "_touch", out _), $"{knot}: prompt {reach[3]} (and _touch) not in the localization table");
                    Assert.IsTrue(AnimClips[reach[0]].Contains("idle") && AnimClips[reach[0]].Contains("run"), $"{knot}: {reach[0]} can't walk (no idle/run clips)");
                    break;
                case "alpha":
                    var alpha = value.Split(':');
                    Assert.That(alpha.Length is 2 or 3 && CastIds.Contains(alpha[0]) && alpha.Skip(1).All(IsNumber), $"{knot}: alpha tag '{tag}' is not alpha:Actor:opacity[:seconds]");
                    break;
                case "shake":
                    var shake = value.Split(':');
                    Assert.That(shake.Length is 1 or 2 && shake.All(IsNumber), $"{knot}: shake tag '{tag}' is not shake:units[:seconds]");
                    break;
                case "follow":
                    var follow = value.Split(':');
                    Assert.That(value == "none" || (follow.Length == 3 && CastIds.Contains(follow[0]) && CastIds.Contains(follow[1]) &&
                                                    follow[2].Split(',').Length == 2 && follow[2].Split(',').All(IsNumber)),
                        $"{knot}: follow tag '{tag}' is not follow:World:Walker:minX,maxX or follow:none");
                    break;
                case "pixel":
                    var pixel = value.Split(':');
                    Assert.That(pixel.Length is 1 or 2 && (pixel.Length == 1 || IsNumber(pixel[1])) &&
                                (pixel[0] is "cut" or "in" || (pixel[0].Length == 6 && ColorUtility.TryParseHtmlString("#" + pixel[0], out _))),
                        $"{knot}: pixel tag '{tag}' is not pixel:cut|in|RRGGBB[:seconds]");
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
