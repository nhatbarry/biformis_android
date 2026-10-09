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
    /// The story's last scene: the heart monitor in the dark, the hospital room, A waking at B's bedside, THE END.
    /// What leads here (the boss's glass shatter, the talk in the dark, the run home through the levels and the door
    /// of light) is the ink knot "Ending" in the Story Cutscene scene. Artwork is exported from Scene_KetThuc.zip.
    /// </summary>
    public class EndingSequence : MonoBehaviour
    {
        [SerializeField] private StoryData storyData;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private AudioClip music;

        private const int Scale = 4;
        private const float FrameSeconds = 0.1f;

        private RectTransform stage;
        private Image whiteOverlay;
        private Image blackOverlay;
        private Image hospitalRoom;
        private Image kneelingA;
        private Image portrait;
        private Image portraitFrameBorder;
        private CanvasGroup dialogueGroup;
        private TMP_Text speakerText;
        private TMP_Text lineText;
        private TMP_Text continueText;

        private Sprite[] hospitalFrames;
        private Sprite[] kneelingFrames;

        private readonly List<FrameTrack> tracks = new();
        private readonly List<Sprite> runtimeSprites = new();
        private InputAction advanceAction;
        private bool advanceRequested;
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

        private IEnumerator Start()
        {
            if (music) MusicManager.Instance.Play(music, loop: true);
            LoadArt();
            BuildStage();

            //the cutscene before ends in white; this scene starts in the dark of the heart monitor
            blackOverlay.color = Color.black;
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
        }

        private void LoadArt()
        {
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

            whiteOverlay = OverlayCanvas.CreateImage(stage, "White Fade", Color.white, true);
            whiteOverlay.color = new Color(1f, 1f, 1f, 0f);
            blackOverlay = OverlayCanvas.CreateImage(stage, "Black Fade", Color.black, true);
            blackOverlay.color = new Color(0f, 0f, 0f, 0f);
            hospitalRoom = CreateImage(stage, "Hospital Room", Vector2.zero, new Vector2(640f, 360f));
            hospitalRoom.gameObject.SetActive(false);
            kneelingA = CreateImage(stage, "A at bedside", ArtPosition(100f, 40f), new Vector2(72f, 72f));
            kneelingA.gameObject.SetActive(false);

            BuildDialoguePanel(stage);
            whiteOverlay.transform.SetAsLastSibling();
            blackOverlay.transform.SetAsLastSibling();
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
            portrait.enabled = portraitFrameBorder.enabled = false;

            var textRoot = new GameObject("Dialogue Text", typeof(RectTransform));
            textRoot.transform.SetParent(root.transform, false);
            Place((RectTransform)textRoot.transform, new Vector2(-8f, -120f), new Vector2(550f, 90f));
            speakerText = CreateText(textRoot.transform, "Speaker", 22f, TextAlignmentOptions.TopLeft, new Color(0.57f, 0.85f, 0.8f, 1f));
            var speakerRect = speakerText.rectTransform;
            StretchAtTopLeft(speakerRect, Vector2.zero, new Vector2(550f, 26f));
            lineText = CreateText(textRoot.transform, "Line", 20f, TextAlignmentOptions.TopLeft, Color.white);
            StretchAtTopLeft(lineText.rectTransform, new Vector2(0f, -27f), new Vector2(550f, 64f));
            lineText.enableWordWrapping = true;
            lineText.overflowMode = TextOverflowModes.Truncate;
            continueText = CreateText(textRoot.transform, "Continue", 18f, TextAlignmentOptions.BottomRight, new Color(0.57f, 0.85f, 0.8f, 1f));
            StretchAtTopLeft(continueText.rectTransform, new Vector2(520f, -64f), new Vector2(30f, 24f));
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
            yield return Say("A", "Anh...?", "Brother...?", new Color(0.58f, 0.83f, 0.87f));
            SetAtlasFrame(kneelingA, kneelingFrames, 4);
            yield return Say("B", "Ừ. Anh về rồi.", "Yeah. I'm home.", new Color(0.95f, 0.32f, 0.35f));

            yield return new WaitForSecondsRealtime(1.1f);
            yield return Fade(whiteOverlay, 1f, 1.6f);
            theEndText.text = Localization.Get("ending.the_end");
            theEndText.transform.SetAsLastSibling();
            var endColor = theEndText.color;
            endColor.a = 1f;
            theEndText.color = endColor;
            yield return new WaitForSecondsRealtime(2.5f);
        }

        private IEnumerator Say(string speaker, string vietnamese, string english, Color nameColor)
        {
            string line = Localization.CurrentLanguage == ELanguage.Vietnamese ? vietnamese : english;
            dialogueGroup.alpha = 1f;
            speakerText.text = speaker == "Villain" ? (Localization.CurrentLanguage == ELanguage.Vietnamese ? "Phản diện" : "The Villain") : speaker;
            speakerText.color = nameColor;
            lineText.text = "";
            portrait.enabled = false;
            portraitFrameBorder.enabled = false;
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
            if (graphic == whiteOverlay || graphic == blackOverlay)
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
