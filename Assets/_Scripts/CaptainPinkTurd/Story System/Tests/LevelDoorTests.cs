#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.Game;
using CaptainPinkTurd.Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// A level is left through its door with the Interact input, no longer by walking into it: standing at the open
    /// door lights the touch HUD's "!" button up, and pressing it goes on to the next step of the story. Walking up into
    /// the door, as before, does nothing. The story saves its step as it goes: the player's save folder is backed up
    /// and restored.
    /// </summary>
    public class LevelDoorTests
    {
        private string saveBackup;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            saveBackup = Path.Combine(Path.GetTempPath(), "biformis_door_backup_" + Guid.NewGuid().ToString("N"));
            Copy(Application.persistentDataPath, saveBackup);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var f in Directory.GetFiles(Application.persistentDataPath, "*", SearchOption.AllDirectories)) File.Delete(f);
            Copy(saveBackup, Application.persistentDataPath);
            Directory.Delete(saveBackup, true);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheOpenDoorIsTakenWithTheInteractButton()
        {
            //into Level 1 as the story's own step, so the door leads on to Level 2
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<Scene.Story.StoryData>("Assets/Game Data/Story/Story Data.asset");
            int step = 0;
            while (data.Steps[step].sceneName != "Level Story 1") step++;
            SceneManager.LoadScene("Core");
            yield return StoryTestLoading.WaitFor(() => GameObject.Find(StoryMenu.LEVEL_SELECT_BUTTON_NAME) != null, 40f, "main menu");
            TouchHud.Ensure();
            yield return StoryTestLoading.WaitFor(() => !Scene.SceneController.Instance.IsBusy, 10f, "menu transition");
            Object.FindAnyObjectByType<StoryMenu>().StartAtStep(step);
            yield return StoryTestLoading.WaitFor(() => SceneManager.GetActiveScene().name == "Level Story 1", 40f, "Level Story 1");
            yield return new WaitForSecondsRealtime(1f);

            var door = Object.FindAnyObjectByType<Door>();
            var player = Object.FindAnyObjectByType<PlayerUnit>();
            Assert.IsNotNull(door, "Level Story 1 has no door");
            var opening = door.GetComponents<BoxCollider2D>().First(c => c.isTrigger).bounds;

            //at the door while it is still shut: nothing to do there yet
            Teleport(player, opening.center);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsFalse(door.PlayerInReach, "a shut door counts as in reach");
            StringAssert.EndsWith("disabled", TouchHud.Clip("Interact"), "the button is lit at a shut door");

            door.OpenDoor();
            yield return StoryTestLoading.WaitFor(() => door.IsOpen, 10f, "the door to open");
            Teleport(player, opening.center);
            yield return StoryTestLoading.WaitFor(() => door.PlayerInReach, 3f, "the player to be in the door's reach");
            Assert.IsTrue(InteractPrompt.Available, "the HUD isn't told the door is in reach");
            yield return null;
            StringAssert.EndsWith("ready", TouchHud.Clip("Interact"), "the button doesn't light up at the open door");

            //walking up into it doesn't take the player through any more
            var thumb = TouchHud.PushStick(Vector2.up);
            yield return new WaitForSecondsRealtime(1f);
            TouchHud.LetGoOfStick(thumb);
            Teleport(player, opening.center);
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual("Level Story 1", SceneManager.GetActiveScene().name, "walking into the door still takes the player through");

            yield return StoryTestLoading.WaitFor(() => door.PlayerInReach, 3f, "the player back in the door's reach");
            yield return TouchHud.Tap("Interact");
            yield return StoryTestLoading.WaitFor(() => SceneManager.GetActiveScene().name == "Level Story 2", 40f, "the next level after the door");
            Assert.IsFalse(InteractPrompt.Available, "the button stays lit in the next level");
        }

        private static void Copy(string source, string target)
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

        private static void Teleport(PlayerUnit player, Vector2 position)
        {
            var body = player.GetComponent<Rigidbody2D>();
            if (body)
            {
                body.position = position;
                body.linearVelocity = Vector2.zero;
            }
            player.transform.position = position;
            Physics2D.SyncTransforms();
        }
    }
}
#endif
