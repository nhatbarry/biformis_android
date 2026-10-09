using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// The boss fight's last frame breaks like glass: cracks run out from the boss across the screen, then the picture
    /// flies apart in shards, showing the dark stage of the ending behind it. The boss and the player don't break with
    /// it: copies of them stay in front, the boss takes its defeated pose, and both slide to where the ending's first
    /// cutscene stands them (see the ink knot "Ending"), the player flickering between B and A.
    /// Built in code on its own overlay canvas (640x360 reference, like the cutscene stage), in unscaled time.
    /// </summary>
    public class GlassShatter : MonoBehaviour
    {
        //where the ending's cutscene stands the two (canvas units); keep in step with the ink knot "Ending"
        public static readonly Vector2 BossStagePosition = new(-150f, -58f);
        public static readonly Vector2 PlayerStagePosition = new(150f, -22f);
        //the cutscene draws the brothers at 4 canvas units per art pixel, the defeated boss's art at 0.25
        public const float PlayerUnitsPerPixel = 4f;
        public const float BossUnitsPerPixel = 0.25f;

        private const int Radials = 11;
        private const int Rings = 6;
        private const float CrackSeconds = 0.8f;
        private const float BreakSeconds = 1.15f;
        private const float SlideSeconds = 0.8f;
        private const float FlickerSeconds = 0.14f;

        public sealed class Settings
        {
            public Camera camera;
            public Transform boss;
            public Transform player;
            public Sprite glow;
            public Sprite[] bossPose;
            public float[] bossPoseSeconds;
            public Sprite redIdle, blueIdle;
            public AudioClip breakSfx;
        }

        private Settings settings;
        private RectTransform root, glass, cracks;
        private RenderTexture picture;
        private Image flash;
        private readonly List<Shard> shards = new();
        private readonly List<(RectTransform line, float length, float appears)> crackLines = new();

        public bool Finished { get; private set; }

        private sealed class Shard
        {
            public ShardGraphic graphic;
            public Vector2 centre, velocity;
            public float spin;
            public float appears;
        }

        public static GlassShatter Play(Settings settings)
        {
            var shatter = new GameObject("Glass Shatter").AddComponent<GlassShatter>();
            shatter.settings = settings;
            return shatter;
        }

        private IEnumerator Start()
        {
            //one frame for the HUD the caller hid to be gone, then the level is pictured without the two actors
            yield return null;
            var canvas = OverlayCanvas.Create(transform, "Shatter Canvas", 90);
            root = (RectTransform)canvas.transform;
            Canvas.ForceUpdateCanvases();
            Vector2 size = root.rect.size;
            if (size.y <= 0f) size = new Vector2(640f, 360f);

            Vector2 bossOnScreen = ToCanvas(settings.boss ? settings.boss.position : Vector3.zero, size);
            var bossCopy = CopyOf(settings.boss, "Boss", size);
            var playerCopy = CopyOf(settings.player, "Player", size);
            picture = CaptureWithout(settings.boss, settings.player);

            var backdrop = OverlayCanvas.CreateImage(root, "Dark", Color.black, true);
            backdrop.transform.SetAsFirstSibling();
            if (settings.glow)
            {
                var glow = OverlayCanvas.CreateImage(root, "Glow", Color.white, false);
                glow.sprite = settings.glow;
                glow.rectTransform.sizeDelta = new Vector2(640f, 360f);
                glow.transform.SetSiblingIndex(1);
            }
            glass = NewLayer("Glass");
            cracks = NewLayer("Cracks");
            if (bossCopy) bossCopy.transform.SetAsLastSibling();
            if (playerCopy) playerCopy.transform.SetAsLastSibling();
            flash = OverlayCanvas.CreateImage(root, "Flash", new Color(1f, 1f, 1f, 0f), true);

            BuildGlass(bossOnScreen, size);
            yield return Crack();
            yield return Break(bossCopy);
            yield return Slide(bossCopy, playerCopy);
            Finished = true;
        }

        private void OnDestroy()
        {
            if (picture) picture.Release();
        }

        // ------------------------------------------------------------------ the picture and the two copies

        private Vector2 ToCanvas(Vector3 world, Vector2 canvasSize)
        {
            var camera = settings.camera;
            if (!camera) return Vector2.zero;
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return new Vector2((viewport.x - 0.5f) * canvasSize.x, (viewport.y - 0.5f) * canvasSize.y);
        }

        //canvas units per world unit at the camera's current zoom
        private float CanvasPerWorld(Vector2 canvasSize) =>
            settings.camera ? canvasSize.y / (settings.camera.orthographicSize * 2f) : 1f;

        //the actor's visible body (the player's current form) as an Image placed exactly over it
        private Image CopyOf(Transform actor, string copyName, Vector2 canvasSize)
        {
            if (!actor) return null;
            SpriteRenderer body = null;
            foreach (var candidate in actor.GetComponentsInChildren<SpriteRenderer>())
            {
                if (!candidate.enabled || !candidate.sprite || !candidate.gameObject.activeInHierarchy) continue;
                if (actor == settings.player && candidate.name is not ("Red" or "Blue")) continue;
                if (!body || candidate.sprite.rect.size.sqrMagnitude > body.sprite.rect.size.sqrMagnitude) body = candidate;
            }
            if (!body) return null;

            var image = OverlayCanvas.CreateImage(root, copyName, Color.white, false);
            var sprite = body.sprite;
            image.sprite = sprite;
            var rect = image.rectTransform;
            rect.pivot = sprite.pivot / sprite.rect.size;
            Vector3 scale = body.transform.lossyScale;
            rect.sizeDelta = sprite.rect.size / sprite.pixelsPerUnit * new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) * CanvasPerWorld(canvasSize);
            rect.anchoredPosition = ToCanvas(body.transform.position, canvasSize);
            rect.localScale = new Vector3(body.flipX ^ scale.x < 0f ? -1f : 1f, 1f, 1f);
            return image;
        }

        //the level as it is now, without the two actors (their copies stand in front of the glass)
        private RenderTexture CaptureWithout(params Transform[] actors)
        {
            var camera = settings.camera;
            var texture = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24) { name = "Shatter Picture" };
            if (!camera) return texture;

            var hidden = new List<Renderer>();
            foreach (var actor in actors)
            {
                if (!actor) continue;
                foreach (var renderer in actor.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.forceRenderingOff) continue;
                    renderer.forceRenderingOff = true;
                    hidden.Add(renderer);
                }
            }

            var request = new RenderPipeline.StandardRequest { destination = texture };
            if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
            else
            {
                var previous = camera.targetTexture;
                camera.targetTexture = texture;
                camera.Render();
                camera.targetTexture = previous;
            }
            //the copies cover them until the scene ends, so the world needn't draw them again
            return texture;
        }

        private RectTransform NewLayer(string layerName)
        {
            var layer = (RectTransform)new GameObject(layerName, typeof(RectTransform)).transform;
            layer.SetParent(root, false);
            OverlayCanvas.Stretch(layer);
            return layer;
        }

        // ------------------------------------------------------------------ cracks

        //radial cracks from the impact, joined by rings: the shards are the cells between them
        private void BuildGlass(Vector2 impact, Vector2 canvasSize)
        {
            var random = new System.Random(19);
            float Rand(float min, float max) => min + (float)random.NextDouble() * (max - min);

            //far enough to pass every corner of the widest phone
            Vector2 half = new(Mathf.Max(canvasSize.x, canvasSize.y * 2.5f) * 0.5f, canvasSize.y * 0.5f);
            float reach = 0f;
            foreach (var corner in new[] { new Vector2(-half.x, -half.y), new Vector2(half.x, -half.y), new Vector2(-half.x, half.y), half })
                reach = Mathf.Max(reach, (corner - impact).magnitude);
            reach += 60f;

            var nodes = new Vector2[Radials, Rings + 1];
            float start = Rand(0f, Mathf.PI * 2f);
            for (int i = 0; i < Radials; i++)
            {
                float angle = start + i * Mathf.PI * 2f / Radials + Rand(-0.18f, 0.18f);
                nodes[i, 0] = impact;
                for (int k = 1; k <= Rings; k++)
                {
                    //rings close together near the impact, wider further out
                    float radius = reach * Mathf.Pow((float)k / Rings, 1.6f) * Rand(0.88f, 1.08f);
                    float bend = angle + Rand(-0.09f, 0.09f);
                    nodes[i, k] = impact + new Vector2(Mathf.Cos(bend), Mathf.Sin(bend)) * (k == Rings ? reach : radius);
                }
            }

            //a crack reaches ring k at this time; each radial runs at its own pace
            var reachesRing = new float[Radials, Rings + 1];
            for (int i = 0; i < Radials; i++)
            {
                float pace = Rand(0.75f, 1.1f);
                for (int k = 0; k <= Rings; k++) reachesRing[i, k] = CrackSeconds * pace * Mathf.Pow((float)k / Rings, 0.8f);
            }

            for (int i = 0; i < Radials; i++)
            {
                int next = (i + 1) % Radials;
                for (int k = 0; k < Rings; k++)
                {
                    AddCrack(nodes[i, k], nodes[i, k + 1], reachesRing[i, k]);
                    //rings only between the two first rings out, and not every one, so the shards vary in size
                    if (k + 1 < Rings && (k < 2 || random.NextDouble() < 0.7))
                        AddCrack(nodes[i, k + 1], nodes[next, k + 1], Mathf.Max(reachesRing[i, k + 1], reachesRing[next, k + 1]) + 0.04f);

                    var corners = k == 0
                        ? new[] { nodes[i, 0], nodes[i, 1], nodes[next, 1] }
                        : new[] { nodes[i, k], nodes[i, k + 1], nodes[next, k + 1], nodes[next, k] };
                    AddShard(corners, canvasSize, Mathf.Max(reachesRing[i, k + 1], reachesRing[next, k + 1]), Rand(0.86f, 1f), impact,
                        Rand(420f, 760f), Rand(-320f, 320f));
                }
            }
        }

        private void AddCrack(Vector2 from, Vector2 to, float appears)
        {
            var line = OverlayCanvas.CreateImage(cracks, "Crack", new Color(1f, 1f, 1f, 0.9f), false).rectTransform;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0f, 0.5f);
            line.anchoredPosition = from;
            Vector2 along = to - from;
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(along.y, along.x) * Mathf.Rad2Deg);
            line.sizeDelta = new Vector2(0f, 2f);
            crackLines.Add((line, along.magnitude, appears));
        }

        private void AddShard(Vector2[] corners, Vector2 canvasSize, float appears, float shade, Vector2 impact, float speed, float spin)
        {
            Vector2 centre = Vector2.zero;
            foreach (var corner in corners) centre += corner;
            centre /= corners.Length;

            var graphic = new GameObject("Shard", typeof(RectTransform)).AddComponent<ShardGraphic>();
            var rect = graphic.rectTransform;
            rect.SetParent(glass, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = centre;
            graphic.raycastTarget = false;
            graphic.Set(picture, corners, centre, canvasSize);

            Vector2 outward = centre - impact;
            outward = outward.sqrMagnitude < 1f ? Random.insideUnitCircle.normalized : outward.normalized;
            shards.Add(new Shard
            {
                graphic = graphic,
                centre = centre,
                velocity = outward * speed,
                spin = spin,
                appears = appears,
            });
            graphic.color = Color.white;
            graphic.facetShade = shade;
        }

        private IEnumerator Crack()
        {
            float started = Time.unscaledTime;
            float end = CrackSeconds * 1.15f + 0.1f;
            Vector2 home = glass.anchoredPosition;
            while (true)
            {
                float t = Time.unscaledTime - started;
                foreach (var (line, length, appears) in crackLines)
                {
                    float grow = Mathf.Clamp01((t - appears) / 0.09f);
                    line.sizeDelta = new Vector2(length * grow, 2f);
                }
                //each facet catches the light a little differently once the cracks around it are there
                foreach (var shard in shards)
                    if (t >= shard.appears) shard.graphic.color = new Color(shard.graphic.facetShade, shard.graphic.facetShade, shard.graphic.facetShade, 1f);
                //the glass trembles while it splits
                Vector2 jitter = t < 0.25f ? Random.insideUnitCircle * 3f : Vector2.zero;
                glass.anchoredPosition = cracks.anchoredPosition = home + jitter;
                if (t >= end) break;
                yield return null;
            }
            glass.anchoredPosition = cracks.anchoredPosition = home;
            yield return new WaitForSecondsRealtime(0.25f);
        }

        // ------------------------------------------------------------------ break

        private IEnumerator Break(Image bossCopy)
        {
            if (settings.breakSfx)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.ignoreListenerPause = true;
                source.PlayOneShot(settings.breakSfx);
            }
            flash.color = new Color(1f, 1f, 1f, 0.85f);
            //the flash hides the boss taking its defeated pose
            ShowBossPose(bossCopy, 0);
            Destroy(cracks.gameObject);

            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < BreakSeconds)
            {
                float t = Time.unscaledTime - started;
                flash.color = new Color(1f, 1f, 1f, 0.85f * (1f - Mathf.Clamp01(t / 0.25f)));
                foreach (var shard in shards)
                {
                    if (!shard.graphic) continue;
                    //thrown out, speeding up, turning and coming slightly towards the viewer
                    var rect = shard.graphic.rectTransform;
                    rect.anchoredPosition = shard.centre + shard.velocity * t + shard.velocity.normalized * (900f * t * t);
                    rect.localRotation = Quaternion.Euler(0f, 0f, shard.spin * t);
                    rect.localScale = Vector3.one * (1f + 0.35f * t);
                }
                AnimateBoss(bossCopy, Time.unscaledTime);
                yield return null;
            }
            flash.color = new Color(1f, 1f, 1f, 0f);
            Destroy(glass.gameObject);
        }

        // ------------------------------------------------------------------ to the cutscene's places

        private IEnumerator Slide(Image bossCopy, Image playerCopy)
        {
            var bossFrom = bossCopy ? bossCopy.rectTransform.anchoredPosition : Vector2.zero;
            var playerFrom = playerCopy ? playerCopy.rectTransform.anchoredPosition : Vector2.zero;
            var playerSizeFrom = playerCopy ? playerCopy.rectTransform.sizeDelta : Vector2.zero;
            float playerFacingFrom = playerCopy ? playerCopy.rectTransform.localScale.x : 1f;
            Vector2 playerSizeTo = playerSizeFrom;
            if (playerCopy && playerCopy.sprite)
            {
                //the cutscene's size: its art pixels at the brothers' scale
                playerSizeTo = playerCopy.sprite.rect.size * PlayerUnitsPerPixel;
            }

            float started = Time.unscaledTime;
            while (true)
            {
                float t = Mathf.Clamp01((Time.unscaledTime - started) / SlideSeconds);
                float eased = Mathf.SmoothStep(0f, 1f, t);
                if (bossCopy) bossCopy.rectTransform.anchoredPosition = Vector2.Lerp(bossFrom, BossStagePosition, eased);
                if (playerCopy)
                {
                    var rect = playerCopy.rectTransform;
                    rect.anchoredPosition = Vector2.Lerp(playerFrom, PlayerStagePosition, eased);
                    rect.sizeDelta = Vector2.Lerp(playerSizeFrom, playerSizeTo, eased);
                    //turns to face the boss on the way
                    if (t > 0.5f) rect.localScale = new Vector3(-1f, 1f, 1f);
                    else rect.localScale = new Vector3(playerFacingFrom, 1f, 1f);
                }
                AnimateBoss(bossCopy, Time.unscaledTime);
                if (t >= 1f) break;
                yield return null;
            }

            //in place: B and A flicker, like the cutscene's first frame
            float flickerStarted = Time.unscaledTime;
            while (Time.unscaledTime - flickerStarted < 0.9f)
            {
                if (playerCopy && settings.redIdle && settings.blueIdle)
                {
                    bool red = Mathf.FloorToInt((Time.unscaledTime - flickerStarted) / FlickerSeconds) % 2 == 0;
                    SetSprite(playerCopy, red ? settings.redIdle : settings.blueIdle, PlayerUnitsPerPixel);
                }
                AnimateBoss(bossCopy, Time.unscaledTime);
                yield return null;
            }
        }

        private void ShowBossPose(Image bossCopy, int frame)
        {
            if (!bossCopy || settings.bossPose == null || settings.bossPose.Length == 0) return;
            SetSprite(bossCopy, settings.bossPose[Mathf.Clamp(frame, 0, settings.bossPose.Length - 1)], BossUnitsPerPixel);
            bossCopy.rectTransform.localScale = Vector3.one; //the art already faces right, towards the brothers
        }

        //the defeated boss breathes: its pose's frames with their own durations
        private void AnimateBoss(Image bossCopy, float now)
        {
            var frames = settings.bossPose;
            if (!bossCopy || frames == null || frames.Length == 0) return;
            float total = 0f;
            for (int i = 0; i < frames.Length; i++) total += Seconds(i);
            float t = total > 0f ? now % total : 0f;
            for (int i = 0; i < frames.Length; i++)
            {
                t -= Seconds(i);
                if (t >= 0f) continue;
                if (bossCopy.sprite != frames[i]) SetSprite(bossCopy, frames[i], BossUnitsPerPixel);
                return;
            }
        }

        private float Seconds(int frame) =>
            settings.bossPoseSeconds != null && frame < settings.bossPoseSeconds.Length ? Mathf.Max(0.01f, settings.bossPoseSeconds[frame]) : 0.3f;

        private static void SetSprite(Image image, Sprite sprite, float unitsPerPixel)
        {
            if (!sprite) return;
            image.sprite = sprite;
            var rect = image.rectTransform;
            rect.pivot = sprite.pivot / sprite.rect.size;
            rect.sizeDelta = sprite.rect.size * unitsPerPixel;
        }
    }

    /// <summary>One piece of the broken picture: a polygon of the captured screen, drawn where it was.</summary>
    public class ShardGraphic : MaskableGraphic
    {
        private Texture texture;
        private Vector2[] corners;
        private Vector2 centre, canvasSize;
        public float facetShade = 1f;

        public override Texture mainTexture => texture ? texture : s_WhiteTexture;

        public void Set(Texture picture, Vector2[] polygon, Vector2 polygonCentre, Vector2 size)
        {
            texture = picture;
            corners = polygon;
            centre = polygonCentre;
            canvasSize = size;
            SetAllDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (corners == null || corners.Length < 3) return;
            Color32 tint = color;
            //a fan from the centre: every cell is roughly convex around it
            vh.AddVert(Vector3.zero, tint, Uv(centre));
            foreach (var corner in corners) vh.AddVert(corner - centre, tint, Uv(corner));
            for (int i = 0; i < corners.Length; i++)
                vh.AddTriangle(0, 1 + i, 1 + (i + 1) % corners.Length);
        }

        //canvas position (from the screen's centre) to the captured picture
        private Vector2 Uv(Vector2 position) =>
            new(position.x / canvasSize.x + 0.5f, position.y / canvasSize.y + 0.5f);
    }
}
