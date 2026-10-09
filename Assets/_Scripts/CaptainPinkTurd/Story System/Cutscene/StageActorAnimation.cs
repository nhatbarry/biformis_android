using System;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// Frame animation for a stage actor drawn in Aseprite: named clips (the file's tags) with their own frame
    /// durations, played on a UI Image. The ink tag "anim:ActorId:clip" switches clips, "anim:ActorId:clip:seconds"
    /// switches after a delay (e.g. sleep a moment, then wake).
    /// Each frame is sized to its pixels and placed by its pivot (the Aseprite canvas centre), so trimmed frames of
    /// different sizes stay where they were drawn and every art pixel is the same size on screen.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class StageActorAnimation : MonoBehaviour
    {
        [Serializable]
        public class Clip
        {
            public string name;
            public Sprite[] frames;
            [Tooltip("Seconds per frame, as set in Aseprite")]
            public float[] durations;
            public bool loop;
            [Tooltip("For a looping clip: how many times it plays before moving on to Next (0 = forever)")]
            public int repeat;
            [Tooltip("Clip played when this one ends")]
            public string next;
        }

        [Tooltip("Canvas units per art pixel. 0 leaves the Image's layout alone (e.g. a portrait that fills its frame).")]
        [SerializeField] private float unitsPerPixel = 5f;
        [Tooltip("0: every frame's pixels are unitsPerPixel. Otherwise a frame imported at another pixels-per-unit is " +
                 "scaled by this / its own, so clips drawn at different resolutions (the defeated boss's breathing and " +
                 "dust sheets) stand at the same size")]
        [SerializeField] private float referencePixelsPerUnit;
        [Tooltip("Played whenever the actor comes on stage")]
        [SerializeField] private string defaultClip;
        [SerializeField] private Clip[] clips;

        private Image image;
        private Clip current;
        private int frame;
        private int loopsDone;
        private float frameTime;
        private string queuedClip;
        private float queuedDelay;

        private void OnEnable()
        {
            //a clip requested while off stage (anim tag before cast tag) is kept
            if (current == null) StartClip(defaultClip);
            else ShowFrame();
        }

        //coming back on stage later starts over from the default clip
        private void OnDisable()
        {
            current = null;
            queuedClip = null;
        }

        public bool HasClip(string clipName) => Find(clipName) != null;
        public string CurrentClip => current?.name;
        public string DefaultClip => defaultClip;

        public void Play(string clipName, float delay = 0f)
        {
            queuedClip = null;
            if (delay > 0f)
            {
                queuedClip = clipName;
                queuedDelay = delay;
                return;
            }
            StartClip(clipName);
        }

        private void StartClip(string clipName)
        {
            var clip = Find(clipName);
            if (clip == null || clip.frames == null || clip.frames.Length == 0)
            {
                Debug.LogWarning($"{name} has no animation clip '{clipName}'");
                return;
            }

            current = clip;
            frame = 0;
            frameTime = 0f;
            loopsDone = 0;
            ShowFrame();
        }

        private void Update()
        {
            if (queuedClip != null)
            {
                queuedDelay -= Time.unscaledDeltaTime;
                if (queuedDelay <= 0f)
                {
                    string clip = queuedClip;
                    queuedClip = null;
                    StartClip(clip);
                    return;
                }
            }

            if (current == null) return;

            frameTime += Time.unscaledDeltaTime;
            while (frameTime >= Duration(frame))
            {
                frameTime -= Duration(frame);
                if (frame + 1 < current.frames.Length)
                {
                    frame++;
                }
                else if (current.loop && (current.repeat <= 0 || ++loopsDone < current.repeat))
                {
                    frame = 0;
                }
                else
                {
                    //hold the last frame, or hand over to the next clip
                    frameTime = 0f;
                    if (!string.IsNullOrEmpty(current.next)) StartClip(current.next);
                    return;
                }
            }
            ShowFrame();
        }

        private float Duration(int index)
        {
            var durations = current.durations;
            return durations != null && index < durations.Length ? Mathf.Max(0.01f, durations[index]) : 0.1f;
        }

        private void ShowFrame()
        {
            if (current == null) return;
            if (!image) image = GetComponent<Image>();

            var sprite = current.frames[frame];
            image.sprite = sprite;
            image.enabled = sprite; //an empty cel (e.g. a closed trapdoor) draws nothing, not a white box
            if (!sprite || unitsPerPixel <= 0f) return;

            var rect = (RectTransform)transform;
            rect.pivot = sprite.pivot / sprite.rect.size;
            float scale = referencePixelsPerUnit > 0f ? referencePixelsPerUnit / sprite.pixelsPerUnit : 1f;
            rect.sizeDelta = sprite.rect.size * (unitsPerPixel * scale);
        }

        private Clip Find(string clipName)
        {
            if (clips == null || clips.Length == 0) return null;
            if (string.IsNullOrEmpty(clipName)) return clips[0];
            foreach (var clip in clips)
            {
                if (clip.name.Equals(clipName, StringComparison.OrdinalIgnoreCase)) return clip;
            }
            return null;
        }
    }
}
