using System;
using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.Localization;
using CaptainPinkTurd.Scene.Story;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// The final story sequence: the prison scenes dissolve, B's second personality removes its mask, the two
    /// brothers separate, and the scene resolves in the hospital. Artwork is exported from Scene_KetThuc.zip.
    /// </summary>
    public class EndingSequence : MonoBehaviour
    {
        [SerializeField] private StoryData storyData;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private AudioClip music;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip burstSfx;

        [Header("B's in-game sprite")]
        [SerializeField] private Sprite bIdle;
        [SerializeField] private Sprite[] bWalk;

        private const int Scale = 4;
        private const float FrameSeconds = 0.1f;

        private RectTransform stage;
        private Image whiteBase;
        private Image worldBackground;
        private Image whiteOverlay;
        private Image blackOverlay;
        private Image flashOverlay;
        private Image villainBody;
        private Image splitB;
        private Image splitA;
        private Image splitMerged;
        private Image hospitalRoom;
        private Image kneelingA;
        private Image portrait;
        private Image portraitFrameBorder;
        private CanvasGroup dialogueGroup;
        private TMP_Text speakerText;
        private TMP_Text lineText;
        private TMP_Text continueText;

        private Sprite[] villainFrames;
        private Sprite[] villainPortraitFrames;
        private Sprite[] aPortraitFrames;
        private Sprite[] aBloodyFrames;
        private Sprite[] hospitalFrames;
        private Sprite[] kneelingFrames;

        private readonly List<FrameTrack> tracks = new();
        private readonly List<FlyingPixel> flyingPixels = new();
        private readonly List<BurstPixelGraphic> worldBursts = new();
        private readonly List<Sprite> runtimeSprites = new();
        private InputAction advanceAction;
        private bool advanceRequested;
        private float shakeUntil;
        private Vector2 stageHome;
        private TMP_Text theEndText;

        private sealed class FrameTrack
        {
            public Image image;
            public Sprite[] frames;
            public int first;
            public int count;
            public float secondsPerFrame;
            public float started;
            public bool loop;
        }

        private sealed class FlyingPixel
        {
            public Image image;
            public Vector2 origin;
            public Vector2 velocity;
            public float delay;
            public float duration;
            public float born;
        }

        private IEnumerator Start()
        {
            if (music) MusicManager.Instance.Play(music, loop: true);
            LoadArt();
            BuildStage();

            yield return new WaitForSecondsRealtime(0.4f);
            yield return BurstWorlds();
            yield return MaskBreak();
            yield return SeparateBrothers();
            yield return HospitalEnding();

            if (StoryFlow.IsStoryRunning) StoryFlow.Advance(storyData);
        }

        private void OnDestroy()
        {
            advanceAction?.Dispose();
            foreach (var sprite in runtimeSprites)
                if (sprite) Destroy(sprite);
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = tracks.Count - 1; i >= 0; i--)
            {
                var track = tracks[i];
                if (!track.image || track.frames == null || track.count <= 0)
                {
                    tracks.RemoveAt(i);
                    continue;
                }

                int frame = Mathf.FloorToInt((now - track.started) / track.secondsPerFrame);
                if (track.loop) frame %= track.count;
                else if (frame >= track.count)
                {
                    track.image.sprite = track.frames[track.first + track.count - 1];
                    tracks.RemoveAt(i);
                    continue;
                }

                track.image.sprite = track.frames[track.first + frame];
            }

            if (Time.unscaledTime < shakeUntil && stage)
            {
                stage.anchoredPosition = stageHome + UnityEngine.Random.insideUnitCircle * 5f;
            }
            else if (stage)
            {
                stage.anchoredPosition = stageHome;
            }

            UpdateFlyingPixels(now);
        }

        private void LoadArt()
        {
            villainFrames = LoadAtlas("Villain_MaskBreak", 40, 40, 8, 25);
            villainPortraitFrames = LoadAtlas("Villain_Portrait_End", 40, 40, 5, 5);
            aPortraitFrames = LoadAtlas("A_Portrait", 40, 40, 6, 6);
            aBloodyFrames = LoadAtlas("A_Bloody", 28, 28, 8, 46);
            hospitalFrames = LoadAtlas("Hospital_End_Room", 160, 90, 5, 20);
            kneelingFrames = LoadAtlas("A_Kneel_Bedside", 18, 18, 3, 9);
        }

        private Sprite[] LoadAtlas(string name, int frameWidth, int frameHeight, int columns, int frameCount)
        {
            var texture = Resources.Load<Texture2D>($"Story Ending/{name}");
            if (!texture)
            {
                Debug.LogError($"Story ending artwork is missing: Resources/Story Ending/{name}.png");
                return Array.Empty<Sprite>();
            }

            var sprites = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                int rowFromTop = i / columns;
                int column = i % columns;
                int y = texture.height - (rowFromTop + 1) * frameHeight;
                sprites[i] = CreateRuntimeSprite(texture,
                    new Rect(column * frameWidth, y, frameWidth, frameHeight), new Vector2(0.5f, 0.5f), 16f);
                sprites[i].name = $"{name}_{i:00}";
            }
            return sprites;
        }

        private void BuildStage()
        {
            var canvas = OverlayCanvas.Create(transform, "Ending Canvas", 10);
            stage = (RectTransform)new GameObject("Stage", typeof(RectTransform)).transform;
            stage.SetParent(canvas.transform, false);
            OverlayCanvas.Stretch(stage);
            stageHome = stage.anchoredPosition;

            whiteBase = OverlayCanvas.CreateImage(stage, "White", Color.white, true);
            whiteBase.gameObject.SetActive(false);
            whiteOverlay = OverlayCanvas.CreateImage(stage, "White Fade", Color.white, true);
            whiteOverlay.color = new Color(1f, 1f, 1f, 0f);
            blackOverlay = OverlayCanvas.CreateImage(stage, "Black Fade", Color.black, true);
            blackOverlay.color = new Color(0f, 0f, 0f, 0f);
            flashOverlay = OverlayCanvas.CreateImage(stage, "Flash", Color.white, true);
            flashOverlay.color = new Color(1f, 1f, 1f, 0f);

            var cageRoom = Resources.Load<Texture2D>("Story Ending/Explode_BG_1_CageRoom");
            if (cageRoom)
            {
                worldBackground = OverlayCanvas.CreateImage(stage, "World Background", Color.white, true);
                worldBackground.sprite = CreateRuntimeSprite(cageRoom, new Rect(0, 0, cageRoom.width, cageRoom.height), new Vector2(0.5f, 0.5f), 16f);
            }

            whiteBase.transform.SetAsFirstSibling();
            if (worldBackground) worldBackground.transform.SetAsLastSibling();

            villainBody = CreateImage(stage, "Villain", Vector2.zero, new Vector2(120f, 144f));
            villainBody.transform.localScale = new Vector3(-1f, 1f, 1f);
            villainBody.gameObject.SetActive(false);

            splitMerged = CreateImage(stage, "Merged Brothers", Vector2.zero, new Vector2(96f, 112f));
            splitB = CreateImage(stage, "B", ArtPosition(28f, 53f), new Vector2(92f, 104f));
            splitA = CreateImage(stage, "A", ArtPosition(48f, 53f), new Vector2(92f, 104f));
            splitMerged.gameObject.SetActive(false);
            splitB.gameObject.SetActive(false);
            splitA.gameObject.SetActive(false);

            hospitalRoom = CreateImage(stage, "Hospital Room", Vector2.zero, new Vector2(640f, 360f));
            hospitalRoom.gameObject.SetActive(false);
            kneelingA = CreateImage(stage, "A at bedside", ArtPosition(100f, 40f), new Vector2(72f, 72f));
            kneelingA.gameObject.SetActive(false);

            BuildDialoguePanel(stage);
            whiteOverlay.transform.SetAsLastSibling();
            blackOverlay.transform.SetAsLastSibling();
            flashOverlay.transform.SetAsLastSibling();
            theEndText.transform.SetAsLastSibling();

            advanceAction = new InputAction("Ending Continue", InputActionType.Button, "<Pointer>/press");
            advanceAction.AddBinding("<Keyboard>/space");
            advanceAction.AddBinding("<Keyboard>/enter");
            advanceAction.AddBinding("<Keyboard>/e");
            advanceAction.AddBinding("<Gamepad>/buttonSouth");
            advanceAction.performed += _ => advanceRequested = true;
            advanceAction.Enable();
        }

        private void BuildDialoguePanel(Transform parent)
        {
            var root = new GameObject("Dialogue UI", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            dialogueGroup = root.GetComponent<CanvasGroup>();

            var border = OverlayCanvas.CreateImage(root.transform, "Dialogue Border", new Color(0.55f, 0.83f, 0.78f, 1f), false);
            Place(border.rectTransform, new Vector2(-8f, -120f), new Vector2(608f, 120f));

            var panel = OverlayCanvas.CreateImage(root.transform, "Dialogue Panel", new Color(0.12f, 0.16f, 0.23f, 1f), false);
            Place(panel.rectTransform, new Vector2(-8f, -120f), new Vector2(600f, 112f));

            portraitFrameBorder = OverlayCanvas.CreateImage(root.transform, "Portrait Frame", new Color(0.67f, 0.87f, 0.82f, 1f), false);
            Place(portraitFrameBorder.rectTransform, new Vector2(-218f, -108f), new Vector2(160f, 160f));
            portrait = OverlayCanvas.CreateImage(root.transform, "Portrait", Color.white, false);
            Place(portrait.rectTransform, new Vector2(-218f, -108f), new Vector2(144f, 144f));

            var textRoot = new GameObject("Dialogue Text", typeof(RectTransform));
            textRoot.transform.SetParent(root.transform, false);
            Place((RectTransform)textRoot.transform, new Vector2(68f, -120f), new Vector2(410f, 90f));
            speakerText = CreateText(textRoot.transform, "Speaker", 22f, TextAlignmentOptions.TopLeft, new Color(0.57f, 0.85f, 0.8f, 1f));
            var speakerRect = speakerText.rectTransform;
            StretchAtTopLeft(speakerRect, Vector2.zero, new Vector2(400f, 26f));
            lineText = CreateText(textRoot.transform, "Line", 20f, TextAlignmentOptions.TopLeft, Color.white);
            StretchAtTopLeft(lineText.rectTransform, new Vector2(0f, -27f), new Vector2(400f, 64f));
            lineText.enableWordWrapping = true;
            lineText.overflowMode = TextOverflowModes.Truncate;
            continueText = CreateText(textRoot.transform, "Continue", 18f, TextAlignmentOptions.BottomRight, new Color(0.57f, 0.85f, 0.8f, 1f));
            StretchAtTopLeft(continueText.rectTransform, new Vector2(370f, -64f), new Vector2(30f, 24f));
            continueText.text = "▼";
            dialogueGroup.alpha = 0f;
            dialogueGroup.interactable = false;
            dialogueGroup.blocksRaycasts = false;

            var endText = CreateText(parent, "The End", 64f, TextAlignmentOptions.Center, new Color(0.92f, 0.17f, 0.29f, 0f));
            endText.rectTransform.anchorMin = Vector2.zero;
            endText.rectTransform.anchorMax = Vector2.one;
            endText.rectTransform.offsetMin = endText.rectTransform.offsetMax = Vector2.zero;
            theEndText = endText;
        }

        private TMP_Text CreateText(Transform parent, string name, float size, TextAlignmentOptions alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.enableAutoSizing = name == "Line";
            if (text.enableAutoSizing)
            {
                text.fontSizeMin = 16f;
                text.fontSizeMax = size;
            }
            return text;
        }

        private static void StretchAtTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var image = OverlayCanvas.CreateImage(parent, name, Color.white, false);
            Place(image.rectTransform, position, size);
            image.preserveAspect = true;
            return image;
        }

        private Sprite CreateRuntimeSprite(Texture2D texture, Rect rect, Vector2 pivot, float pixelsPerUnit)
        {
            var sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            runtimeSprites.Add(sprite);
            return sprite;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Vector2 ArtPosition(float xFromLeft, float yFromTop) =>
            new Vector2((xFromLeft - 80f) * Scale, (45f - yFromTop) * Scale);

        private IEnumerator BurstWorlds()
        {
            var dungeon = Resources.Load<Texture2D>("Story Ending/Explode_BG_2_Dungeon");
            if (dungeon && worldBackground)
            {
                worldBackground.sprite = CreateRuntimeSprite(dungeon, new Rect(0, 0, dungeon.width, dungeon.height), new Vector2(0.5f, 0.5f), 16f);
                yield return new WaitForSecondsRealtime(0.6f);
            }
            if (dungeon) SpawnBurst(dungeon);
            yield return new WaitForSecondsRealtime(1.9f);

            var cage = Resources.Load<Texture2D>("Story Ending/Explode_BG_1_CageRoom");
            if (cage && worldBackground)
                worldBackground.sprite = CreateRuntimeSprite(cage, new Rect(0, 0, cage.width, cage.height), new Vector2(0.5f, 0.5f), 16f);
            if (cage) SpawnBurst(cage);
            if (sfxSource && burstSfx) sfxSource.PlayOneShot(burstSfx);
            yield return Fade(whiteOverlay, 1f, 0.5f);
            yield return new WaitForSecondsRealtime(1.2f);
            whiteOverlay.color = new Color(1f, 1f, 1f, 0f);
            ClearWorldBurstPixels();
            if (worldBackground) worldBackground.gameObject.SetActive(false);
            whiteBase.gameObject.SetActive(true);
        }

        private void ClearWorldBurstPixels()
        {
            foreach (var burst in worldBursts)
            {
                if (burst) Destroy(burst.gameObject);
            }
            worldBursts.Clear();
        }

        private void SpawnBurst(Texture2D texture)
        {
            var graphic = new GameObject("World Burst", typeof(RectTransform)).AddComponent<BurstPixelGraphic>();
            graphic.transform.SetParent(stage, false);
            OverlayCanvas.Stretch(graphic.rectTransform);
            graphic.Initialize(texture, 4, Scale);
            worldBursts.Add(graphic);
        }

        private void UpdateFlyingPixels(float now)
        {
            for (int i = flyingPixels.Count - 1; i >= 0; i--)
            {
                var particle = flyingPixels[i];
                if (!particle.image)
                {
                    flyingPixels.RemoveAt(i);
                    continue;
                }

                float t = now - particle.born - particle.delay;
                if (t < 0f) continue;
                float p = Mathf.Clamp01(t / particle.duration);
                particle.image.rectTransform.anchoredPosition = particle.origin + particle.velocity * t + Vector2.up * (240f * t * t);
                var color = particle.image.color;
                color.a = 1f - p;
                particle.image.color = color;
                if (p >= 1f)
                {
                    Destroy(particle.image.gameObject);
                    flyingPixels.RemoveAt(i);
                }
            }
        }

        private IEnumerator MaskBreak()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            villainBody.gameObject.SetActive(true);
            SetBodyFrame(0);
            villainBody.color = new Color(1f, 1f, 1f, 0f);
            yield return Fade(villainBody, 1f, 0.9f);
            yield return new WaitForSecondsRealtime(0.35f);

            yield return Say("Villain", "Lại muốn quay về à? Ngoài kia chỉ có đau đớn thôi.", "Want to go back again? There's nothing out there but pain.", villainPortraitFrames, 0, 1, 0.1f, new Color(0.82f, 0.66f, 0.52f));
            yield return Say("B", "Tôi biết.", "I know.", villainPortraitFrames, 0, 1, 0.1f, new Color(0.95f, 0.32f, 0.35f));
            yield return Crack(1);
            yield return Say("Villain", "Vậy tại sao? Ở đây ngươi chẳng phải đau gì nữa.", "Then why? You don't have to hurt here.", villainPortraitFrames, 1, 1, 0.1f, new Color(0.82f, 0.66f, 0.52f));
            yield return Crack(2);
            yield return Say("B", "Vì nỗi đau đó là của tôi. Cậu bé ngã trên đường chạy năm ấy cũng là tôi.", "Because that pain is mine. I was the boy who fell on the track all those years ago.", villainPortraitFrames, 2, 1, 0.1f, new Color(0.95f, 0.32f, 0.35f));
            yield return Crack(3);

            yield return new WaitForSecondsRealtime(0.45f);
            SetPortrait(villainPortraitFrames, 4);
            SetBodyRange(10, 7, false);
            flashOverlay.color = Color.white;
            yield return Fade(flashOverlay, 0f, 0.18f);
            shakeUntil = Time.unscaledTime + 0.28f;
            SpawnDissolvePixels(true);
            SetBodyRange(17, 3, false);
            yield return new WaitForSecondsRealtime(1.05f);

            yield return Say("B", "...Tôi chỉ muốn cậu không phải khổ nữa thôi.", "...I only wanted to spare you from suffering.", villainPortraitFrames, 4, 1, 0.1f, new Color(0.95f, 0.32f, 0.35f));
            yield return Say("B", "Tôi biết. Cảm ơn cậu, vì đã chịu đựng thay tôi suốt thời gian qua.", "I know. Thank you for carrying my pain all this time.", villainPortraitFrames, 4, 1, 0.1f, new Color(0.95f, 0.32f, 0.35f));
            yield return Say("B", "Lần này đừng chạy trốn nữa nhé.", "This time, don't run away.", villainPortraitFrames, 4, 1, 0.1f, new Color(0.95f, 0.32f, 0.35f));

            yield return new WaitForSecondsRealtime(0.25f);
            SpawnDissolvePixels(false);
            yield return Fade(villainBody, 0f, 0.7f);
            villainBody.gameObject.SetActive(false);
            yield return new WaitForSecondsRealtime(1.3f);
        }

        private IEnumerator Crack(int step)
        {
            int portraitFrame = Mathf.Clamp(step, 1, 3);
            SetPortrait(villainPortraitFrames, portraitFrame);
            SetBodyRange(step == 1 ? 1 : step == 2 ? 3 : 5, step == 3 ? 5 : 2, false);
            shakeUntil = Time.unscaledTime + 0.14f;
            yield return new WaitForSecondsRealtime(step == 3 ? 0.45f : 0.2f);
            SetBodyFrame(0);
        }

        private void SetBodyFrame(int frame)
        {
            StopTrack(villainBody);
            if (villainFrames != null && frame < villainFrames.Length) villainBody.sprite = villainFrames[frame];
        }

        private void SetBodyRange(int first, int count, bool loop)
        {
            AddTrack(villainBody, villainFrames, first, count, 0.08f, loop);
        }

        private void SetPortrait(Sprite[] frames, int frame)
        {
            if (frames != null && frame >= 0 && frame < frames.Length) portrait.sprite = frames[frame];
        }

        private void SpawnDissolvePixels(bool maskOnly)
        {
            var colors = maskOnly
                ? new[] { new Color(0.91f, 0.86f, 0.78f), new Color(0.74f, 0.70f, 0.64f), new Color(0.14f, 0.14f, 0.14f) }
                : new[] { new Color(0.14f, 0.14f, 0.14f), new Color(0.22f, 0.22f, 0.22f), new Color(0.37f, 0.37f, 0.36f) };
            Vector2 basePosition = villainBody.rectTransform.anchoredPosition;
            float now = Time.unscaledTime;
            for (int i = 0; i < 84; i++)
            {
                var image = OverlayCanvas.CreateImage(stage, "Dissolving Pixel", colors[UnityEngine.Random.Range(0, colors.Length)], false);
                float size = UnityEngine.Random.Range(4f, 10f);
                Place(image.rectTransform, basePosition + new Vector2(UnityEngine.Random.Range(-38f, 38f), UnityEngine.Random.Range(-64f, 54f)), Vector2.one * size);
                flyingPixels.Add(new FlyingPixel
                {
                    image = image,
                    origin = image.rectTransform.anchoredPosition,
                    velocity = new Vector2(UnityEngine.Random.Range(20f, 100f), UnityEngine.Random.Range(80f, 180f)),
                    delay = UnityEngine.Random.Range(0f, 0.25f),
                    duration = maskOnly ? 0.7f : 1.3f,
                    born = now,
                });
            }
        }

        private IEnumerator SeparateBrothers()
        {
            splitMerged.sprite = aBloodyFrames.Length > 31 ? aBloodyFrames[31] : null;
            splitMerged.gameObject.SetActive(true);
            float flicker = 0.14f;
            for (float elapsed = 0f; elapsed < 0.9f; elapsed += flicker)
            {
                splitMerged.sprite = bIdle;
                yield return new WaitForSecondsRealtime(flicker);
                splitMerged.sprite = aBloodyFrames.Length > 31 ? aBloodyFrames[31] : null;
                flicker = Mathf.Lerp(0.14f, 0.06f, Mathf.Clamp01(elapsed / 0.9f));
            }

            flashOverlay.color = Color.white;
            yield return Fade(flashOverlay, 0f, 0.2f);
            splitMerged.gameObject.SetActive(false);
            splitB.sprite = bIdle;
            splitA.sprite = aBloodyFrames.Length > 1 ? aBloodyFrames[1] : null;
            splitB.gameObject.SetActive(true);
            splitA.gameObject.SetActive(true);
            yield return new WaitForSecondsRealtime(0.65f);
            yield return Say("A", "Anh ơi! Về thôi!", "Come on, big brother! Let's go home!", aPortraitFrames, 2, 4, 0.11f, new Color(0.58f, 0.83f, 0.87f));

            var debris = SpawnCollapseTiles();
            splitB.transform.SetAsLastSibling();
            splitA.transform.SetAsLastSibling();
            blackOverlay.transform.SetAsLastSibling();
            AddTrack(splitB, bWalk, 0, bWalk != null ? bWalk.Length : 0, 1f / 8f, true);
            AddTrack(splitA, aBloodyFrames, 7, 8, 0.1f, true);
            float start = Time.unscaledTime;
            float duration = 3.8f;
            while (Time.unscaledTime - start < duration)
            {
                float p = Mathf.Clamp01((Time.unscaledTime - start) / duration);
                float front = -20f + p * 200f;
                for (int i = 0; i < debris.Count; i++)
                {
                    var cell = debris[i];
                    if (cell.image && cell.image.rectTransform.anchoredPosition.x < front + UnityEngine.Random.Range(0f, 32f))
                        cell.image.gameObject.SetActive(false);
                }

                splitB.rectTransform.anchoredPosition = ArtPosition(28f + p * 150f, 53f);
                splitA.rectTransform.anchoredPosition = ArtPosition(48f + p * 146f - Mathf.Max(0f, 14f - p * 60f), 53f);
                blackOverlay.color = new Color(0f, 0f, 0f, p * 0.85f);
                if (UnityEngine.Random.value < 0.05f) shakeUntil = Time.unscaledTime + 0.08f;
                yield return null;
            }

            yield return Fade(blackOverlay, 1f, 0.6f);
            splitB.gameObject.SetActive(false);
            splitA.gameObject.SetActive(false);
            foreach (var cell in debris) if (cell.image) Destroy(cell.image.gameObject);
        }

        private List<FlyingPixel> SpawnCollapseTiles()
        {
            var blocks = new List<FlyingPixel>();
            for (int y = 0; y < 360; y += 16)
            {
                for (int x = 0; x < 640; x += 16)
                {
                    var image = OverlayCanvas.CreateImage(stage, "Collapsing White Pixel", Color.white, false);
                    Place(image.rectTransform, new Vector2(x + 8f - 320f, 180f - y - 8f), Vector2.one * 16f);
                    blocks.Add(new FlyingPixel { image = image });
                }
            }
            return blocks;
        }

        private IEnumerator HospitalEnding()
        {
            yield return new WaitForSecondsRealtime(0.2f);
            dialogueGroup.transform.SetAsLastSibling();
            for (int i = 0; i < 4; i++)
            {
                continueText.text = "▼";
                if (dialogueGroup) dialogueGroup.alpha = 1f;
                speakerText.text = "";
                portrait.enabled = false;
                portraitFrameBorder.enabled = false;
                lineText.text = i == 0 ? "bíp..." : i == 1 ? "bíp...  bíp.." : i == 2 ? "bíp.. bíp.. bíp." : "bíp. bíp. bíp. bíp.";
                yield return new WaitForSecondsRealtime(i == 0 ? 0.7f : i == 1 ? 0.5f : i == 2 ? 0.38f : 0.6f);
            }
            dialogueGroup.alpha = 0f;
            continueText.text = "▼";

            hospitalRoom.gameObject.SetActive(true);
            SetAtlasFrame(hospitalRoom, hospitalFrames, 0);
            kneelingA.gameObject.SetActive(true);
            SetAtlasFrame(kneelingA, kneelingFrames, 0);
            blackOverlay.color = Color.black;
            yield return Fade(blackOverlay, 0f, 1.4f);
            yield return new WaitForSecondsRealtime(1.5f);

            AddTrack(hospitalRoom, hospitalFrames, 10, 10, FrameSeconds, false);
            yield return new WaitForSecondsRealtime(1.05f);

            AddTrack(kneelingA, kneelingFrames, 2, 2, 0.12f, false);
            yield return new WaitForSecondsRealtime(0.28f);
            SetAtlasFrame(kneelingA, kneelingFrames, 4);
            yield return new WaitForSecondsRealtime(0.6f);
            AddTrack(kneelingA, kneelingFrames, 5, 4, 0.12f, true);
            yield return Say("A", "Anh...?", "Brother...?", aPortraitFrames, 2, 4, 0.11f, new Color(0.58f, 0.83f, 0.87f));
            SetAtlasFrame(kneelingA, kneelingFrames, 4);
            yield return Say("B", "Ừ. Anh về rồi.", "Yeah. I'm home.", villainPortraitFrames, 4, 1, 0.1f, new Color(0.95f, 0.32f, 0.35f));

            yield return new WaitForSecondsRealtime(1.1f);
            yield return Fade(whiteOverlay, 1f, 1.6f);
            theEndText.text = Localization.Get("ending.the_end");
            theEndText.transform.SetAsLastSibling();
            var endColor = theEndText.color;
            endColor.a = 1f;
            theEndText.color = endColor;
            yield return new WaitForSecondsRealtime(2.5f);
        }

        private IEnumerator Say(string speaker, string vietnamese, string english, Sprite[] portraitFrames, int portraitFrame,
            int portraitFrameCount, float portraitFrameSeconds, Color nameColor)
        {
            return Say(speaker, Localization.CurrentLanguage == ELanguage.Vietnamese ? vietnamese : english,
                portraitFrames, portraitFrame, portraitFrameCount, portraitFrameSeconds, nameColor);
        }

        private IEnumerator Say(string speaker, string line, Sprite[] portraitFrames, int portraitFrame,
            int portraitFrameCount, float portraitFrameSeconds, Color nameColor)
        {
            dialogueGroup.alpha = 1f;
            speakerText.text = speaker == "Villain" ? (Localization.CurrentLanguage == ELanguage.Vietnamese ? "Phản diện" : "The Villain") : speaker;
            speakerText.color = nameColor;
            lineText.text = "";
            portrait.enabled = true;
            portraitFrameBorder.enabled = true;
            SetPortrait(portraitFrames, portraitFrame);
            if (portraitFrameCount > 1) AddTrack(portrait, portraitFrames, portraitFrame, portraitFrameCount, portraitFrameSeconds, true);
            advanceRequested = false;
            string localized = line;
            for (int i = 0; i < localized.Length; i++)
            {
                if (advanceRequested)
                {
                    lineText.text = localized;
                    break;
                }
                lineText.text = localized.Substring(0, i + 1);
                yield return new WaitForSecondsRealtime(0.025f);
            }
            lineText.text = localized;
            advanceRequested = false;
            while (!advanceRequested) yield return null;
            dialogueGroup.alpha = 0f;
            StopTrack(portrait);
        }

        private IEnumerator Fade(Graphic graphic, float alpha, float duration)
        {
            if (!graphic) yield break;
            if (graphic == whiteOverlay || graphic == blackOverlay || graphic == flashOverlay)
                graphic.transform.SetAsLastSibling();
            Color color = graphic.color;
            float from = color.a;
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < duration)
            {
                float p = Mathf.Clamp01((Time.unscaledTime - started) / duration);
                color.a = Mathf.Lerp(from, alpha, p);
                graphic.color = color;
                yield return null;
            }
            color.a = alpha;
            graphic.color = color;
        }

        private void AddTrack(Image image, Sprite[] frames, int first, int count, float secondsPerFrame, bool loop)
        {
            StopTrack(image);
            if (!image || frames == null || first < 0 || first >= frames.Length || count <= 0) return;
            count = Mathf.Min(count, frames.Length - first);
            image.sprite = frames[first];
            tracks.Add(new FrameTrack
            {
                image = image,
                frames = frames,
                first = first,
                count = count,
                secondsPerFrame = Mathf.Max(0.01f, secondsPerFrame),
                started = Time.unscaledTime,
                loop = loop,
            });
        }

        private void StopTrack(Image image)
        {
            tracks.RemoveAll(track => track.image == image);
        }

        private void SetAtlasFrame(Image image, Sprite[] frames, int index)
        {
            StopTrack(image);
            if (image && frames != null && index >= 0 && index < frames.Length) image.sprite = frames[index];
        }
    }
}
