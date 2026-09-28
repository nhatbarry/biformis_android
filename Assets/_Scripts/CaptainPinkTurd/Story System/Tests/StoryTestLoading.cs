using System;
using System.Collections;
using CaptainPinkTurd.Scene;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// Loads a level the way the game does - Core, then the menu, then Session + the level - so the level runs with
    /// the real managers. A level opened on its own gets auto-generated managers that throw on first use.
    /// </summary>
    internal static class StoryTestLoading
    {
        public static IEnumerator LoadLevelThroughCore(string level)
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Core");
            yield return WaitFor(() => SceneManager.GetSceneByName("MainMenu").isLoaded, 30f, "main menu");
            LogAssert.ignoreFailingMessages = true;

            //the menu counts as loaded a frame before the controller finishes that transition, and a busy
            //controller ignores new requests (Perform returns null), so retry until it takes this one
            float giveUp = Time.realtimeSinceStartup + 10f;
            while (SceneController.Instance.NewTransition()
                       .Load(SceneDatabase.Slots.Session, SceneDatabase.Scenes.Session)
                       .Load(SceneDatabase.Slots.SessionContent, level, true)
                       .Unload(SceneDatabase.Slots.Menu)
                       .Perform() == null)
            {
                if (Time.realtimeSinceStartup > giveUp) Assert.Fail("the scene controller stayed busy");
                yield return null;
                LogAssert.ignoreFailingMessages = true;
            }
            yield return WaitFor(() => SceneManager.GetActiveScene().name == level, 30f, level);
            LogAssert.ignoreFailingMessages = true;
        }

        public static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds, string what)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }
    }
}
