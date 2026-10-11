#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.DataPersistence;
using CaptainPinkTurd.Game;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.InkDialogue;
using CaptainPinkTurd.Scene;
using CaptainPinkTurd.Scene.Manager;
using CaptainPinkTurd.Scene.Story;
using CaptainPinkTurd.Story.Presentation;
using CaptainPinkTurd.TopDownControllerSystem;
using CaptainPinkTurd.UnitSystem;
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
    /// Plays the embedded encounter in the real Level 6 scene, including the dialogue and native cast timing.
    /// Captures its key frames for visual QA and restores the player's save after every test.
    /// </summary>
    public class BossEntranceTests
    {
        private const string StoryDataPath = "Assets/Game Data/Story/Story Data.asset";
        private readonly List<string> exceptions = new();
        private readonly List<GameObject> testObjects = new();
        private string saveBackup;
        private string saveSource, testSavedFilePath;

        private static StoryData Data => AssetDatabase.LoadAssetAtPath<StoryData>(StoryDataPath);

        [SetUp]
        public void SetUp()
        {
            exceptions.Clear();
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
            saveSource = Path.GetFullPath(Application.persistentDataPath);
            testSavedFilePath = null;
            saveBackup = Path.Combine(Path.GetTempPath(), "biformis_entrance_save_" + Guid.NewGuid().ToString("N"));
            CopyDirectory(saveSource, saveBackup);
            SessionState.EraseString(StoryMenu.EDITOR_PLAY_SCENE_KEY);
            StoryFlow.Stop();
            HitStop.Abort();
            Time.timeScale = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            var entrance = Object.FindAnyObjectByType<BossEntranceSequence>();
            if (entrance && entrance.IsPlaying) entrance.Skip();
            foreach (var obj in testObjects) if (obj) Object.DestroyImmediate(obj);
            testObjects.Clear();
            StoryFlow.Stop();
            HitStop.Abort();
            Time.timeScale = 1f;
            SessionState.EraseString(StoryMenu.EDITOR_PLAY_SCENE_KEY);
            if (Directory.Exists(saveBackup))
            {
                Assert.AreEqual(saveSource, Path.GetFullPath(Application.persistentDataPath), "the application's save root changed during the test");
                Assert.IsTrue(IsChildPath(saveBackup, Path.GetTempPath()), "backup cleanup escaped the temporary directory");
                // Restore original files and remove only the game's known test-created save, if it did not
                // exist before this test. Unrelated files in the application's folder are left alone.
                if (testSavedFilePath != null)
                {
                    Assert.IsTrue(IsChildPath(testSavedFilePath, saveSource), "test save escaped the application's save root");
                    string backedUpFile = Path.Combine(saveBackup, Path.GetRelativePath(saveSource, testSavedFilePath));
                    if (!File.Exists(backedUpFile) && File.Exists(testSavedFilePath)) File.Delete(testSavedFilePath);
                }
                CopyDirectory(saveBackup, saveSource);
                Directory.Delete(saveBackup, true);
                // OnApplicationQuit writes the manager's in-memory data; keep it aligned with the restored save.
                if (DataPersistenceManager.HasInstance) DataPersistenceManager.Instance.LoadGame();
            }
            Application.logMessageReceived -= OnLog;
            Assert.IsEmpty(exceptions, string.Join("\n\n", exceptions));
        }

        [TestCase("Assets/Ink Dialogue/Biformis_Story.json")]
        [TestCase("Assets/Ink Dialogue/Biformis_Story_EN.json")]
        public void BeforeBossHasSixAlternatingDialogueLines(string inkPath)
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(inkPath);
            Assert.IsNotNull(json, inkPath);
            var story = new Ink.Runtime.Story(json.text);
            story.ChoosePathString("BeforeBoss");
            var speakers = new List<string>();
            while (story.canContinue)
            {
                string line = story.Continue().Trim();
                if (line.Length == 0) continue;
                Assert.IsNotEmpty(line);
                speakers.Add(story.currentTags.Single(tag => tag.StartsWith("speaker:"))["speaker:".Length..]);
                Assert.IsFalse(story.currentTags.Any(tag => tag.StartsWith("cast:") || tag.StartsWith("move:")),
                    "the embedded entrance owns its stage; its dialogue must not move the standalone cutscene cast");
            }
            CollectionAssert.AreEqual(new[] { "Villain", "AB", "Villain", "AB", "Villain", "AB" }, speakers);
            Assert.IsEmpty(story.currentChoices);
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator LevelSixHasPixelArtAndCompleteNativeAnimationTimings()
        {
            // Direct gameplay test loads bypass the story presentation, so existing boss tests remain usable.
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 6");
            var entrance = Object.FindAnyObjectByType<BossEntranceSequence>();
            Assert.IsNotNull(entrance, "Level 6 has no embedded entrance");
            yield return null;
            Assert.IsFalse(entrance.IsPlaying, "opening Level 6 outside the story should start combat directly");
            Assert.IsNull(entrance.transform.Find("Boss Entrance Canvas"));

            var settings = new SerializedObject(entrance);
            var pipe = settings.FindProperty("pipeBackground").objectReferenceValue as Sprite;
            Assert.IsNotNull(pipe, "the side-view pipe background is missing");
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(pipe));
            Assert.AreEqual(FilterMode.Point, importer.filterMode, "pipe pixels must remain sharp");
            Assert.IsFalse(importer.mipmapEnabled, "pipe art must not blur through mipmaps");
            Assert.Greater(pipe.rect.width, pipe.rect.height, "the pipe must be a horizontal interior");
            Assert.IsNotNull(settings.FindProperty("dialoguePrefab").objectReferenceValue);
            Assert.IsNotNull(settings.FindProperty("revealShader").objectReferenceValue);

            foreach (string clip in new[] { "redRun", "blueRun", "redIdle", "blueIdle", "bossIdle", "bossCast" })
            {
                var frames = Frames(settings, clip);
                var durations = settings.FindProperty(clip).FindPropertyRelative("durations");
                Assert.IsNotEmpty(frames, clip + " has no frames");
                Assert.AreEqual(frames.Length, durations.arraySize, clip + " has missing frame timings");
                for (int i = 0; i < frames.Length; i++)
                {
                    Assert.IsNotNull(frames[i], $"{clip} frame {i} is missing");
                    Assert.Greater(durations.GetArrayElementAtIndex(i).floatValue, 0f, $"{clip} frame {i} has no duration");
                }
            }
            Assert.IsFalse(Frames(settings, "redRun").Intersect(Frames(settings, "blueRun")).Any(),
                "the merged runner must alternate the red and blue characters' separate art");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Boss/Plague Doctor Boss.prefab");
            var native = new SerializedObject(prefab.GetComponent<PlagueDoctorBoss>());
            AssertNativeBossClip(settings.FindProperty("bossIdle"), native.FindProperty("idle"));
            AssertNativeBossClip(settings.FindProperty("bossCast"), native.FindProperty("cast"));
            Assert.IsFalse(settings.FindProperty("bossCast").FindPropertyRelative("loop").boolValue,
                "the summoning gesture must play once before the arena is revealed");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LevelFiveLeadsThroughMergedRunDialogueCastAndArenaWithoutAnotherSceneLoad()
        {
            yield return LoadMenu();
            var data = Data;
            int level5 = SceneIndex("Level Story 5");
            Assert.AreEqual("Level Story 6", data.Steps[level5 + 1].sceneName,
                "the entrance should live inside Level 6, directly after Level 5");
            StoryFlow.StartAt(data, level5);
            yield return StoryTestLoading.WaitFor(() => SceneManager.GetActiveScene().name == "Level Story 5" && !SceneController.Instance.IsBusy,
                40f, "Level 5");
            Object.FindAnyObjectByType<LevelManager>().NextLevel();
            yield return WaitForRunningEntrance();

            var entrance = Object.FindAnyObjectByType<BossEntranceSequence>();
            var boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
            var player = Object.FindAnyObjectByType<PlayerUnit>();
            var health = player.GetComponent<UnitHealth>();
            var merged = entrance.transform.Find("Boss Entrance Canvas/Merged A&B").GetComponent<Image>();
            var maskedBoss = entrance.transform.Find("Boss Entrance Canvas/Masked Boss").GetComponent<Image>();
            var settings = new SerializedObject(entrance);
            var redFrames = new HashSet<Sprite>(Frames(settings, "redRun"));
            var blueFrames = new HashSet<Sprite>(Frames(settings, "blueRun"));
            var sceneHandle = SceneManager.GetActiveScene().handle;
            int sceneLoads = 0, sceneUnloads = 0;
            void Loaded(UnityEngine.SceneManagement.Scene _, LoadSceneMode __) => sceneLoads++;
            void Unloaded(UnityEngine.SceneManagement.Scene _) => sceneUnloads++;
            SceneManager.sceneLoaded += Loaded;
            SceneManager.sceneUnloaded += Unloaded;
            try
            {
                Assert.AreEqual(level5 + 1, data.CurrentStepIndex);
                AssertIntroGates(entrance, boss, player);
                AssertLoadingOverlayClear();
                float startX = merged.rectTransform.anchoredPosition.x;
                bool sawRed = false, sawBlue = false;
                while (entrance.CurrentStage == BossEntranceSequence.EStage.Running)
                {
                    sawRed |= redFrames.Contains(merged.sprite);
                    sawBlue |= blueFrames.Contains(merged.sprite);
                    Assert.IsTrue(redFrames.Contains(merged.sprite) || blueFrames.Contains(merged.sprite), "the running figure uses foreign art");
                    Assert.AreEqual(1, entrance.GetComponentsInChildren<Image>().Count(image => image.name == "Merged A&B"),
                        "A&B are one overlapping figure");
                    var canvasRect = (RectTransform)merged.canvas.rootCanvas.transform;
                    bool fullyOnScreen = merged.rectTransform.anchoredPosition.x > -canvasRect.rect.width * 0.5f + merged.rectTransform.rect.width;
                    if (sawRed && sawBlue && fullyOnScreen) break;
                    yield return null;
                }
                Assert.IsTrue(sawRed && sawBlue, "the runner did not flicker between the red and blue sprites");
                Assert.Greater(merged.rectTransform.anchoredPosition.x, startX, "the merged runner did not run from left to right");
                Capture("01-running");
                yield return StoryTestLoading.WaitFor(() => entrance.CurrentStage == BossEntranceSequence.EStage.BossAppearing, 10f, "masked boss appearance");
                float initialBossAlpha = maskedBoss.color.a;
                yield return StoryTestLoading.WaitFor(() => entrance.CurrentStage == BossEntranceSequence.EStage.Dialogue, 10f, "entrance dialogue");
                Assert.Greater(maskedBoss.color.a, initialBossAlpha, "the masked boss did not fade into the pipe");
                Assert.IsTrue(DialogueManager.Instance.DialogueIsPlaying);
                AssertIntroGates(entrance, boss, player);
                Capture("02-dialogue");
                Capture("02-dialogue-20x9", 1600, 720);

                // Even a synthetic contact cannot hurt either actor while the real colliders and AI are gated.
                Vector2 playerPosition = player.rb.position;
                int playerHp = health.CurrentHealth, bossHp = boss.CurrentHealth;
                player.rb.position = boss.transform.position;
                Physics2D.SyncTransforms();
                boss.TakeDamage(new SDamageData(1, player.gameObject));
                yield return new WaitForSecondsRealtime(0.3f);
                Assert.AreEqual(playerHp, health.CurrentHealth, "player took damage during the dialogue");
                Assert.AreEqual(bossHp, boss.CurrentHealth, "disabled boss accepted damage before combat");
                player.rb.position = playerPosition;

                yield return FinishEntranceDialogue(entrance);
                Assert.AreEqual(BossEntranceSequence.EStage.Casting, entrance.CurrentStage);
                float castStart = Time.realtimeSinceStartup;
                var castFrames = new HashSet<Sprite>(Frames(settings, "bossCast"));
                Assert.Contains(maskedBoss.sprite, castFrames.ToArray(), "summoning does not use the boss's phase-one cast");
                AssertIntroGates(entrance, boss, player);
                Capture("03-cast");
                yield return StoryTestLoading.WaitFor(() => entrance.CurrentStage == BossEntranceSequence.EStage.Revealing, 10f, "arena reveal");
                float nativeCastSeconds = ClipSeconds(settings.FindProperty("bossCast"));
                Assert.GreaterOrEqual(Time.realtimeSinceStartup - castStart, nativeCastSeconds - 0.1f,
                    "the reveal cut off the native hand gesture");
                yield return StoryTestLoading.WaitFor(() => entrance.RevealProgress > 0.25f, 10f, "arena to expand from the boss");
                Assert.Less(entrance.RevealProgress, 1f, "the arena popped in instead of spreading across the screen");
                float previousProgress = entrance.RevealProgress;
                Capture("04-arena-spreading");
                while (!entrance.Finished)
                {
                    Assert.GreaterOrEqual(entrance.RevealProgress, previousProgress, "the summon reveal moved backwards");
                    previousProgress = entrance.RevealProgress;
                    Assert.IsFalse(boss.enabled, "boss AI started before the reveal finished");
                    AssertLoadingOverlayClear();
                    yield return null;
                }
                AssertCombatRestored(entrance, boss, player);
                Assert.AreEqual(playerHp, health.CurrentHealth);
                Assert.AreEqual(sceneHandle, SceneManager.GetActiveScene().handle, "the arena was loaded as another scene after the dialogue");
                Assert.AreEqual(0, sceneLoads, "a scene was loaded during the embedded entrance");
                Assert.AreEqual(0, sceneUnloads, "a scene was unloaded during the embedded entrance");
                Assert.AreEqual(level5 + 1, data.CurrentStepIndex, "the entrance incorrectly advanced the story");
                yield return null;
                Capture("05-combat-arena");
                var thumb = TouchHud.PushStick(Vector2.right);
                Vector2 beforeMove = player.rb.position;
                yield return new WaitForSeconds(0.35f);
                TouchHud.LetGoOfStick(thumb);
                Assert.Greater(player.rb.position.x, beforeMove.x, "the touch joystick did not regain player control");
            }
            finally
            {
                SceneManager.sceneLoaded -= Loaded;
                SceneManager.sceneUnloaded -= Unloaded;
            }
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator SkipAndDisableRestoreControlsAndOriginalCanvasStates([Values(false, true)] bool disable)
        {
            yield return LoadMenu();
            var enabledCanvas = QaCanvas("Originally enabled entrance test UI", true, false);
            var disabledCanvas = QaCanvas("Originally disabled entrance test UI", false, true);
            StoryFlow.StartAt(Data, SceneIndex("Level Story 6"));
            yield return WaitForRunningEntrance();
            var entrance = Object.FindAnyObjectByType<BossEntranceSequence>();
            var boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
            var player = Object.FindAnyObjectByType<PlayerUnit>();
            yield return StoryTestLoading.WaitFor(() => entrance.CurrentStage == BossEntranceSequence.EStage.Dialogue, 15f, "dialogue before interruption");
            Assert.IsFalse(enabledCanvas.enabled);
            Assert.IsFalse(disabledCanvas.enabled);
            AssertIntroGates(entrance, boss, player);
            if (disable) entrance.enabled = false;
            else entrance.Skip();
            yield return null;
            Assert.IsFalse(entrance.IsPlaying, "interruption left the entrance live");
            Assert.IsFalse(DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsPlaying, "interruption left dialogue consuming interaction input");
            AssertCombatRestored(entrance, boss, player);
            Assert.IsTrue(enabledCanvas.enabled, "previously visible canvas was not restored");
            Assert.IsFalse(enabledCanvas.GetComponent<GraphicRaycaster>().enabled, "a raycaster disabled before the intro was enabled");
            Assert.IsFalse(disabledCanvas.enabled, "a canvas hidden before the intro became visible");
            Assert.IsTrue(disabledCanvas.GetComponent<GraphicRaycaster>().enabled, "untouched canvas raycaster was changed");
            Assert.IsNull(entrance.transform.Find("Boss Entrance Canvas"), "interruption left the pipe overlay blocking combat");
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator ReloadingTheBossArenaSkipsTheEntrance()
        {
            yield return LoadMenu();
            StoryFlow.StartAt(Data, SceneIndex("Level Story 6"));
            yield return WaitForRunningEntrance();
            var entrance = Object.FindAnyObjectByType<BossEntranceSequence>();
            entrance.Skip();
            yield return StoryTestLoading.WaitFor(() => !SceneController.Instance.IsBusy, 10f, "boss transition completion");
            int previousHandle = SceneManager.GetActiveScene().handle;
            StoryFlow.ReloadCurrent(Data);
            yield return StoryTestLoading.WaitFor(() => SceneManager.GetActiveScene().name == "Level Story 6"
                    && SceneManager.GetActiveScene().handle != previousHandle && !SceneController.Instance.IsBusy,
                40f, "boss retry");
            entrance = Object.FindAnyObjectByType<BossEntranceSequence>();
            yield return null;
            Assert.IsTrue(StoryFlow.IsStoryRunning);
            Assert.IsTrue(StoryFlow.IsReloadingCurrentStep);
            Assert.IsFalse(entrance.IsPlaying, "retry replayed the entrance");
            Assert.IsNull(entrance.transform.Find("Boss Entrance Canvas"), "retry spawned a pipe scene");
            Assert.IsFalse(DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsPlaying);
            var boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
            Assert.AreEqual(15, boss.CurrentHealth, "retry did not create a fresh boss");
            AssertCombatRestored(entrance, boss, Object.FindAnyObjectByType<PlayerUnit>());
        }

        private IEnumerator LoadMenu()
        {
            SceneManager.LoadScene("Core");
            yield return StoryTestLoading.WaitFor(() => SceneManager.GetSceneByName("MainMenu").isLoaded && !SceneController.Instance.IsBusy, 40f, "main menu");
            string fileName = new SerializedObject(DataPersistenceManager.Instance).FindProperty("fileName").stringValue;
            testSavedFilePath = Path.GetFullPath(Path.Combine(saveSource, "Default", fileName));
            Assert.IsTrue(IsChildPath(testSavedFilePath, saveSource));
            TouchHud.Ensure();
            yield return null;
        }

        private static IEnumerator WaitForRunningEntrance()
        {
            yield return StoryTestLoading.WaitFor(() => SceneManager.GetActiveScene().name == "Level Story 6", 40f, "boss scene");
            yield return StoryTestLoading.WaitFor(() => Object.FindAnyObjectByType<BossEntranceSequence>()?.CurrentStage == BossEntranceSequence.EStage.Running,
                10f, "merged runner");
        }

        private static IEnumerator FinishEntranceDialogue(BossEntranceSequence entrance)
        {
            float giveUp = Time.realtimeSinceStartup + 10f;
            while (entrance.CurrentStage == BossEntranceSequence.EStage.Dialogue)
            {
                Assert.Less(Time.realtimeSinceStartup, giveUp, "entrance dialogue never yielded to the summoning gesture");
                DialogueManager.Instance.RequestContinue();
                yield return null;
                yield return null;
            }
        }

        private static void AssertIntroGates(BossEntranceSequence entrance, PlagueDoctorBoss boss, PlayerUnit player)
        {
            Assert.IsTrue(entrance.IsPlaying);
            Assert.IsFalse(boss.enabled, "combat boss is running under the pipe scene");
            Assert.IsFalse(boss.GetComponent<BossRoaming>().enabled, "roaming is running under the pipe scene");
            Assert.IsFalse(player.GetComponent<PlayerFreeMovementTopDownController2D>().enabled, "movement input is active during the entrance");
            Assert.IsTrue((player.rb.constraints & RigidbodyConstraints2D.FreezePosition) == RigidbodyConstraints2D.FreezePosition);
            Assert.IsTrue(player.GetComponentsInChildren<Collider2D>(true).All(collider => !collider.enabled), "player can collide during the entrance");
            Assert.IsTrue(boss.GetComponentsInChildren<Collider2D>(true).All(collider => !collider.enabled), "boss can collide during the entrance");
            Assert.AreEqual(0, boss.GetComponent<BossHazards>().ActiveProjectileCount, "boss emitted a projectile during the entrance");
            var hud = TouchHud.Ensure();
            Assert.IsTrue(hud.gameObject.activeInHierarchy, "hiding HUD must preserve its virtual gamepad objects");
            Assert.IsTrue(hud.GetComponentsInChildren<Canvas>(true).All(canvas => !canvas.enabled), "gameplay HUD is visible during the pipe scene");
            Assert.AreEqual(1f, Time.timeScale, "the entrance changed the global gameplay clock");
        }

        private static void AssertCombatRestored(BossEntranceSequence entrance, PlagueDoctorBoss boss, PlayerUnit player)
        {
            Assert.IsFalse(entrance.IsPlaying);
            Assert.IsTrue(boss.enabled);
            Assert.IsTrue(boss.GetComponent<BossRoaming>().enabled);
            Assert.IsTrue(boss.GetComponent<BoxCollider2D>().enabled);
            Assert.IsTrue(boss.GetComponent<CircleCollider2D>().enabled);
            Assert.IsTrue(player.GetComponent<PlayerFreeMovementTopDownController2D>().enabled);
            Assert.AreEqual(RigidbodyConstraints2D.None, player.rb.constraints & RigidbodyConstraints2D.FreezePosition, "player stayed frozen after the intro");
            Assert.IsTrue(player.GetComponentsInChildren<Collider2D>(true).Any(collider => collider.enabled && collider.gameObject.activeInHierarchy));
            Assert.IsTrue(player.GetComponentsInChildren<Renderer>(true).All(renderer => !renderer.forceRenderingOff));
            Assert.IsFalse(boss.GetComponent<Renderer>().forceRenderingOff);
            Assert.IsTrue(TouchHud.Ensure().GetComponentsInChildren<Canvas>(true).Any(canvas => canvas.enabled), "HUD did not return for combat");
            Assert.AreEqual(1f, Time.timeScale);
        }

        private static void AssertLoadingOverlayClear()
        {
            foreach (var overlay in Object.FindObjectsByType<LoadingOverlay>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var group = new SerializedObject(overlay).FindProperty("canvasGroup").objectReferenceValue as CanvasGroup;
                if (group) Assert.AreEqual(0f, group.alpha, 0.001f, "loading overlay appeared during the embedded encounter");
            }
        }

        private Canvas QaCanvas(string name, bool visible, bool raycasts)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            Object.DontDestroyOnLoad(obj);
            testObjects.Add(obj);
            var canvas = obj.GetComponent<Canvas>();
            canvas.enabled = visible;
            obj.GetComponent<GraphicRaycaster>().enabled = raycasts;
            return canvas;
        }

        private static int SceneIndex(string scene)
        {
            for (int i = 0; i < Data.Steps.Count; i++) if (Data.Steps[i].sceneName == scene) return i;
            Assert.Fail("story has no step " + scene);
            return -1;
        }

        private static Sprite[] Frames(SerializedObject owner, string clip)
        {
            var frames = owner.FindProperty(clip).FindPropertyRelative("frames");
            return Enumerable.Range(0, frames.arraySize).Select(i => frames.GetArrayElementAtIndex(i).objectReferenceValue as Sprite).ToArray();
        }

        private static float ClipSeconds(SerializedProperty clip)
        {
            var durations = clip.FindPropertyRelative("durations");
            return Enumerable.Range(0, durations.arraySize).Sum(i => durations.GetArrayElementAtIndex(i).floatValue);
        }

        private static void AssertNativeBossClip(SerializedProperty stageClip, SerializedProperty nativeClip)
        {
            var actual = stageClip.FindPropertyRelative("frames");
            var expected = nativeClip.FindPropertyRelative("frames");
            var durations = stageClip.FindPropertyRelative("durations");
            var milliseconds = nativeClip.FindPropertyRelative("frameMilliseconds");
            Assert.AreEqual(expected.arraySize, actual.arraySize, stageClip.name + " has a different native frame count");
            for (int i = 0; i < expected.arraySize; i++)
            {
                Assert.AreEqual(expected.GetArrayElementAtIndex(i).objectReferenceValue, actual.GetArrayElementAtIndex(i).objectReferenceValue,
                    $"{stageClip.name} frame {i} is not the phase-one asset");
                Assert.AreEqual(milliseconds.GetArrayElementAtIndex(i).intValue / 1000f, durations.GetArrayElementAtIndex(i).floatValue, 0.00001f,
                    $"{stageClip.name} frame {i} changed the authored timing");
            }
        }

        private static void Capture(string name, int width = 1600, int height = 900)
        {
            // Temporarily route the actual overlay canvases through the real arena camera; batch mode's
            // ScreenCapture cannot reliably capture overlay UI. Restore every rendering setting immediately.
            var camera = Camera.main;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.enabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .Select(canvas => (canvas, canvas.worldCamera, canvas.planeDistance)).ToArray();
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            target.filterMode = FilterMode.Point;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            float previousAspect = camera.aspect;
            var entrance = Object.FindAnyObjectByType<BossEntranceSequence>();
            var stage = entrance ? entrance.transform.Find("Boss Entrance Canvas") as RectTransform : null;
            var pipe = stage ? stage.Find("Pipe Background").GetComponent<RawImage>() : null;
            var bossRect = stage ? stage.Find("Masked Boss") as RectTransform : null;
            Rect previousPipeUv = pipe ? pipe.uvRect : default;
            var material = pipe ? pipe.material : null;
            float previousRevealAspect = material ? material.GetFloat("_ScreenAspect") : 0f;
            Vector4 previousRevealCentre = material ? material.GetVector("_RevealCentre") : default;
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                foreach (var (canvas, _, _) in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
                // Overlay UI normally uses the Game View aspect. Match the temporary camera target here
                // without changing the live sequence's clocks or its accumulated reveal radius.
                if (pipe && stage && bossRect)
                {
                    float targetAspect = stage.rect.width / stage.rect.height;
                    float sourceAspect = (float)pipe.texture.width / pipe.texture.height;
                    float horizontalWindow = targetAspect / sourceAspect;
                    pipe.uvRect = new Rect((1f - horizontalWindow) * 0.5f, 0f, horizontalWindow, 1f);
                    Vector2 centre = bossRect.anchoredPosition + (Vector2.one * 0.5f - bossRect.pivot) * bossRect.sizeDelta;
                    material.SetFloat("_ScreenAspect", targetAspect);
                    material.SetVector("_RevealCentre", new Vector4(centre.x / stage.rect.width + 0.5f,
                        centre.y / stage.rect.height + 0.5f, 0f, 0f));
                }
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
                RenderTexture.active = target;
                picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                picture.Apply();
                string directory = Path.GetFullPath("Logs/Boss Entrance QA");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), picture.EncodeToPNG());
            }
            finally
            {
                foreach (var (canvas, oldCamera, distance) in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = oldCamera;
                    canvas.planeDistance = distance;
                }
                camera.targetTexture = previousTarget;
                camera.aspect = previousAspect;
                if (pipe) pipe.uvRect = previousPipeUv;
                if (material)
                {
                    material.SetFloat("_ScreenAspect", previousRevealAspect);
                    material.SetVector("_RevealCentre", previousRevealCentre);
                }
                RenderTexture.active = previousActive;
                Canvas.ForceUpdateCanvases();
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(picture);
            }
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Exception) exceptions.Add(message + "\n" + stack);
        }

        private static void CopyDirectory(string source, string target)
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

        private static bool IsChildPath(string path, string parent)
        {
            string parentPrefix = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return Path.GetFullPath(path).StartsWith(parentPrefix, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
