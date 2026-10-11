using System.Collections.Generic;
using CaptainPinkTurd.Core.DesignPattern.Singleton;
using UnityEngine;

namespace CaptainPinkTurd.AudioSystem
{
    public class MusicManager : PersistentSingleton<MusicManager>
    {
        [SerializeField] private List<AudioClip> initialPlaylist;
        [SerializeField, Min(0f)] private float fadeSeconds = 1f;

        private sealed class MusicVoice
        {
            public AudioSource source;
            public float startTime, endTime;
            public float fadeFrom, fadeTo, fadeElapsed;
            public bool fading;
        }

        private MusicVoice current;
        private readonly List<MusicVoice> voices = new();
        private bool playlistStopped;
        private readonly Queue<AudioClip> playlist = new();

        private void Start()
        {
            if (initialPlaylist == null) return;
            foreach (var clip in initialPlaylist)
            {
                AddToPlaylist(clip);
            }
        }

        public void AddToPlaylist(AudioClip clip)
        {
            if (!clip) return;
            playlist.Enqueue(clip);
            if (!playlistStopped && (current == null || !current.source.isPlaying))
            {
                PlayNextTrack();
            }
        }

        public void Clear() => playlist.Clear();

        public void PlayNextTrack() 
        {
            if (playlist.TryDequeue(out AudioClip nextTrack)) 
            {
                Play(nextTrack);
            }
        }

        public void Play(AudioClip clip, bool loop = false, float startTime = 0f, float endTime = 0f)
        {
            if (!clip) return;
            playlistStopped = false;
            float trimmedStart = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));
            float trimmedEnd = endTime > 0f
                ? Mathf.Clamp(endTime, trimmedStart + 0.01f, clip.length)
                : clip.length;
            if (current != null && current.source.clip == clip && current.source.isPlaying)
            {
                current.source.loop = loop;
                current.startTime = trimmedStart;
                current.endTime = trimmedEnd;
                KeepLoopInRange(current);
                return;
            }

            current = null;
            foreach (var voice in voices) FadeOut(voice);

            current = GetAvailableVoice();
            var source = current.source;
            source.clip = clip;
            source.outputAudioMixerGroup = AudioManager.Instance.MusicMixerGroup;
            source.loop = loop;
            source.volume = 0f;
            current.startTime = trimmedStart;
            current.endTime = trimmedEnd;
            source.Play();
            if (current.startTime > 0f) source.time = current.startTime;
            BeginFade(current, 1f);
        }

        public void StopCurrentTrack()
        {
            playlistStopped = true;
            current = null;
            foreach (var voice in voices) FadeOut(voice);
        }

        private MusicVoice GetAvailableVoice()
        {
            foreach (var voice in voices)
            {
                if (!voice.source.isPlaying) return voice;
            }

            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.bypassListenerEffects = true;
            var available = new MusicVoice { source = source };
            voices.Add(available);
            return available;
        }

        private void FadeOut(MusicVoice voice)
        {
            if (!voice.source.isPlaying || (voice.fading && voice.fadeTo == 0f)) return;
            BeginFade(voice, 0f);
        }

        private void BeginFade(MusicVoice voice, float target)
        {
            voice.fadeFrom = voice.source.volume;
            voice.fadeTo = target;
            voice.fadeElapsed = 0f;
            voice.fading = fadeSeconds > 0f;
            if (voice.fading) return;
            voice.source.volume = target;
            if (target == 0f) voice.source.Stop();
        }

        private static void KeepLoopInRange(MusicVoice voice)
        {
            if (!voice.source.loop) return;
            float time = voice.source.time;
            if (time < voice.startTime)
                voice.source.time = voice.startTime;
            else if (time >= voice.endTime)
                voice.source.time = voice.startTime + Mathf.Repeat(time - voice.endTime, voice.endTime - voice.startTime);
        }

        private void Update()
        {
            foreach (var voice in voices)
            {
                if (!voice.source.isPlaying) continue;

                // Skip trimmed intros and silent tails on every repeat without restarting at story transitions.
                KeepLoopInRange(voice);

                if (!voice.fading) continue;
                voice.fadeElapsed += Time.unscaledDeltaTime;
                float fraction = fadeSeconds > 0f ? Mathf.Clamp01(voice.fadeElapsed / fadeSeconds) : 1f;
                voice.source.volume = Mathf.Lerp(voice.fadeFrom, voice.fadeTo, fraction.ToLogarithmicFraction());
                if (fraction < 1f) continue;
                voice.fading = false;
                if (voice.fadeTo == 0f) voice.source.Stop();
            }

            if (!playlistStopped && current != null && !current.source.isPlaying && playlist.Count > 0)
                PlayNextTrack();
        }
    }
}
