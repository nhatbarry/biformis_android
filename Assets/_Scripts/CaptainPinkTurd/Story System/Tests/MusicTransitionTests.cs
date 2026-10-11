#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CaptainPinkTurd.AudioSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    public class MusicTransitionTests
    {
        private const float TestFadeSeconds = 0.8f;
        private readonly List<AudioClip> clips = new();
        private MusicManager music;
        private AudioManager audio;
        private GameObject listenerObject;
        private bool ownsMusic, ownsAudio;
        private float savedTimeScale, savedFadeSeconds, savedStopFadeSeconds, savedListenerVolume;
        private bool savedListenerPause;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 1f;
            savedListenerVolume = AudioListener.volume;
            savedListenerPause = AudioListener.pause;
            AudioListener.volume = 0f;
            AudioListener.pause = false;
            if (!Object.FindAnyObjectByType<AudioListener>())
                listenerObject = new GameObject("Music Test Listener", typeof(AudioListener));

            audio = AudioManager.TryGetInstance();
            ownsAudio = !audio;
            if (ownsAudio)
            {
                audio = new GameObject("Music Test Audio").AddComponent<AudioManager>();
                // Playback accepts a null mixer group. The volume-settings Start needs the game's mixer.
                audio.enabled = false;
            }

            music = MusicManager.TryGetInstance();
            ownsMusic = !music;
            if (ownsMusic) music = new GameObject("Music Test Manager").AddComponent<MusicManager>();
            var fields = new SerializedObject(music);
            savedFadeSeconds = fields.FindProperty("fadeSeconds").floatValue;
            savedStopFadeSeconds = fields.FindProperty("stopFadeSeconds").floatValue;
            SetFadeSeconds(0f);
            SetStopFadeSeconds(0f);
            music.Clear();
            music.StopCurrentTrack();
            yield return null;
            SetFadeSeconds(TestFadeSeconds);
            SetStopFadeSeconds(TestFadeSeconds);
        }

        [TearDown]
        public void TearDown()
        {
            if (music)
            {
                SetFadeSeconds(0f);
                SetStopFadeSeconds(0f);
                music.Clear();
                music.StopCurrentTrack();
                foreach (var source in music.GetComponents<AudioSource>())
                    if (clips.Contains(source.clip))
                    {
                        source.Stop();
                        source.clip = null;
                    }
                if (ownsMusic) Object.DestroyImmediate(music.gameObject);
                else
                {
                    SetFadeSeconds(savedFadeSeconds);
                    SetStopFadeSeconds(savedStopFadeSeconds);
                }
            }
            if (ownsAudio && audio) Object.DestroyImmediate(audio.gameObject);
            if (listenerObject) Object.DestroyImmediate(listenerObject);
            foreach (var clip in clips) Object.DestroyImmediate(clip);
            clips.Clear();
            AudioListener.volume = savedListenerVolume;
            AudioListener.pause = savedListenerPause;
            Time.timeScale = savedTimeScale;
        }

        [UnityTest, Timeout(10000)]
        public IEnumerator CrossfadeKeepsBothTracksPlayingAndDoesNotDuckAtTheMidpoint()
        {
            var first = CreateClip("Outgoing", 4f);
            var second = CreateClip("Incoming", 4f);
            music.Play(first, true);
            var outgoing = SourceFor(first);
            yield return WaitUntil(() => outgoing.volume >= 0.999f, 3f, "the first track to finish fading in");

            music.Play(second, true);
            var incoming = SourceFor(second);
            Assert.IsTrue(outgoing.isPlaying, "the outgoing track should overlap the incoming track");
            Assert.IsTrue(incoming.isPlaying);
            Assert.AreNotSame(outgoing, incoming);
            bool sampledMidpoint = false;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (outgoing.isPlaying)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "the outgoing track never finishes its fade");
                yield return null;
                if (!outgoing.isPlaying) break;
                Assert.IsTrue(incoming.isPlaying, "the new track stopped during the transition");
                float combinedPower = outgoing.volume * outgoing.volume + incoming.volume * incoming.volume;
                Assert.AreEqual(1f, combinedPower, 0.05f, "the overlap should retain a steady total signal power");
                if (Mathf.Abs(outgoing.volume - incoming.volume) < 0.15f)
                {
                    sampledMidpoint = true;
                    Assert.Greater(outgoing.volume, 0.6f, "the old track should remain audible at the midpoint");
                    Assert.Greater(incoming.volume, 0.6f, "the new track should already be audible at the midpoint");
                }
            }

            Assert.IsTrue(sampledMidpoint, "the test should observe the overlapping midpoint");
            Assert.IsTrue(incoming.isPlaying);
            Assert.AreEqual(1f, incoming.volume, 0.001f);
        }

        [UnityTest, Timeout(10000)]
        public IEnumerator CrossfadeCompletesWhileGameplayIsPaused()
        {
            var first = CreateClip("Paused Outgoing", 4f);
            var second = CreateClip("Paused Incoming", 4f);
            music.Play(first, true);
            var outgoing = SourceFor(first);
            yield return WaitUntil(() => outgoing.volume >= 0.999f, 3f, "the first track to reach full volume");

            Time.timeScale = 0f;
            music.Play(second, true);
            var incoming = SourceFor(second);
            Assert.IsTrue(outgoing.isPlaying);
            Assert.IsTrue(incoming.isPlaying);
            yield return WaitUntil(() => !outgoing.isPlaying && incoming.volume >= 0.999f,
                3f, "the crossfade to finish while paused");
            Assert.AreEqual(0f, Time.timeScale, "a music transition must not resume gameplay");
            Assert.IsTrue(incoming.isPlaying);
        }

        [UnityTest, Timeout(10000)]
        public IEnumerator StopThenTheSameStoryTrackResumesItsExistingVoiceWithoutRestarting()
        {
            var clip = CreateClip("Continuous Story Track", 5f);
            music.Play(clip, true, 0.5f, 4.5f);
            var source = SourceFor(clip);
            yield return WaitUntil(() => source.volume >= 0.999f, 3f, "the story track to reach full volume");
            int sourceCount = music.GetComponents<AudioSource>().Length;

            music.StopCurrentTrack();
            yield return new WaitForSecondsRealtime(TestFadeSeconds * 0.3f);
            Assert.IsTrue(source.isPlaying, "the outgoing voice should still be available during its fade");
            float fadeOutVolume = source.volume;
            float resumeTime = source.time;
            Assert.Less(fadeOutVolume, 0.99f);

            music.Play(clip, true, 0.5f, 4.5f);
            Assert.AreSame(source, SourceFor(clip), "adjacent story steps should reuse the same audio voice");
            Assert.AreEqual(sourceCount, music.GetComponents<AudioSource>().Length,
                "resuming the same track must not create a second copy playing over it");
            Assert.AreEqual(resumeTime, source.time, 0.03f, "the track must continue at its current musical position");
            Assert.AreEqual(fadeOutVolume, source.volume, 0.01f, "reversing a fade must not jump in volume");
            yield return WaitUntil(() => source.volume >= 0.999f, 3f, "the resumed voice to recover its volume");
            Assert.IsTrue(source.isPlaying);

            float continuingTime = source.time;
            music.Play(clip, true, 0.5f, 4.5f);
            Assert.AreSame(source, SourceFor(clip));
            Assert.AreEqual(continuingTime, source.time, 0.03f, "another request for the current track must not restart it");
        }

        [UnityTest, Timeout(10000)]
        public IEnumerator EveryRepeatKeepsTheTrimmedIntroAndTailOutsideThePlaybackRange()
        {
            SetFadeSeconds(0f);
            var clip = CreateClip("Trimmed Story Track", 1.2f);
            const float start = 0.4f, end = 1f;
            music.Play(clip, true, start, end);
            var source = SourceFor(clip);
            Assert.AreEqual(start, source.time, 0.03f, "the first play should skip the intro");

            int wraps = 0;
            float previousTime = source.time;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (wraps < 3)
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, "the shortened section did not loop three times");
                yield return null;
                Assert.IsTrue(source.isPlaying);
                float time = source.time;
                Assert.GreaterOrEqual(time, start - 0.01f, "the intro returned on a later repeat");
                Assert.Less(time, end + 0.04f, "the source continued into the trimmed tail");
                if (time < previousTime - 0.1f) wraps++;
                previousTime = time;
            }
            Assert.AreEqual(1, music.GetComponents<AudioSource>().Count(candidate => candidate.isPlaying && candidate.clip == clip));
        }

        private AudioClip CreateClip(string name, float duration)
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.RoundToInt(duration * sampleRate);
            var clip = AudioClip.Create(name, sampleCount, 1, sampleRate, false);
            var samples = new float[sampleCount];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = 0.02f * Mathf.Sin(2f * Mathf.PI * 220f * i / sampleRate);
            clip.SetData(samples, 0);
            clips.Add(clip);
            return clip;
        }

        private AudioSource SourceFor(AudioClip clip)
        {
            var playing = music.GetComponents<AudioSource>().Where(source => source.clip == clip && source.isPlaying).ToArray();
            Assert.AreEqual(1, playing.Length, $"{clip.name} should have exactly one playing voice");
            return playing[0];
        }

        private void SetFadeSeconds(float seconds)
        {
            var fields = new SerializedObject(music);
            fields.FindProperty("fadeSeconds").floatValue = seconds;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetStopFadeSeconds(float seconds)
        {
            var fields = new SerializedObject(music);
            fields.FindProperty("stopFadeSeconds").floatValue = seconds;
            fields.ApplyModifiedPropertiesWithoutUndo();
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                Assert.Less(Time.realtimeSinceStartup, deadline, $"timed out waiting for {what}");
                yield return null;
            }
        }
    }
}
#endif
