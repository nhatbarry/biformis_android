#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.InkDialogue;
using CaptainPinkTurd.Scene.Manager;
using CaptainPinkTurd.Scene.Story;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// Plays the whole story from the main menu to the ending and back, reading every cutscene line and skipping
    /// each level through its exit. Fails on any exception. The player's save folder is backed up and restored.
    /// </summary>
    public class StoryFlowTests
    {
        private const string StoryDataPath = "Assets/Game Data/Story/Story Data.asset";

        private readonly List<string> exceptions = new();
        private string saveBackup;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true; //the game logs pooled-object warnings and similar noise; only exceptions count here
            Application.logMessageReceived += OnLog;
            saveBackup = BackupDirectory(Application.persistentDataPath);
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            RestoreDirectory(saveBackup, Application.persistentDataPath);
            SessionState.EraseString(StoryMenu.EDITOR_PLAY_SCENE_KEY);
            Time.timeScale = 1f;
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator TheWholeStoryPlaysThroughToTheMenu()
        {
            LogAssert.ignoreFailingMessages = true; //reset per test, so set here as well as in SetUp
            var data = AssetDatabase.LoadAssetAtPath<StoryData>(StoryDataPath);

            SceneManager.LoadScene("Core");
            yield return WaitUntil(() => SceneManager.GetSceneByName("MainMenu").isLoaded, 30f, "main menu");
            yield return new WaitForSecondsRealtime(0.5f);

            Object.FindAnyObjectByType<StoryMenu>().StartNewStory();

            for (int i = 0; i < data.Steps.Count; i++)
            {
                var step = data.Steps[i];
                yield return WaitUntil(() => SceneManager.GetActiveScene().name == step.sceneName, 40f, step.sceneName);
                Assert.AreEqual(i, data.CurrentStepIndex, $"saved step while in {step.sceneName}");

                if (step.type == EStoryStepType.Cutscene)
                {
                    yield return ReadCutscene(step.knotName);
                }
                else if (step.sceneName.StartsWith("Level"))
                {
                    yield return new WaitForSecondsRealtime(1.5f);
                    Assert.IsNotNull(Object.FindAnyObjectByType<PlayerUnit>(), $"{step.sceneName} has no player");
                    Object.FindAnyObjectByType<LevelManager>().NextLevel();
                }
                //the ending scene advances by itself
            }

            yield return WaitUntil(() => SceneManager.GetSceneByName("MainMenu").isLoaded, 90f, "main menu after the ending");
            Assert.IsTrue(data.Completed, "story not marked completed");
            Assert.IsFalse(data.HasProgress, "story progress not cleared");
            Assert.IsEmpty(exceptions, string.Join("\n\n", exceptions));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LevelSelectStartsStraightAtLevel5()
        {
            LogAssert.ignoreFailingMessages = true;
            var data = AssetDatabase.LoadAssetAtPath<StoryData>(StoryDataPath);
            int level5 = IndexOfScene(data, "Level Story 5");

            SceneManager.LoadScene("Core");
            yield return WaitUntil(() => SceneManager.GetSceneByName("MainMenu").isLoaded, 30f, "main menu");
            yield return WaitUntil(() => !Scene.SceneController.Instance.IsBusy, 10f, "menu transition");

            Object.FindAnyObjectByType<StoryMenu>().StartAtStep(level5);
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Level Story 5", 40f, "Level Story 5");
            Assert.AreEqual(level5, data.CurrentStepIndex, "story step not saved at Level 5");

            //and the story carries on from there as usual
            yield return new WaitForSecondsRealtime(1.5f);
            Object.FindAnyObjectByType<LevelManager>().NextLevel();
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == data.Steps[level5 + 1].sceneName, 40f, "the step after Level 5");
            Assert.IsEmpty(exceptions, string.Join("\n\n", exceptions));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator EditorPlayRequestJumpsIntoTheOpenedLevel()
        {
            //the runtime half of "press Play with a level open": the editor hook stores the scene, Core boots, the menu jumps
            LogAssert.ignoreFailingMessages = true;
            SessionState.SetString(StoryMenu.EDITOR_PLAY_SCENE_KEY, "Level Story 3");

            //LoadScene only happens at the end of the frame, and an earlier test may have left Level Story 3 open
            SceneManager.LoadScene("Core");
            yield return WaitUntil(() => SceneManager.GetSceneByName("MainMenu").isLoaded, 30f, "main menu");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Level Story 3", 60f, "Level Story 3");

            Assert.AreEqual("", SessionState.GetString(StoryMenu.EDITOR_PLAY_SCENE_KEY, ""), "the request should be used once");
            Assert.IsNotNull(Object.FindAnyObjectByType<PlayerUnit>(), "Level Story 3 has no player");
            Assert.IsEmpty(exceptions, string.Join("\n\n", exceptions));
        }

        private static int IndexOfScene(StoryData data, string scene)
        {
            for (int i = 0; i < data.Steps.Count; i++)
                if (data.Steps[i].sceneName == scene) return i;
            Assert.Fail($"{scene} is not a story step");
            return -1;
        }

        private static IEnumerator ReadCutscene(string knot)
        {
            yield return WaitUntil(() => DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsPlaying, 10f, $"cutscene {knot} to start");

            int presses = 0;
            while (DialogueManager.Instance.DialogueIsPlaying)
            {
                DialogueManager.Instance.RequestContinue();
                Assert.Less(++presses, 600, $"cutscene {knot} never ended");
                yield return null;
                yield return null;
            }
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception) exceptions.Add(condition + "\n" + stackTrace);
        }

        private static string BackupDirectory(string source)
        {
            string backup = Path.Combine(Path.GetTempPath(), "biformis_save_backup_" + Guid.NewGuid().ToString("N"));
            CopyDirectory(source, backup);
            return backup;
        }

        private static void RestoreDirectory(string backup, string target)
        {
            if (backup == null || !Directory.Exists(backup)) return;
            if (Directory.Exists(target))
            {
                foreach (var file in Directory.GetFiles(target, "*", SearchOption.AllDirectories)) File.Delete(file);
            }
            CopyDirectory(backup, target);
            Directory.Delete(backup, true);
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            if (!Directory.Exists(source)) return;
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }
    }
}
#endif
