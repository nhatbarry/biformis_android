#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.Story.Cutscene;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// Cutscene actors play sprites imported from the team's .aseprite files. Re-importing a file (a new version from
    /// the artist, a changed import mode) can leave a clip pointing at sprites that no longer exist: this fails then.
    /// </summary>
    public class CutsceneArtTests
    {
        //layers that are empty in some frames on purpose (the trapdoor is closed in the first one, the light under the
        //dungeon door has gone out before the blood seeps in)
        private static readonly HashSet<string> MayHaveEmptyFrames = new() { "Trapdoor", "Gap" };

        [UnityTest]
        public IEnumerator EveryCutsceneActorFrameHasItsSprite()
        {
            LogAssert.ignoreFailingMessages = true; //the scene opened on its own has no Core managers
            SceneManager.LoadScene("Story Cutscene");
            yield return null;
            yield return null;

            var animations = Object.FindObjectsByType<StageActorAnimation>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(animations.Length, 5, "no animated cutscene actors found");

            var problems = new List<string>();
            foreach (var animation in animations)
            {
                var clips = new SerializedObject(animation).FindProperty("clips");
                for (int c = 0; c < clips.arraySize; c++)
                {
                    var clip = clips.GetArrayElementAtIndex(c);
                    string clipName = clip.FindPropertyRelative("name").stringValue;
                    var frames = clip.FindPropertyRelative("frames");
                    int empty = 0;
                    for (int f = 0; f < frames.arraySize; f++)
                    {
                        if (frames.GetArrayElementAtIndex(f).objectReferenceValue == null) empty++;
                    }
                    if (empty > 0 && !MayHaveEmptyFrames.Contains(animation.name))
                        problems.Add($"{animation.name}/{clipName}: {empty} of {frames.arraySize} frames have no sprite");
                    if (frames.arraySize == 0) problems.Add($"{animation.name}/{clipName}: no frames");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>
        /// Dialogue portraits are still pictures (the user's call): one frame per speaker, no talking mouth, no glint.
        /// </summary>
        [Test]
        public void EveryDialoguePortraitIsAStillPicture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Story/Story Dialogue System.prefab");
            var style = prefab.GetComponentInChildren<DialogueSpeakerStyle>(true);
            var animation = (StageActorAnimation)new SerializedObject(style).FindProperty("portraitAnimation").objectReferenceValue;
            var clips = new SerializedObject(animation).FindProperty("clips");
            Assert.Greater(clips.arraySize, 0, "no portraits");
            for (int c = 0; c < clips.arraySize; c++)
            {
                var clip = clips.GetArrayElementAtIndex(c);
                string clipName = clip.FindPropertyRelative("name").stringValue;
                var frames = clip.FindPropertyRelative("frames");
                Assert.AreEqual(1, frames.arraySize, $"portrait {clipName} is animated ({frames.arraySize} frames)");
                Assert.IsNotNull(frames.GetArrayElementAtIndex(0).objectReferenceValue, $"portrait {clipName} has no picture");
            }
        }

        /// <summary>
        /// In Level 4 A gets in alone, already hurt by the villain: the player's Blue form shows A_Bloody's frames
        /// (PlayerSpriteSwap in the level), while the prefab and its animations stay Blue Character's.
        /// </summary>
        [UnityTest, Timeout(120000)]
        public IEnumerator Level4ShowsAAlreadyHurt()
        {
            const string bloody = "Assets/Animations/A_Bloody.aseprite";
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 4");
            yield return new WaitForSecondsRealtime(1f);

            var player = Object.FindAnyObjectByType<PlayerUnit>();
            Assert.IsNotNull(player, "Level 4 has no player");
            SpriteRenderer blue = null;
            foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.name == "Blue") blue = renderer;
            }
            Assert.IsNotNull(blue, "the player has no Blue form");
            for (int frame = 0; frame < 30; frame++) //across several animation frames
            {
                Assert.IsNotNull(blue.sprite, "A's renderer shows nothing");
                Assert.AreEqual(bloody, AssetDatabase.GetAssetPath(blue.sprite), $"A shows {blue.sprite.name} of {AssetDatabase.GetAssetPath(blue.sprite)}");
                yield return new WaitForSecondsRealtime(0.05f);
            }
        }
    }
}
#endif
