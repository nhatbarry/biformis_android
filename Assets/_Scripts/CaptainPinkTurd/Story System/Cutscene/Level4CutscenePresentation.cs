using System;
using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.InkDialogue;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>The wounded arrival and the supplied memory animations, on the existing Ink stage.</summary>
    public sealed class Level4CutscenePresentation : MonoBehaviour
    {
        private const string ResourceRoot = "Story Cutscene/Level4/";
        private const float DestinationX = -440f;

        [Serializable] private sealed class AnimationData { public ClipData[] clips; }
        [Serializable] private sealed class ClipData
        {
            public string name;
            public bool loop;
            public string next;
            public FrameData[] frames;
        }
        [Serializable] private sealed class FrameData
        {
            public string texture;
            public int x, y, width, height;
            public float duration;
            public Vector2[] hull;
            public OpaqueBoxGraphic.EdgeRun[] edgeRuns;
        }
        [Serializable] private sealed class DoorGeometry
        {
            public int x, y, width, height, canvasWidth, canvasHeight;
        }

        private readonly Dictionary<string, StageActor> actors = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<Sprite> createdSprites = new();
        private readonly HashSet<string> configured = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Sprite, OpaqueBoxGraphic.Shape> boxShapes = new();
        private Action<float, float> shake;
        private StageActor a, room;
        private StageActorAnimation aAnimation;
        private DoorBreakGraphic door;
        private Image doorFrame;
        private Coroutine entrance;
        private float breakTime = -1f;
        private bool roomWasCast;

        public void Initialize(StageActor[] stageActors, Action<float, float> shakeStage)
        {
            foreach (var actor in stageActors)
                if (actor) actors[actor.ActorId] = actor;
            actors.TryGetValue("A4", out a);
            actors.TryGetValue("DarkRoom", out room);
            if (a) aAnimation = a.GetComponent<StageActorAnimation>();
            shake = shakeStage;
        }

        //Called before actors are enabled, so the supplied art is present on their first visible frame.
        public void PrepareCast(HashSet<string> cast)
        {
            bool roomIsCast = cast.Contains("DarkRoom");
            if (roomIsCast)
            {
                PrepareDoor();
                if (!roomWasCast && door)
                {
                    breakTime = -1f;
                    door.gameObject.SetActive(true);
                    door.SetDamage(0f, 0f);
                }
            }
            roomWasCast = roomIsCast;
            if (cast.Contains("Box")) ConfigureActor("Box", "Box", 0.3125f);
            if (cast.Contains("Drown")) ConfigureActor("Drown", "Rescue", 1f);
        }

        public void BeginEntrance(string actorId)
        {
            if (!actorId.Trim().Equals("A4", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"Unknown wounded entrance actor: {actorId}");
                ReleaseHold();
                return;
            }
            if (entrance != null) StopCoroutine(entrance);
            PrepareDoor();
            if (!aAnimation || !door || !PrepareRecovery())
            {
                Debug.LogError("Level 4 arrival assets are missing.");
                if (a)
                {
                    ((RectTransform)a.transform).anchoredPosition = new Vector2(DestinationX, -28f);
                    a.SetVisible(true);
                }
                ReleaseHold();
                return;
            }
            entrance = StartCoroutine(Enter());
        }

        private IEnumerator Enter()
        {
            var rect = (RectTransform)a.transform;
            a.SetVisible(false);
            rect.localRotation = Quaternion.identity;
            rect.localScale = Vector3.one;
            rect.anchoredPosition = new Vector2(-558f, -28f);
            door.gameObject.SetActive(true);
            breakTime = -1f;
            door.SetDamage(0f, 0f);

            //Each strike leaves a permanent bend and a larger crack before the next one lands.
            for (int beat = 0; beat < 4; beat++)
            {
                shake?.Invoke(3f + beat * 2f, 0.2f + beat * 0.035f);
                if (beat == 3)
                {
                    breakTime = Time.unscaledTime;
                    door.Break(0f);
                    break;
                }
                float damage = (beat + 1) / 3f;
                float duration = 0.85f - beat * 0.12f;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    door.SetDamage(damage, Mathf.Exp(-elapsed * 9f));
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
                door.SetDamage(damage, 0f);
            }

            yield return new WaitForSecondsRealtime(0.1f);
            a.SetVisible(true);
            aAnimation.Play("fall");
            yield return Slide(rect, new Vector2(-504f, -28f), ClipSeconds("fall"));
            shake?.Invoke(4f, 0.2f);
            aAnimation.Play("prone");
            yield return new WaitForSecondsRealtime(0.65f);
            aAnimation.Play("getup");
            yield return new WaitForSecondsRealtime(ClipSeconds("getup"));
            aAnimation.Play("idle");
            yield return new WaitForSecondsRealtime(0.35f);
            aAnimation.Play("walk");
            yield return Slide(rect, new Vector2(DestinationX, -28f), 3.2f);
            aAnimation.Play("idle");
            entrance = null;
            //The following Ink cast reveals the box only here, then keeps the existing player handoff.
            ReleaseHold();
        }

        private static IEnumerator Slide(RectTransform rect, Vector2 destination, float seconds)
        {
            Vector2 start = rect.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                rect.anchoredPosition = Vector2.Lerp(start, destination, Mathf.Clamp01(elapsed / seconds));
                yield return null;
            }
            rect.anchoredPosition = destination;
        }

        private void Update()
        {
            if (door && breakTime >= 0f && door.gameObject.activeInHierarchy)
            {
                float elapsed = Time.unscaledTime - breakTime;
                door.Break(elapsed);
                if (elapsed >= 1.8f) door.gameObject.SetActive(false);
            }
            if (room && room.TryGetComponent(out Image roomImage))
            {
                if (door) door.color = roomImage.color;
                if (doorFrame) doorFrame.color = roomImage.color;
            }
            if (entrance != null && (!DialogueManager.HasInstance || !DialogueManager.Instance.DialogueIsPlaying))
            {
                StopCoroutine(entrance);
                entrance = null;
                if (aAnimation) aAnimation.Play("idle");
            }
        }

        private bool PrepareRecovery()
        {
            if (configured.Contains("ARecovery")) return true;
            var clips = LoadClips("ARecovery");
            if (clips == null) return false;
            foreach (string required in new[] { "fall", "prone", "getup" })
            {
                bool found = false;
                foreach (var clip in clips)
                    if (clip.name == required && clip.frames.Length > 0) found = true;
                if (!found) return false;
            }
            foreach (var clip in clips) aAnimation.AddClip(clip);
            var run = aAnimation.GetClip("run");
            if (run == null) return false;
            var slowDurations = new float[run.frames.Length];
            for (int i = 0; i < slowDurations.Length; i++)
                slowDurations[i] = (run.durations != null && i < run.durations.Length ? run.durations[i] : 0.1f) * 2.4f;
            aAnimation.AddClip(new StageActorAnimation.Clip
            {
                name = "walk", frames = run.frames, durations = slowDurations, loop = true
            });
            configured.Add("ARecovery");
            return true;
        }

        private float ClipSeconds(string name)
        {
            var clip = aAnimation.GetClip(name);
            float seconds = 0f;
            for (int i = 0; i < clip.frames.Length; i++)
                seconds += clip.durations != null && i < clip.durations.Length ? Mathf.Max(0.01f, clip.durations[i]) : 0.1f;
            return seconds;
        }

        private void ConfigureActor(string id, string manifest, float pixelSize)
        {
            if (configured.Contains(id) || !actors.TryGetValue(id, out var actor)) return;
            var clips = LoadClips(manifest);
            if (clips == null || clips.Length == 0) return;
            var animation = actor.GetComponent<StageActorAnimation>();
            animation.Configure(clips, clips[0].name, pixelSize);
            if (id == "Box")
            {
                var obj = new GameObject("Black faces and white edges", typeof(RectTransform), typeof(CanvasRenderer));
                obj.transform.SetParent(actor.transform, false);
                var faces = obj.AddComponent<OpaqueBoxGraphic>();
                faces.rectTransform.anchorMin = Vector2.zero;
                faces.rectTransform.anchorMax = Vector2.one;
                faces.rectTransform.sizeDelta = Vector2.zero;
                faces.rectTransform.anchoredPosition = Vector2.zero;
                faces.Configure(actor.GetComponent<Image>(), boxShapes);
            }
            if (id == "Drown")
            {
                //Keep the existing loop clip name available to other authored stage tags.
                animation.AddClip(new StageActorAnimation.Clip
                {
                    name = "loop", frames = clips[0].frames, durations = clips[0].durations, loop = true
                });
            }
            configured.Add(id);
        }

        private StageActorAnimation.Clip[] LoadClips(string manifest)
        {
            var json = Resources.Load<TextAsset>(ResourceRoot + manifest);
            if (!json) return null;
            var data = JsonUtility.FromJson<AnimationData>(json.text);
            var clips = new List<StageActorAnimation.Clip>();
            foreach (var entry in data.clips)
            {
                var frames = new Sprite[entry.frames.Length];
                var durations = new float[frames.Length];
                for (int i = 0; i < frames.Length; i++)
                {
                    var frame = entry.frames[i];
                    var texture = Resources.Load<Texture2D>(frame.texture);
                    if (!texture) throw new InvalidOperationException($"Missing cutscene texture: {frame.texture}");
                    frames[i] = NewSprite(texture, new Rect(frame.x, texture.height - frame.y - frame.height,
                        frame.width, frame.height));
                    if (frame.hull != null && frame.edgeRuns != null)
                        boxShapes[frames[i]] = new OpaqueBoxGraphic.Shape { hull = frame.hull, edges = frame.edgeRuns };
                    durations[i] = frame.duration;
                }
                clips.Add(new StageActorAnimation.Clip
                {
                    name = entry.name, frames = frames, durations = durations, loop = entry.loop, next = entry.next
                });
            }
            return clips.ToArray();
        }

        private void PrepareDoor()
        {
            if (door || !room) return;
            var roomTexture = Resources.Load<Texture2D>(ResourceRoot + "DungeonRoom");
            var frameTexture = Resources.Load<Texture2D>(ResourceRoot + "DungeonDoorFrame");
            var leafTexture = Resources.Load<Texture2D>(ResourceRoot + "DungeonDoorLeaf");
            var geometryJson = Resources.Load<TextAsset>(ResourceRoot + "DoorGeometry");
            if (!roomTexture || !frameTexture || !leafTexture || !geometryJson) return;
            var geometry = JsonUtility.FromJson<DoorGeometry>(geometryJson.text);
            room.GetComponent<StageActorAnimation>().Configure(new[]
            {
                new StageActorAnimation.Clip
                {
                    name = "idle", frames = new[] { NewSprite(roomTexture, new Rect(0, 0, roomTexture.width, roomTexture.height)) },
                    durations = new[] { 0.1f }, loop = true
                }
            }, "idle", 4f);

            doorFrame = CreateImage("Doorway stone frame", room.transform);
            doorFrame.sprite = NewSprite(frameTexture, new Rect(0, 0, frameTexture.width, frameTexture.height));
            doorFrame.rectTransform.sizeDelta = new Vector2(geometry.canvasWidth, geometry.canvasHeight) * 4f;

            var leafObject = new GameObject("Breaking wooden door", typeof(RectTransform), typeof(CanvasRenderer));
            leafObject.transform.SetParent(room.transform, false);
            door = leafObject.AddComponent<DoorBreakGraphic>();
            var rect = door.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(geometry.width, geometry.height) * 4f;
            rect.anchoredPosition = new Vector2(geometry.x + geometry.width * 0.5f - geometry.canvasWidth * 0.5f,
                geometry.canvasHeight * 0.5f - geometry.y - geometry.height * 0.5f) * 4f;
            door.Configure(leafTexture);
        }

        private Sprite NewSprite(Texture2D texture, Rect rect)
        {
            var sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
            createdSprites.Add(sprite);
            return sprite;
        }

        private static Image CreateImage(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(parent, false);
            var image = obj.GetComponent<Image>();
            image.raycastTarget = false;
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return image;
        }

        private static void ReleaseHold()
        {
            if (DialogueManager.HasInstance) DialogueManager.Instance.ReleaseStageHold();
        }

        private void OnDestroy()
        {
            foreach (var sprite in createdSprites)
                if (sprite) Destroy(sprite);
        }
    }
}
