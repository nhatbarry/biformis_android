#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.Localization;
using CaptainPinkTurd.InkDialogue;
using CaptainPinkTurd.Scene;
using CaptainPinkTurd.Scene.Story;
using CaptainPinkTurd.Story.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// Guards the supplied montage's timing and composition, and its place before the hospital ending.
    /// The integration test uses the real menu transition and restores the player's original save afterward.
    /// </summary>
    public class RedFinalCutsceneTests
    {
        private const string ResourcesRoot = "Story Ending/Red Final/";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly List<GameObject> testObjects = new();
        private readonly List<IEnumerator> players = new();

        [Serializable] private sealed class TimingSummary
        {
            public int totalDurationMs;
            public TimedShot[] timeline;
            public WorldPoint[] world;
        }

        [Serializable] private sealed class TimedShot
        {
            public string id;
            public int startMs;
            public int endMs;
        }

        [Serializable] private sealed class WorldPoint
        {
            public string id;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var player in players) (player as IDisposable)?.Dispose();
            players.Clear();
            foreach (var testObject in testObjects) if (testObject) Object.DestroyImmediate(testObject);
            testObjects.Clear();
        }

        [Test]
        public void RuntimeResourcesAreTheSuppliedArtworkWithPixelPreservingImports()
        {
            var paths = new List<string> { "backgrounds/wide", "backgrounds/close" };
            string[] ids = { "red", "blue", "mom", "doctor", "teen", "pink", "green", "bunny", "reaper" };
            foreach (string id in ids)
            {
                paths.Add($"characters/{id}/world");
                string animation = id == "red" ? "look_talk" : id == "pink" ? "bob" : id == "bunny" ? "dance" : "clap_talk";
                paths.Add($"characters/{id}/{animation}");
            }

            foreach (string path in paths)
            {
                var texture = Resources.Load<Texture2D>(ResourcesRoot + path);
                Assert.IsNotNull(texture, $"the packaged texture {path} is missing");
                string importedPath = AssetDatabase.GetAssetPath(texture);
                string suppliedPath = Path.Combine(ProjectRoot, "Cutscene_Red_Final", path + ".png");
                if (File.Exists(suppliedPath))
                    Assert.AreEqual(Hash(suppliedPath), Hash(Path.Combine(ProjectRoot, importedPath)), $"{path} was changed while being imported");

                var importer = (TextureImporter)AssetImporter.GetAtPath(importedPath);
                Assert.AreEqual(FilterMode.Point, importer.filterMode, $"{path} is blurred");
                Assert.IsFalse(importer.mipmapEnabled, $"{path} has mipmaps");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, $"{path} is compressed");
                Vector2 expected = path.StartsWith("backgrounds/") ? new Vector2(320, 180)
                    : path.EndsWith("/world") ? new Vector2(64, 64)
                    : path.EndsWith("/look_talk") ? new Vector2(384, 288) : new Vector2(384, 192);
                Assert.AreEqual(expected, new Vector2(texture.width, texture.height), $"{path} changed canvas size");
            }

            var timing = Resources.Load<TextAsset>(ResourcesRoot + "timing_positions");
            Assert.IsNotNull(timing, "the authored timeline is missing");
            string suppliedTiming = Path.Combine(ProjectRoot, "Cutscene_Red_Final", "timing_positions.json");
            if (File.Exists(suppliedTiming))
                Assert.AreEqual(Hash(suppliedTiming), Hash(Path.Combine(ProjectRoot, AssetDatabase.GetAssetPath(timing))),
                    "the authored timing or positions changed");
            var summary = JsonUtility.FromJson<TimingSummary>(timing.text);
            Assert.AreEqual(26120, summary.totalDurationMs);
            Assert.AreEqual(9, summary.world.Length);
            string[] expectedShots = { "flash", "reveal", "wide_hold", "blue", "mom", "doctor", "green_bunny", "reaper", "teen", "pink", "red" };
            Assert.AreEqual(expectedShots.Length, summary.timeline.Length);
            int previousEnd = 0;
            for (int i = 0; i < summary.timeline.Length; i++)
            {
                var shot = summary.timeline[i];
                Assert.AreEqual(expectedShots[i], shot.id);
                Assert.AreEqual(previousEnd, shot.startMs, "the timeline has a gap or overlap at " + shot.id);
                Assert.Greater(shot.endMs, shot.startMs);
                previousEnd = shot.endMs;
            }
            Assert.AreEqual(summary.totalDurationMs, previousEnd);
        }

        [TestCase(0f, 0)]
        [TestCase(119.9f, 0)]
        [TestCase(120f, 1)]
        [TestCase(300f, 2)]
        [TestCase(800f, 3)]
        [TestCase(1599.9f, 5)]
        [TestCase(1600f, 6)]
        [TestCase(1800f, 7)]
        [TestCase(1960f, 8)]
        [TestCase(2520f, 11)]
        [TestCase(2679.9f, 11)]
        [TestCase(2680f, 6)]
        [TestCase(3600f, 11)]
        [TestCase(3760f, 6)]
        [TestCase(100000f, 6)]
        public void RedLooksEachWayOnceAndThenLoopsOnlyItsTalkingFrames(float elapsedMs, int expectedFrame)
        {
            int[] durations = { 120, 180, 500, 120, 180, 500, 200, 160, 200, 160, 200, 160 };
            var method = typeof(RedFinalCutscene).GetMethod("FrameAt", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            Assert.AreEqual(expectedFrame, method.Invoke(null, new object[] { durations, 6, elapsedMs }));
        }

        [Test]
        public void CompositionUsesTheAuthoredFeetAndCloseUpCenters()
        {
            var cutscene = NewCutscene();
            cutscene.Initialize(null);
            var viewport = Field<RectTransform>(cutscene, "viewport");
            Assert.AreEqual(new Vector2(640, 360), viewport.sizeDelta);
            var wide = Field<RectTransform>(cutscene, "wideRoot");
            string[] backToFront = { "mom", "doctor", "blue", "red", "teen", "reaper", "bunny", "pink", "green" };
            for (int i = 0; i < backToFront.Length; i++)
            {
                var actor = (RectTransform)wide.Find(backToFront[i]);
                Assert.IsNotNull(actor);
                Assert.AreEqual(i + 1, actor.GetSiblingIndex(), "feet sorting changed for " + actor.name);
                Assert.AreEqual(new Vector2(0.5f, 0), actor.pivot);
                Assert.AreEqual(new Vector2(128, 128), actor.sizeDelta);
            }
            Assert.AreEqual(new Vector2(-48, -58), ((RectTransform)wide.Find("red")).anchoredPosition);

            ApplyTime(cutscene, 12120f);
            var close = Field<RectTransform>(cutscene, "closeRoot");
            Assert.AreEqual("green_bunny", cutscene.CurrentShotId);
            Assert.IsTrue(close.Find("green").gameObject.activeSelf);
            Assert.IsTrue(close.Find("bunny").gameObject.activeSelf);
            Assert.AreEqual(new Vector2(20, -50), ((RectTransform)close.Find("green")).anchoredPosition);
            Assert.AreEqual(new Vector2(212, -70), ((RectTransform)close.Find("bunny")).anchoredPosition);
            Assert.AreEqual(new Vector2(260, 260), ((RectTransform)close.Find("green")).sizeDelta);
            Assert.AreEqual(new Vector2(220, 220), ((RectTransform)close.Find("bunny")).sizeDelta);

            ApplyTime(cutscene, 22520f);
            Assert.AreEqual("red", cutscene.CurrentShotId);
            Assert.AreEqual(Vector2.zero, ((RectTransform)close.Find("red")).anchoredPosition);
            Assert.AreEqual(new Vector2(336, 336), ((RectTransform)close.Find("red")).sizeDelta);
            Assert.IsFalse(close.Find("green").gameObject.activeSelf);
            Assert.IsFalse(close.Find("bunny").gameObject.activeSelf);
        }

        [TestCase(1200f, 540f)] //20:9 phone
        [TestCase(640f, 480f)] //4:3 tablet
        public void TheCompleteCanvasFitsWideAndTallScreensWithOpaqueBlackBars(float width, float height)
        {
            var parent = new GameObject("Red Final test screen", typeof(RectTransform));
            testObjects.Add(parent);
            ((RectTransform)parent.transform).sizeDelta = new Vector2(width, height);
            var cutscene = NewCutscene();
            cutscene.Initialize(parent.transform);
            typeof(RedFinalCutscene).GetMethod("FitViewport", PrivateInstance).Invoke(cutscene, null);

            var root = Field<RectTransform>(cutscene, "root");
            var viewport = Field<RectTransform>(cutscene, "viewport");
            float expectedScale = Mathf.Min(width / 640f, height / 360f);
            Assert.AreEqual(expectedScale, viewport.localScale.x, 0.001f);
            Assert.AreEqual(expectedScale, viewport.localScale.y, 0.001f);
            Assert.LessOrEqual(viewport.rect.width * expectedScale, root.rect.width + 0.001f);
            Assert.LessOrEqual(viewport.rect.height * expectedScale, root.rect.height + 0.001f);
            Assert.AreEqual(Vector2.zero, viewport.anchoredPosition);
            Assert.AreEqual(Color.black, root.GetComponent<Image>().color, "the uncovered screen must stay opaque black");
            Assert.AreEqual(Vector2.zero, root.anchorMin);
            Assert.AreEqual(Vector2.one, root.anchorMax);
        }

        [Test]
        public void WhiteFlashRevealsTheWideShotBeforeTheFirstCloseUp()
        {
            var cutscene = NewCutscene();
            cutscene.Initialize(null);
            var flash = Field<Image>(cutscene, "flash");
            ApplyTime(cutscene, 119f);
            Assert.AreEqual("flash", cutscene.CurrentShotId);
            Assert.AreEqual(1f, flash.color.a);
            ApplyTime(cutscene, 720f);
            Assert.AreEqual("reveal", cutscene.CurrentShotId);
            Assert.AreEqual(0.5f, flash.color.a, 0.001f);
            ApplyTime(cutscene, 1320f);
            Assert.AreEqual("wide_hold", cutscene.CurrentShotId);
            Assert.AreEqual(0f, flash.color.a);
            Assert.IsTrue(Field<RectTransform>(cutscene, "wideRoot").gameObject.activeSelf);
            ApplyTime(cutscene, 4320f);
            Assert.AreEqual("blue", cutscene.CurrentShotId);
            Assert.IsFalse(Field<RectTransform>(cutscene, "wideRoot").gameObject.activeSelf);
            Assert.IsTrue(Field<RectTransform>(cutscene, "closeRoot").gameObject.activeSelf);
        }

        [Test]
        public void DialogueFollowsEverySpeakerAndWaitsUntilRedFacesForward()
        {
            var cutscene = NewCutscene();
            cutscene.Initialize(null);
            var speakers = new List<string>();
            cutscene.SpeakerChanged += speakers.Add;
            float[] starts = { 4320, 6920, 9520, 12120, 13420, 14720, 17320, 19920 };
            string[] names = { "blue", "mom", "doctor", "green", "bunny", "reaper", "teen", "pink" };
            for (int i = 0; i < starts.Length; i++)
            {
                ApplyTime(cutscene, starts[i]);
                Assert.AreEqual(names[i], speakers[speakers.Count - 1]);
                Assert.AreEqual(i + 1, speakers.Count);
                ApplyTime(cutscene, starts[i] + 600f);
                Assert.AreEqual(i + 1, speakers.Count, "looping mouths must not retrigger the dialogue");
            }
            ApplyTime(cutscene, 22520f);
            Assert.IsNull(speakers[speakers.Count - 1], "Red's head turns must clear the preceding line");
            int count = speakers.Count;
            ApplyTime(cutscene, 23320f);
            ApplyTime(cutscene, 24119f);
            Assert.AreEqual(count, speakers.Count, "Red speaks while looking to the sides");
            ApplyTime(cutscene, 24120f);
            Assert.AreEqual("red", speakers[speakers.Count - 1]);
            Assert.AreEqual("red_06", Field<RectTransform>(cutscene, "closeRoot").Find("red").GetComponent<Image>().sprite.name);
            ApplyTime(cutscene, 26000f);
            Assert.AreEqual(count + 1, speakers.Count);

            var player = cutscene.Play();
            players.Add(player);
            Assert.IsTrue(player.MoveNext());
            ApplyTime(cutscene, 24120f);
            ((IDisposable)player).Dispose();
            players.Remove(player);
            Assert.IsFalse(cutscene.IsPlaying);
            Assert.IsNull(speakers[speakers.Count - 1], "ending or interrupting the montage leaves a dialogue showing");
            Assert.IsFalse(Field<RectTransform>(cutscene, "root").gameObject.activeSelf);
        }

        [UnityTest, Timeout(150000)]
        public IEnumerator EndingUsesInkAndOnePanelWithTopFinaleThenFadesWhiteIntoTheHospital()
        {
            bool previousIgnoreLogs = LogAssert.ignoreFailingMessages;
            float previousTimeScale = Time.timeScale;
            ELanguage previousLanguage = Localization.CurrentLanguage;
            bool hadLanguagePreference = PlayerPrefs.HasKey("language");
            var exceptions = new List<string>();
            Application.LogCallback onLog = (message, stack, type) =>
            {
                if (type == LogType.Exception) exceptions.Add(message + "\n" + stack);
            };
            Application.logMessageReceived += onLog;
            string backup = BackupSaveDirectory();
            DialogueManager manager = null;
            var heard = new List<(string speaker, string line)>();
            void OnLine(DialogueInfo info)
            {
                if (info.line != null) heard.Add((info.speaker, info.line.Trim()));
            }
            try
            {
                StoryFlow.Stop();
                Localization.SetLanguage(ELanguage.Vietnamese);
                LogAssert.ignoreFailingMessages = true;
                SceneManager.LoadScene("Core");
                yield return StoryTestLoading.WaitFor(() => GameObject.Find(StoryMenu.LEVEL_SELECT_BUTTON_NAME) != null, 40f, "the main menu");
                yield return StoryTestLoading.WaitFor(() => !SceneController.Instance.IsBusy, 10f, "the menu transition");
                var data = AssetDatabase.LoadAssetAtPath<StoryData>("Assets/Game Data/Story/Story Data.asset");
                int endingStep = -1;
                for (int i = 0; i < data.Steps.Count; i++)
                    if (data.Steps[i].sceneName == "Story Ending") endingStep = i;
                Assert.GreaterOrEqual(endingStep, 0, "the story has no ending step");
                Object.FindAnyObjectByType<StoryMenu>().StartAtStep(endingStep);
                yield return StoryTestLoading.WaitFor(() =>
                {
                    var sequence = Object.FindAnyObjectByType<EndingSequence>();
                    return sequence && sequence.GetComponent<RedFinalCutscene>();
                }, 40f, "Red Final initialized in the real ending scene");
                var ending = Object.FindAnyObjectByType<EndingSequence>();
                Assert.IsNotNull(ending);
                var cutscene = ending.GetComponent<RedFinalCutscene>();
                Assert.IsNotNull(cutscene, "the actual Story Ending scene did not start Red Final");
                if (SceneController.Instance.IsBusy)
                    Assert.IsFalse(cutscene.IsPlaying, "the montage's flash is hidden behind the loading overlay");
                StoryFlow.Stop(); //the test observes the hospital without marking the player's story complete
                yield return StoryTestLoading.WaitFor(() => !SceneController.Instance.IsBusy && cutscene.IsPlaying,
                    10f, "Red Final playback after the loading overlay");
                Assert.IsTrue(cutscene.IsPlaying);
                var hospital = Field<Image>(ending, "hospitalRoom");
                var whiteFade = Field<Image>(ending, "whiteOverlay");
                manager = DialogueManager.Instance;
                Assert.AreEqual("Assets/Ink Dialogue/Biformis_Story.json", AssetDatabase.GetAssetPath(Field<TextAsset>(manager, "inkJson")));
                Assert.AreEqual("Assets/Ink Dialogue/Biformis_Story_EN.json", AssetDatabase.GetAssetPath(Field<TextAsset>(manager, "inkJsonEnglish")));
                manager.OnDisplayDialogue.Subscribe(OnLine);
                var panel = Object.FindAnyObjectByType<DialoguePanelUI>();
                Assert.IsNotNull(panel, "the ending must use the Story Dialogue System's Ink UI");
                Assert.AreEqual(1, Object.FindObjectsByType<DialoguePanelUI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);
                var content = Field<GameObject>(panel, "contentParent");
                var line = Field<Component>(panel, "dialogueText");
                var speakerName = Field<Component>(panel, "speakerNameText");
                var cutsceneRoot = Field<RectTransform>(cutscene, "root");
                typeof(DialoguePanelUI).GetMethod("SetLayout", PrivateInstance).Invoke(panel, new object[] { "top" });
                AssertStandardPanel(panel, true);
                typeof(DialoguePanelUI).GetMethod("SetLayout", PrivateInstance).Invoke(panel, new object[] { "bottom" });
                AssertStandardPanel(panel);
                Assert.IsFalse(hospital.gameObject.activeSelf, "the hospital is revealed before Red Final");
                var speakers = new List<string>();
                cutscene.SpeakerChanged += speakers.Add;
                Time.timeScale = 0f; //the ending uses real time, including during gameplay hit-stop/pause

                yield return StoryTestLoading.WaitFor(() => cutscene.CurrentShotId == "wide_hold", 5f, "the montage's wide shot");
                Capture(ending, cutscene, "01-wide");
                var cues = new (string shot, string speaker, string text)[]
                {
                    ("blue", "A", "chúc mừng"), ("mom", "Mom", "chúc mừng"), ("doctor", "Doctor", "chúc mừng"),
                    ("green_bunny", "Zoro", "chúc mừng"), ("green_bunny", "Usagi", "Unaa~"),
                    ("reaper", "Reaper", "grrr"), ("teen", "TeenB", "chúc mừng"), ("pink", "CaptainPinkTurd", "*fart*")
                };
                foreach (var cue in cues)
                {
                    yield return StoryTestLoading.WaitFor(() => heard.Count > 0 && heard[heard.Count - 1].speaker == cue.speaker,
                        5f, cue.speaker + "'s Ink line");
                    Assert.AreEqual(cue.shot, cutscene.CurrentShotId, "the line did not follow the character's animation");
                    Assert.IsFalse(hospital.gameObject.activeSelf, "hospital shown during " + cue.shot);
                    Assert.IsTrue(content.activeSelf, "dialogue hidden during " + cue.shot);
                    Assert.AreEqual(cue.text, heard[heard.Count - 1].line);
                    yield return StoryTestLoading.WaitFor(() => Text(line).Trim() == cue.text, 1f, cue.speaker + "'s visible line");
                    Assert.AreEqual(Localization.Get("speaker." + cue.speaker), Text(speakerName));
                    AssertStandardPanel(panel, true);
                    if (cue.speaker == "Usagi") Capture(ending, cutscene, "02-green-bunny");
                    if (cue.speaker == "CaptainPinkTurd") Capture(ending, cutscene, "02b-pink");
                }
                yield return StoryTestLoading.WaitFor(() => cutscene.CurrentShotId == "red", 5f, "Red looking sideways");
                Assert.IsFalse(content.activeSelf, "Red thanks everybody before his talking phase");
                yield return new WaitForSecondsRealtime(0.9f);
                Assert.IsFalse(content.activeSelf, "Red speaks while looking to the other side");
                yield return StoryTestLoading.WaitFor(() => content.activeSelf && Text(line).Trim() == "cảm ơn mọi người",
                    2f, "Red's forward-facing talking phase");
                Assert.AreEqual("red", cutscene.CurrentShotId);
                Assert.AreEqual("B", heard[heard.Count - 1].speaker);
                AssertStandardPanel(panel, true);
                Assert.IsFalse(hospital.gameObject.activeSelf);
                Capture(ending, cutscene, "03-red-thanks");
                var celebration = MusicManager.Instance.GetComponents<AudioSource>()
                    .Single(source => source.clip == Field<AudioClip>(ending, "music") && source.isPlaying);
                Assert.IsFalse(celebration.loop, "the celebration must play the complete recording once");
                Assert.AreEqual(1f, celebration.volume, 0.001f, "the celebration faded before its recording finished");

                yield return StoryTestLoading.WaitFor(() => !cutscene.IsPlaying, 5f, "the montage to finish");
                CollectionAssert.AreEqual(new[] { "blue", "mom", "doctor", "green", "bunny", "reaper", "teen", "pink", null, "red", null }, speakers);
                CollectionAssert.AreEqual(cues.Select(cue => cue.speaker).Append("B"), heard.Select(info => info.speaker));
                Assert.IsTrue(cutsceneRoot.gameObject.activeSelf, "the last Red frame must remain under the white fade");
                Assert.IsFalse(content.activeSelf, "the thank-you line remains over the transition");
                Assert.IsFalse(hospital.gameObject.activeSelf, "hospital appears before the white transition");
                var redImage = Field<RectTransform>(cutscene, "closeRoot").Find("red").GetComponent<Image>();
                string heldFrame = redImage.sprite.name;
                float fadeStarted = Time.realtimeSinceStartup;
                float previousAlpha = whiteFade.color.a;
                bool sawPartialWhite = false;
                while (!hospital.gameObject.activeSelf)
                {
                    Assert.Less(Time.realtimeSinceStartup - fadeStarted, 15f, "the full celebration never hands over to the hospital");
                    if (celebration.isPlaying)
                    {
                        Assert.AreEqual(1f, celebration.volume, 0.001f, "the final pose faded the recording before it finished");
                        Assert.AreEqual(0f, whiteFade.color.a, 0.001f, "the hospital transition began before the recording finished");
                    }
                    Assert.GreaterOrEqual(whiteFade.color.a, previousAlpha - 0.001f);
                    previousAlpha = whiteFade.color.a;
                    sawPartialWhite |= previousAlpha > 0.05f && previousAlpha < 0.95f;
                    Assert.IsTrue(cutsceneRoot.gameObject.activeSelf, "the final Red frame disappears before white covers it");
                    Assert.AreEqual(heldFrame, redImage.sprite.name, "the final Red frame is not held during the fade");
                    yield return null;
                }
                Assert.IsTrue(sawPartialWhite, "the transition must fade gradually to white");
                Assert.IsFalse(celebration.isPlaying, "the celebration must finish before the hospital");
                Assert.AreEqual(1f, whiteFade.color.a, 0.001f, "hospital appears before the screen is fully white");
                Assert.GreaterOrEqual(Time.realtimeSinceStartup - fadeStarted, 1.45f);
                Assert.IsTrue(!cutsceneRoot || !cutsceneRoot.gameObject.activeSelf);
                yield return StoryTestLoading.WaitFor(() => whiteFade.color.a <= 0.001f, 3f, "the white overlay to reveal the hospital");
                yield return StoryTestLoading.WaitFor(() => heard.Count > 9 && heard[heard.Count - 1].speaker == "A",
                    8f, "the first hospital Ink line");
                yield return StoryTestLoading.WaitFor(() => Text(line).Trim() == "Anh...?", 2f, "A's visible hospital line");
                Assert.AreSame(panel, Object.FindAnyObjectByType<DialoguePanelUI>(), "hospital speech must reuse the same dialogue panel");
                AssertStandardPanel(panel);
                Capture(ending, null, "04-hospital");
                yield return StoryTestLoading.WaitFor(() => heard.Count > 10 && heard[heard.Count - 1].speaker == "B",
                    12f, "the second hospital Ink line to auto-advance");
                Assert.AreEqual("Ừ. Anh về rồi.", heard[heard.Count - 1].line);
                Assert.IsFalse(heard.Any(info => info.line.IndexOf("bíp", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    info.line.IndexOf("beep", StringComparison.OrdinalIgnoreCase) >= 0), "the ending reintroduced heart-monitor dialogue");
                Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
            }
            finally
            {
                Time.timeScale = previousTimeScale;
                if (manager) manager.OnDisplayDialogue.Unsubscribe(OnLine);
                Localization.SetLanguage(previousLanguage);
                if (!hadLanguagePreference) { PlayerPrefs.DeleteKey("language"); PlayerPrefs.Save(); }
                Application.logMessageReceived -= onLog;
                LogAssert.ignoreFailingMessages = previousIgnoreLogs;
                RestoreSaveDirectory(backup);
            }
        }

        private RedFinalCutscene NewCutscene()
        {
            var go = new GameObject("Red Final test");
            testObjects.Add(go);
            return go.AddComponent<RedFinalCutscene>();
        }

        private static T Field<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, PrivateInstance);
            Assert.IsNotNull(field, $"{target.GetType().Name}.{name} is missing");
            return (T)field.GetValue(target);
        }

        private static void ApplyTime(RedFinalCutscene cutscene, float milliseconds) =>
            typeof(RedFinalCutscene).GetMethod("ApplyTime", PrivateInstance).Invoke(cutscene, new object[] { milliseconds });

        private static void AssertStandardPanel(DialoguePanelUI panel, bool top = false)
        {
            var rect = (RectTransform)panel.transform;
            float edge = top ? 1f : 0f;
            Assert.AreEqual(new Vector2(0f, edge), rect.anchorMin);
            Assert.AreEqual(new Vector2(1f, edge), rect.anchorMax);
            Assert.AreEqual(new Vector2(0.5f, edge), rect.pivot);
            Assert.AreEqual(DialoguePanelStyle.PanelHeight, rect.rect.height, 0.001f);
            if (top) Assert.LessOrEqual(rect.offsetMax.y, -DialoguePanelStyle.TopMargin, "finale dialogue must fit below the safe top edge");
            else Assert.GreaterOrEqual(rect.offsetMin.y, DialoguePanelStyle.BottomMargin);
            var content = Field<GameObject>(panel, "contentParent");
            Assert.AreEqual(DialoguePanelStyle.AccentColor, content.transform.Find("Dialogue Border").GetComponent<Image>().color);
            Assert.AreEqual(DialoguePanelStyle.PanelColor, content.transform.Find("Back Panel").GetComponent<Image>().color);
            var body = Field<Component>(panel, "dialogueText");
            var name = Field<Component>(panel, "speakerNameText");
            Assert.AreSame(body.GetType().GetProperty("font").GetValue(body), name.GetType().GetProperty("font").GetValue(name));
            Assert.AreEqual(DialoguePanelStyle.TextColor, body.GetType().GetProperty("color").GetValue(body));
            var portrait = Field<GameObject>(panel, "portraitFrame");
            Assert.IsTrue(!portrait || !portrait.activeSelf, "the shared panel must use its full width");
        }

        //The existing test assembly does not directly reference TextMeshPro.
        private static string Text(Component component) => (string)component.GetType().GetProperty("text").GetValue(component);

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string Hash(string path)
        {
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
        }

        private static void Capture(EndingSequence ending, RedFinalCutscene cutscene, string name)
        {
            //Render the actual runtime canvas synchronously; overlay ScreenCapture does not write in batch mode.
            const int width = 1600, height = 900;
            var panel = Object.FindAnyObjectByType<DialoguePanelUI>();
            var canvases = new HashSet<Canvas> { Field<RectTransform>(ending, "stage").GetComponentInParent<Canvas>().rootCanvas };
            if (panel) canvases.Add(panel.GetComponentInParent<Canvas>().rootCanvas);
            var canvasStates = canvases.Select(canvas => (
                canvas, scaler: canvas.GetComponent<CanvasScaler>(), mode: canvas.renderMode, camera: canvas.worldCamera,
                distance: canvas.planeDistance, scale: canvas.scaleFactor,
                scalerEnabled: canvas.GetComponent<CanvasScaler>() && canvas.GetComponent<CanvasScaler>().enabled)).ToList();
            var previousActive = RenderTexture.active;
            var layers = new List<(GameObject gameObject, int layer)>();
            foreach (var canvas in canvases)
                foreach (var child in canvas.GetComponentsInChildren<Transform>(true))
                    layers.Add((child.gameObject, child.gameObject.layer));

            var camera = new GameObject("Red Final QA Camera").AddComponent<Camera>();
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            target.filterMode = FilterMode.Point;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            string directory = Path.Combine(ProjectRoot, "Logs", "Red Final QA");
            try
            {
                camera.transform.position = new Vector3(0, 0, -100);
                camera.orthographic = true;
                camera.orthographicSize = 180f;
                camera.aspect = (float)width / height;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                camera.allowHDR = false;
                camera.allowMSAA = false;
                foreach (var item in layers) item.gameObject.layer = 31;
                foreach (var state in canvasStates)
                {
                    if (state.scaler) state.scaler.enabled = false;
                    state.canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    state.canvas.worldCamera = camera;
                    state.canvas.planeDistance = 1f;
                    state.canvas.scaleFactor = height / 360f;
                }
                Canvas.ForceUpdateCanvases();
                if (cutscene) typeof(RedFinalCutscene).GetMethod("FitViewport", PrivateInstance).Invoke(cutscene, null);
                Canvas.ForceUpdateCanvases();
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                var pixels = image.GetPixels32();
                var sampledColors = new HashSet<Color32>();
                for (int i = 0; i < pixels.Length; i += 31) sampledColors.Add(pixels[i]);
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
                if (cutscene) Assert.Greater(sampledColors.Count, 32, "the QA camera did not render the runtime artwork");
                else AssertHospitalArtwork(ending, camera, image);
            }
            finally
            {
                foreach (var state in canvasStates)
                {
                    state.canvas.renderMode = state.mode;
                    state.canvas.worldCamera = state.camera;
                    state.canvas.planeDistance = state.distance;
                    if (state.scaler) state.scaler.enabled = state.scalerEnabled;
                    state.canvas.scaleFactor = state.scale;
                }
                foreach (var item in layers) if (item.gameObject) item.gameObject.layer = item.layer;
                Canvas.ForceUpdateCanvases();
                if (cutscene) typeof(RedFinalCutscene).GetMethod("FitViewport", PrivateInstance).Invoke(cutscene, null);
                RenderTexture.active = previousActive;
                camera.targetTexture = null;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        private static void AssertHospitalArtwork(EndingSequence ending, Camera camera, Texture2D capture)
        {
            // The hospital's native frame has only 29 colours. Check visible artwork landmarks rather than
            // requiring a larger palette, which would also allow a blank room with colourful dialogue to pass.
            var room = Field<Image>(ending, "hospitalRoom");
            Assert.IsTrue(room.gameObject.activeInHierarchy, "the hospital room is hidden in the QA capture");
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(room.sprite.texture))));
                var rect = room.sprite.rect;
                var corners = new Vector3[4];
                room.rectTransform.GetWorldCorners(corners);
                // Window, monitor and wall are above the bottom dialogue panel and away from the actors.
                foreach (var point in new[] { new Vector2Int(20, 70), new Vector2Int(152, 73), new Vector2Int(50, 50) })
                {
                    Color32 expected = source.GetPixel((int)rect.x + point.x, (int)rect.y + point.y);
                    Vector3 world = Vector3.Lerp(corners[0], corners[3], (point.x + 0.5f) / rect.width) +
                                    (corners[1] - corners[0]) * ((point.y + 0.5f) / rect.height);
                    Vector3 viewport = camera.WorldToViewportPoint(world);
                    Color32 actual = capture.GetPixel(Mathf.Clamp((int)(viewport.x * capture.width), 0, capture.width - 1),
                        Mathf.Clamp((int)(viewport.y * capture.height), 0, capture.height - 1));
                    Assert.That(actual.r, Is.EqualTo(expected.r).Within(2), $"hospital artwork at {point}: red");
                    Assert.That(actual.g, Is.EqualTo(expected.g).Within(2), $"hospital artwork at {point}: green");
                    Assert.That(actual.b, Is.EqualTo(expected.b).Within(2), $"hospital artwork at {point}: blue");
                }
            }
            finally { Object.DestroyImmediate(source); }
        }

        private static string BackupSaveDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), "biformis_red_final_backup_" + Guid.NewGuid().ToString("N"));
            CopyFiles(Application.persistentDataPath, directory);
            return directory;
        }

        private static void RestoreSaveDirectory(string backup)
        {
            //Only remove files within Unity's exact save directory; the backup has its own unique temp directory.
            if (Directory.Exists(Application.persistentDataPath))
                foreach (string file in Directory.GetFiles(Application.persistentDataPath, "*", SearchOption.AllDirectories)) File.Delete(file);
            CopyFiles(backup, Application.persistentDataPath);
            Directory.Delete(backup, true);
        }

        private static void CopyFiles(string source, string target)
        {
            Directory.CreateDirectory(target);
            if (!Directory.Exists(source)) return;
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }
    }
}
#endif
