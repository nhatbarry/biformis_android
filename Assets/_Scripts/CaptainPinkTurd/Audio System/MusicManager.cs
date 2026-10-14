using System.Collections.Generic;
using CaptainPinkTurd.Core.DesignPattern.Singleton;
using UnityEngine;

namespace CaptainPinkTurd.AudioSystem
{
    public class MusicManager : PersistentSingleton<MusicManager>
    {
        [SerializeField] private List<AudioClip> initialPlaylist;
        [SerializeField, Min(0f)] private float fadeSeconds = 1.5f;
        [SerializeField, Min(0f)] private float stopFadeSeconds = 0.75f;

        private sealed class MusicVoice
        {
            public AudioSource source;
            public float startTime, endTime;
            public float fadeFrom, fadeTo, fadeElapsed, fadeDuration;
            public bool fading;
        }

        private MusicVoice current;
        private readonly List<MusicVoice> voices = new();
        private MusicLoopCatalog loopCatalog;
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
            if (loop)
            {
                if (!loopCatalog) loopCatalog = Resources.Load<MusicLoopCatalog>("Music Loop Ranges");
                if (loopCatalog) loopCatalog.Trim(clip, ref trimmedStart, ref trimmedEnd);
            }
            if (current != null && current.source.clip == clip && current.source.isPlaying)
            {
                current.source.loop = loop;
                current.startTime = trimmedStart;
                current.endTime = trimmedEnd;
                KeepLoopInRange(current);
                return;
            }

            // A cue can return while its previous voice is still fading out. Recover that
            // voice so a stop/play pair or an interrupted transition keeps its musical position.
            MusicVoice returning = null;
            foreach (var voice in voices)
            {
                if (voice.source.clip == clip && voice.source.isPlaying)
                {
                    returning = voice;
                    break;
                }
            }
            foreach (var voice in voices)
                if (voice != returning) FadeOut(voice, fadeSeconds);

            current = returning ?? GetAvailableVoice();
            var source = current.source;
            source.outputAudioMixerGroup = AudioManager.Instance.MusicMixerGroup;
            source.loop = loop;
            current.startTime = trimmedStart;
            current.endTime = trimmedEnd;
            if (returning == null)
            {
                source.clip = clip;
                source.volume = 0f;
                source.timeSamples = Mathf.Clamp(Mathf.RoundToInt(trimmedStart * clip.frequency), 0, clip.samples - 1);
                source.Play();
            }
            else KeepLoopInRange(current);
            BeginFade(current, 1f, fadeSeconds);
        }

        public void StopCurrentTrack()
        {
            playlistStopped = true;
            current = null;
            foreach (var voice in voices) FadeOut(voice, stopFadeSeconds);
        }

        public bool IsPlaying(AudioClip clip)
        {
            foreach (var voice in voices)
                if (voice.source.clip == clip && voice.source.isPlaying) return true;
            return false;
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

        private void FadeOut(MusicVoice voice, float duration)
        {
            if (!voice.source.isPlaying) return;
            BeginFade(voice, 0f, duration);
        }

        private void BeginFade(MusicVoice voice, float target, float duration)
        {
            voice.fadeFrom = voice.source.volume;
            voice.fadeTo = target;
            voice.fadeElapsed = 0f;
            voice.fadeDuration = duration;
            voice.fading = duration > 0f;
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
                float fraction = Mathf.Clamp01(voice.fadeElapsed / voice.fadeDuration);
                // Equal-power crossfade: steady combined energy, with gentle starts/ends.
                // Interpolating power also keeps interrupted fades continuous at their current volume.
                float angle = Mathf.SmoothStep(0f, 1f, fraction) * Mathf.PI * 0.5f;
                float blend = Mathf.Sin(angle);
                voice.source.volume = Mathf.Sqrt(Mathf.Lerp(voice.fadeFrom * voice.fadeFrom,
                    voice.fadeTo * voice.fadeTo, blend * blend));
                if (fraction < 1f) continue;
                voice.fading = false;
                if (voice.fadeTo == 0f) voice.source.Stop();
            }

            if (!playlistStopped && current != null && !current.source.isPlaying && playlist.Count > 0)
                PlayNextTrack();
        }
    }
}
