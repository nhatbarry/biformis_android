using System;
using UnityEngine;
using UnityEngine.Audio;

namespace CaptainPinkTurd.AudioSystem
{
    [CreateAssetMenu(fileName = "Story SFX", menuName = "Audio/Story SFX")]
    public sealed class StorySfxCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Cue
        {
            public string id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 0.5f;
        }

        public AudioMixerGroup mixerGroup;
        public Cue[] cues = Array.Empty<Cue>();

        public Cue Find(string id)
        {
            foreach (var cue in cues)
                if (string.Equals(cue.id, id, StringComparison.OrdinalIgnoreCase)) return cue;
            return null;
        }
    }
}
