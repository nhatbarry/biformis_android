#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.InkDialogue;
using Ink.Runtime;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    public class DialogueSfxTests
    {
        private const string Profiles = "Assets/Game Data/Story/Typewriter/";
        private const string DialoguePrefab = "Assets/Prefabs/Story/Story Dialogue System.prefab";
        private GameObject typingObject;
        private bool previousIgnoreFailingMessages;

        [SetUp]
        public void SaveLogPolicy()
        {
            previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
        }

        [TearDown]
        public void CleanUp()
        {
            try
            {
                if (typingObject) Object.DestroyImmediate(typingObject);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }

        [Test]
        public void StoryCharactersHaveResolvedShortVoicesAndNarrationIsQuiet()
        {
            var distinctVoices = new HashSet<AudioClip>();
            foreach (string speaker in new[] { "A", "B", "Mom", "Doctor", "Villain", "Teen" })
            {
                var profile = Profile(speaker);
                Assert.AreEqual(2, profile.typingSoundClips.Length, speaker);
                foreach (var clip in profile.typingSoundClips)
                {
                    Assert.IsNotNull(clip, speaker + " has a broken audio GUID");
                    Assert.IsTrue(AssetDatabase.GetAssetPath(clip).StartsWith("Assets/Audio/Story SFX/Dialogue/"));
                    Assert.That(clip.length, Is.InRange(.03f, .06f), speaker);
                    Assert.AreEqual(1, clip.channels, speaker);
                }
                distinctVoices.Add(profile.typingSoundClips[0]);
            }
            Assert.AreEqual(5, distinctVoices.Count, "Teen uses a higher version of A; other characters have their own tone");
            Assert.IsEmpty(Profile("Narrator").typingSoundClips);
            Assert.IsEmpty(Profile("System").typingSoundClips);
            var writer = AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefab).GetComponentInChildren<TypewriterText>(true);
            Assert.AreSame(Profile("Narrator"), new SerializedObject(writer).FindProperty("defaultAudioInfo").objectReferenceValue,
                "the shared panel's fallback must resolve the quiet narrator instead of the old missing voice");
        }

        [UnityTest]
        public IEnumerator RichTextUsesTheSpokenLetterAndSilenceDoesNotProduceBlips()
        {
            var writer = CreateWriter();
            var source = writer.GetComponent<AudioSource>();
            writer.StartTyping("A", "<color=red>A</color>", TextAlignmentOptions.Left);
            Assert.AreSame(Profile("A").typingSoundClips['A' % 2], source.clip,
                "the voice was chosen from a rich-text bracket instead of the spoken A");
            writer.SkipTyping();
            Assert.IsNull(source.clip);

            writer.StartTyping("B", "<color=red> ...!? — \n</color>", TextAlignmentOptions.Left);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.IsNull(source.clip, "punctuation or spaces made a voice sound");
            writer.SkipTyping();

            writer.StartTyping("TeenB", "B", TextAlignmentOptions.Left);
            Assert.AreSame(Profile("B").typingSoundClips['B' % 2], source.clip,
                "B's younger self lost the B voice");
            writer.SkipTyping();
            writer.StartTyping("AB", "A", TextAlignmentOptions.Left);
            Assert.AreSame(Profile("B").typingSoundClips['A' % 2], source.clip,
                "the joined A&B figure has no voice in the boss conversation");
            writer.SkipTyping();
            writer.StartTyping("Unknown", "A", TextAlignmentOptions.Left);
            Assert.IsNull(source.clip, "an unknown speaker inherited the previous B voice");
        }

        [UnityTest]
        public IEnumerator SkippingAndDisablingCancelTheVoiceAndOnlyCompleteTheLineOnce()
        {
            var writer = CreateWriter();
            var source = writer.GetComponent<AudioSource>();
            int completions = 0;
            writer.StartTyping("B", "Anh về rồi.", TextAlignmentOptions.Left, () => completions++);
            Assert.IsNotNull(source.clip);
            writer.SkipTyping();
            writer.SkipTyping();
            Assert.IsFalse(writer.IsTyping);
            Assert.IsNull(source.clip);
            Assert.AreEqual(1, completions);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.IsNull(source.clip, "a skipped coroutine continued emitting sound");

            writer.StartTyping("Villain", "Đừng bỏ đi.", TextAlignmentOptions.Left, () => completions++);
            Assert.IsNotNull(source.clip);
            typingObject.SetActive(false);
            Assert.IsFalse(writer.IsTyping);
            Assert.IsNull(source.clip);
            yield return new WaitForSecondsRealtime(.1f);
            Assert.AreEqual(1, completions, "disabling completed an old line into the next panel");
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator TheSharedPanelUsesTheSfxMixerMutesCongratsAndResumesInTheHospital()
        {
            LogAssert.ignoreFailingMessages = true; //Core's usual scene-transition cleanup during tests
            SceneManager.LoadScene("Core");
            yield return StoryTestLoading.WaitFor(() => SceneManager.GetSceneByName("MainMenu").isLoaded,
                40f, "the configured Core audio mixer");
            typingObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DialoguePrefab));
            yield return null;
            var manager = typingObject.GetComponentInChildren<DialogueManager>(true);
            var writer = typingObject.GetComponentInChildren<TypewriterText>(true);
            var source = writer.GetComponent<AudioSource>();
            manager.OnDialogueStart.Raise();
            manager.IsTimelineControlled = true;
            manager.OnDisplayDialogue.Raise(Line("A", "Chúc mừng."));
            Assert.IsTrue(writer.IsTyping, "muting changed the visible dialogue");
            Assert.IsNull(source.clip, "the congrats timeline also emitted typing audio");
            Assert.AreSame(AudioManager.Instance.SfxMixerGroup, source.outputAudioMixerGroup);
            writer.SkipTyping();

            manager.IsTimelineControlled = false;
            manager.OnDisplayDialogue.Raise(Line("B", "Ừ. Anh về rồi."));
            Assert.IsNotNull(source.clip, "normal hospital speech stayed muted after the congrats timeline");
            Assert.AreSame(AudioManager.Instance.SfxMixerGroup, source.outputAudioMixerGroup);
            writer.SkipTyping();
        }

        private TypewriterText CreateWriter()
        {
            typingObject = new GameObject("Dialogue audio test", typeof(RectTransform));
            typingObject.SetActive(false);
            var text = typingObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            var writer = typingObject.AddComponent<TypewriterText>();
            SetField(writer, "textUI", text);
            SetField(writer, "defaultAudioInfo", Profile("Narrator"));
            SetField(writer, "audioInfos", new[] { Profile("A"), Profile("B"), Profile("Villain") });
            SetField(writer, "makePredictable", true);
            typingObject.SetActive(true);
            return writer;
        }

        private static DialogueInfo Line(string speaker, string line) => new()
        {
            speaker = speaker, line = line, layout = DialogueManager.DEFAULT_LAYOUT, choices = new List<Choice>()
        };

        private static TypewriterAudioInfo Profile(string speaker) =>
            AssetDatabase.LoadAssetAtPath<TypewriterAudioInfo>(Profiles + speaker + ".asset");

        private static void SetField(TypewriterText writer, string field, object value) =>
            typeof(TypewriterText).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(writer, value);
    }
}
#endif
