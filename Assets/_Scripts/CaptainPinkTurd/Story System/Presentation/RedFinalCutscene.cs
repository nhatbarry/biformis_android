using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// The authored Red Final montage, between the door of light and the hospital.
    /// Positions and frame durations come directly from the supplied pack, in top-left pixels.
    /// </summary>
    public sealed class RedFinalCutscene : MonoBehaviour
    {
        private const string ResourceRoot = "Story Ending/Red Final/";
        private const float PixelScale = 2f;

        [Serializable] private sealed class TimelineData
        {
            public int totalDurationMs;
            public Shot[] timeline;
            public WorldActor[] world;
        }

        [Serializable] private sealed class Shot
        {
            public int startMs;
            public int endMs;
            public string id;
            public CloseActor[] actors;
        }

        [Serializable] private sealed class WorldActor
        {
            public string id;
            public float x;
            public float y;
            public float worldScale;
            public int canvasWidth;
            public int canvasHeight;
        }

        [Serializable] private sealed class CloseActor
        {
            public string id;
            public int width;
            public int height;
            public float centerX;
            public float centerY;
            public string animation;
            public int frames;
            public int columns;
            public int rows;
            public int[] frameDurationsMs;
            public int animationLoopFrom;
            public int oneTimePrefixDurationMs;
        }

        private sealed class AnimatedActor
        {
            public Image image;
            public Sprite[] frames;
        }

        private TimelineData data;
        private RectTransform root;
        private RectTransform viewport;
        private RectTransform wideRoot;
        private RectTransform closeRoot;
        private Image flash;
        private Shot currentShot;
        private string currentSpeaker;
        private readonly Dictionary<string, AnimatedActor> portraits = new();
        private readonly List<Sprite> runtimeSprites = new();

        public bool IsPlaying { get; private set; }
        public string CurrentShotId => currentShot?.id;
        public event Action<string> SpeakerChanged;

        public void Initialize(Transform parent)
        {
            if (root) return;
            var timing = Resources.Load<TextAsset>(ResourceRoot + "timing_positions");
            if (!timing) throw new InvalidOperationException("Red Final cutscene timing is missing from Resources.");
            data = JsonUtility.FromJson<TimelineData>(timing.text);

            if (!parent) parent = OverlayCanvas.Create(transform, "Red Final Canvas", 10).transform;
            root = OverlayCanvas.CreateImage(parent, "Red Final Cutscene", Color.black, true).rectTransform;
            viewport = NewRect(root, "Viewport");
            viewport.sizeDelta = new Vector2(640f, 360f);
            wideRoot = NewRect(viewport, "Wide");
            OverlayCanvas.Stretch(wideRoot);
            closeRoot = NewRect(viewport, "Close");
            OverlayCanvas.Stretch(closeRoot);

            Picture(wideRoot, "Background", LoadPicture("backgrounds/wide", new Vector2(0.5f, 0.5f)),
                Vector2.zero, viewport.sizeDelta, new Vector2(0.5f, 0.5f));
            Picture(closeRoot, "Background", LoadPicture("backgrounds/close", new Vector2(0.5f, 0.5f)),
                Vector2.zero, viewport.sizeDelta, new Vector2(0.5f, 0.5f));

            //Back-to-front order follows the feet, as specified by the pack.
            Array.Sort(data.world, (a, b) => a.y.CompareTo(b.y));
            foreach (var actor in data.world)
                Picture(wideRoot, actor.id, LoadPicture($"characters/{actor.id}/world", new Vector2(0.5f, 0f)),
                    Position(actor.x, actor.y), new Vector2(actor.canvasWidth, actor.canvasHeight) * actor.worldScale * PixelScale,
                    new Vector2(0.5f, 0f));

            foreach (var shot in data.timeline)
            foreach (var actor in shot.actors)
            {
                var frames = LoadFrames(actor);
                var image = Picture(closeRoot, actor.id, frames[0], Position(actor.centerX, actor.centerY),
                    new Vector2(actor.width, actor.height) * PixelScale, new Vector2(0.5f, 0.5f));
                image.gameObject.SetActive(false);
                portraits.Add(actor.id, new AnimatedActor { image = image, frames = frames });
            }

            flash = OverlayCanvas.CreateImage(root, "White Flash", Color.white, true);
            FitViewport();
        }

        public IEnumerator Play()
        {
            Initialize(null);
            root.gameObject.SetActive(true);
            currentShot = null;
            IsPlaying = true;
            float started = Time.unscaledTime;
            bool completed = false;
            try
            {
                while ((Time.unscaledTime - started) * 1000f < data.totalDurationMs)
                {
                    ApplyTime((Time.unscaledTime - started) * 1000f);
                    yield return null;
                }
                completed = true;
            }
            finally
            {
                IsPlaying = false;
                SetSpeaker(null);
                //The ending fades this last picture to white before revealing the hospital.
                if (!completed && root) root.gameObject.SetActive(false);
            }
        }

        public void Hide()
        {
            if (root) root.gameObject.SetActive(false);
        }

        private void ApplyTime(float elapsedMs)
        {
            Shot next = data.timeline[data.timeline.Length - 1];
            foreach (var shot in data.timeline)
                if (elapsedMs < shot.endMs) { next = shot; break; }

            if (currentShot != next)
            {
                currentShot = next;
                bool wide = next.actors.Length == 0;
                wideRoot.gameObject.SetActive(wide);
                closeRoot.gameObject.SetActive(!wide);
                foreach (var portrait in portraits.Values) portrait.image.gameObject.SetActive(false);
                foreach (var actor in next.actors) portraits[actor.id].image.gameObject.SetActive(true);
            }

            //The first two authored entries are the full-screen flash and reveal of the wide picture.
            float alpha = next.id == "flash" ? 1f : next.id == "reveal"
                ? 1f - Mathf.Clamp01((elapsedMs - next.startMs) / (next.endMs - next.startMs)) : 0f;
            flash.color = new Color(1f, 1f, 1f, alpha);

            float shotTime = Mathf.Max(0f, elapsedMs - next.startMs);
            string speaker = null;
            foreach (var actor in next.actors)
            {
                var portrait = portraits[actor.id];
                portrait.image.sprite = portrait.frames[FrameAt(actor.frameDurationsMs, actor.animationLoopFrom, shotTime)];
                if (next.id == "green_bunny")
                    speaker = shotTime < (next.endMs - next.startMs) * 0.5f ? "green" : "bunny";
                else if (actor.id != "red" || shotTime >= actor.oneTimePrefixDurationMs) speaker = actor.id;
            }
            SetSpeaker(speaker);
        }

        private void SetSpeaker(string speaker)
        {
            if (currentSpeaker == speaker) return;
            currentSpeaker = speaker;
            SpeakerChanged?.Invoke(speaker);
        }

        //Play the prefix once, then loop only the tail (Red's turns must never repeat).
        private static int FrameAt(int[] durationsMs, int loopFrom, float elapsedMs)
        {
            int prefixMs = 0;
            int tailMs = 0;
            for (int i = 0; i < durationsMs.Length; i++)
                if (i < loopFrom) prefixMs += durationsMs[i]; else tailMs += durationsMs[i];
            if (elapsedMs >= prefixMs && tailMs > 0)
                elapsedMs = prefixMs + Mathf.Repeat(elapsedMs - prefixMs, tailMs);
            float end = 0f;
            for (int i = 0; i < durationsMs.Length; i++)
            {
                end += durationsMs[i];
                if (elapsedMs < end) return i;
            }
            return durationsMs.Length - 1;
        }

        private Sprite LoadPicture(string path, Vector2 pivot)
        {
            var texture = LoadTexture(path);
            return NewSprite(texture, new Rect(0f, 0f, texture.width, texture.height), pivot, path);
        }

        private Sprite[] LoadFrames(CloseActor actor)
        {
            var texture = LoadTexture($"characters/{actor.id}/{actor.animation}");
            int width = texture.width / actor.columns;
            int height = texture.height / actor.rows;
            var frames = new Sprite[actor.frames];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = NewSprite(texture, new Rect(i % actor.columns * width,
                    texture.height - (i / actor.columns + 1) * height, width, height),
                    new Vector2(0.5f, 0.5f), $"{actor.id}_{i:00}");
            return frames;
        }

        private static Texture2D LoadTexture(string path)
        {
            var texture = Resources.Load<Texture2D>(ResourceRoot + path);
            if (!texture) throw new InvalidOperationException($"Red Final cutscene artwork is missing: {path}");
            return texture;
        }

        private Sprite NewSprite(Texture2D texture, Rect rect, Vector2 pivot, string name)
        {
            var sprite = Sprite.Create(texture, rect, pivot, 16f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            runtimeSprites.Add(sprite);
            return sprite;
        }

        private static RectTransform NewRect(Transform parent, string name)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static Image Picture(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Vector2 pivot)
        {
            var image = OverlayCanvas.CreateImage(parent, name, Color.white, false);
            image.sprite = sprite;
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }

        private static Vector2 Position(float x, float y) => new((x - 160f) * PixelScale, (90f - y) * PixelScale);

        private void LateUpdate() => FitViewport();

        private void FitViewport()
        {
            if (!root || !viewport) return;
            float scale = Mathf.Min(root.rect.width / 640f, root.rect.height / 360f);
            viewport.localScale = new Vector3(scale, scale, 1f);
        }

        private void OnDestroy()
        {
            if (root) Destroy(root.gameObject);
            foreach (var sprite in runtimeSprites) if (sprite) Destroy(sprite);
        }
    }
}
