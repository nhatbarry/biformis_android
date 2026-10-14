using UnityEngine;

namespace CaptainPinkTurd.AudioSystem
{
    /// <summary>Shared, small one-shot cues for the story stage and boss actions.</summary>
    public static class StorySfx
    {
        private static StorySfxCatalog catalog;
        private static readonly System.Random variants = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() => catalog = null;

        public static StorySfxCatalog Catalog => catalog ? catalog : catalog = Resources.Load<StorySfxCatalog>("Story SFX");

        public static AudioSource CreateSource(GameObject owner, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(owner.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            Route(source);
            return source;
        }

        public static void Route(AudioSource source)
        {
            if (!source) return;
            var manager = AudioManager.TryGetInstance();
            source.outputAudioMixerGroup = manager && manager.SfxMixerGroup
                ? manager.SfxMixerGroup : Catalog ? Catalog.mixerGroup : null;
        }

        public static bool Play(AudioSource source, string id, float gain = 1f)
        {
            if (!source || !source.isActiveAndEnabled) return false;
            var cue = Catalog ? Catalog.Find(id) : null;
            if (cue == null || cue.clips == null || cue.clips.Length == 0) return false;
            // Audio variants must not consume the RNG used for boss patterns and enemy spawns.
            var clip = cue.clips[cue.clips.Length == 1 ? 0 : variants.Next(cue.clips.Length)];
            if (!clip) return false;
            Route(source);
            source.PlayOneShot(clip, cue.volume * Mathf.Clamp01(gain));
            PlayImpact(cue.id, gain);
            return true;
        }

        private static void PlayImpact(string id, float gain)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Run at playback, rather than when an Ink tag is read: delayed head slams
            // and every repeated door hit must buzz at the same time as their sound.
            float amplitude = id.ToLowerInvariant() switch
            {
                "door_bang" => 0.8f,
                "door_break" => 1f,
                "door_slam" => 0.85f,
                "door_knock" => 0.35f,
                "thud" => 0.85f,
                _ => 0f
            };
            amplitude *= Mathf.Clamp01(gain);
            if (amplitude > 0f)
                Lofelt.NiceVibrations.HapticPatterns.PlayEmphasis(amplitude, 0.65f);
#endif
        }
    }
}
