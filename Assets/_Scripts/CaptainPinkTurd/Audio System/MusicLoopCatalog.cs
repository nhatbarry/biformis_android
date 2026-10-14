using System;
using UnityEngine;

namespace CaptainPinkTurd.AudioSystem
{
    /// <summary>
    /// Musical playback ranges, excluding recorded silence and faded outros.
    /// Shared by level music, boss phases and the story's music tags.
    /// </summary>
    [CreateAssetMenu(fileName = "Music Loop Ranges", menuName = "Audio/Music Loop Ranges")]
    public sealed class MusicLoopCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Range
        {
            public AudioClip clip;
            [Min(0f)] public float startTime;
            [Min(0f)] public float endTime;
        }

        public Range[] ranges = Array.Empty<Range>();

        public void Trim(AudioClip clip, ref float startTime, ref float endTime)
        {
            foreach (var range in ranges)
            {
                if (range.clip != clip) continue;
                float start = Mathf.Max(startTime, range.startTime);
                float end = Mathf.Min(endTime, range.endTime);
                // An explicitly authored section outside this range still takes precedence.
                if (end > start + 0.01f)
                {
                    startTime = start;
                    endTime = end;
                }
                return;
            }
        }
    }
}
