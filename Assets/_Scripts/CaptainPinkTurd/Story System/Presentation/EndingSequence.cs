using System;
using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.Localization;
using CaptainPinkTurd.InkDialogue;
using CaptainPinkTurd.Scene;
using CaptainPinkTurd.Scene.Story;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// After the door of light: the Red Final reunion, a white dissolve, the hospital and THE END.
    /// Every spoken line uses the story's Ink and the same dialogue prefab as the preceding cutscenes.
    /// </summary>
    public class EndingSequence : MonoBehaviour
    {
        [SerializeField] private StoryData storyData;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private AudioClip music;
        [SerializeField] private GameObject dialogueSystemPrefab;
        [SerializeField] private SoundData clappingSfx;
        [SerializeField] private SoundData crowdCheeringSfx;
        [SerializeField] private SerializeKeyValuePair<string, SoundData>[] omedetouSounds;
        
        private const int Scale = 4;
        private const float FrameSeconds = 0.1f;

        private RectTransform stage;
        private Image whiteOverlay;
        private Image hospitalRoom;
        private Image kneelingA;
        private TMP_Text theEndText;
        private DialogueManager dialogueManager;
        private GameObject dialogueSystemInstance;
        private Sprite[] hospitalFrames;
        private Sprite[] kneelingFrames;
        private readonly List<FrameTrack> tracks = new();
        private readonly List<Sprite> runtimeSprites = new();

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
            if (music) MusicManager.Instance.Play(music);
            SoundManager.Instance.CreateSoundBuilder().Play(crowdCheeringSfx);
            LoadArt();
            BuildStage();
            if (!DialogueManager.HasInstance)
            {
                if (!dialogueSystemPrefab) throw new InvalidOperationException("The ending needs the shared Story Dialogue System prefab.");
                dialogueSystemInstance = Instantiate(dialogueSystemPrefab, transform);
            }
            dialogueManager = DialogueManager.Instance;

            var redFinal = gameObject.AddComponent<RedFinalCutscene>();
            redFinal.Initialize(stage);
            redFinal.SpeakerChanged += ShowRedFinalDialogue;
            //Core's loading overlay draws above this canvas. Preserve the first flash until it has gone.
            var sceneController = SceneController.TryGetInstance();
            while (sceneController && sceneController.IsBusy) yield return null;
            yield return redFinal.Play();
            redFinal.SpeakerChanged -= ShowRedFinalDialogue;

            //Keep the last Red picture behind the white dissolve. The hospital appears only under full white.
            yield return Fade(whiteOverlay, 1f, 1.6f);
            redFinal.Hide();
            Destroy(redFinal);
            yield return HospitalEnding();
            if (StoryFlow.IsStoryRunning) StoryFlow.Advance(storyData);
        }

        private void OnDestroy()
        {
            if (dialogueManager && dialogueManager.DialogueIsPlaying) dialogueManager.ExitDialogue();
            foreach (var sprite in runtimeSprites) if (sprite) Destroy(sprite);
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
            if (!texture) throw new InvalidOperationException($"Story ending artwork is missing: {name}");
            var sprites = new Sprite[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                int y = texture.height - (i / columns + 1) * frameHeight;
                var sprite = Sprite.Create(texture, new Rect(i % columns * frameWidth, y, frameWidth, frameHeight),
                    new Vector2(0.5f, 0.5f), 16f, 0, SpriteMeshType.FullRect);
                sprite.name = $"{name}_{i:00}";
                runtimeSprites.Add(sprite);
                sprites[i] = sprite;
            }
            return sprites;
        }

        private void BuildStage()
        {
            var canvas = OverlayCanvas.Create(transform, "Ending Canvas", 10);
            stage = (RectTransform)new GameObject("Stage", typeof(RectTransform)).transform;
            stage.SetParent(canvas.transform, false);
            OverlayCanvas.Stretch(stage);
            OverlayCanvas.CreateImage(stage, "Background", Color.white, true);
            hospitalRoom = CreateImage(stage, "Hospital Room", Vector2.zero, new Vector2(640f, 360f));
            hospitalRoom.gameObject.SetActive(false);
            kneelingA = CreateImage(stage, "A at bedside", ArtPosition(100f, 40f), new Vector2(72f, 72f));
            kneelingA.gameObject.SetActive(false);
            whiteOverlay = OverlayCanvas.CreateImage(stage, "White Fade", new Color(1f, 1f, 1f, 0f), true);

            var endObject = new GameObject("The End", typeof(RectTransform));
            endObject.transform.SetParent(stage, false);
            theEndText = endObject.AddComponent<TextMeshProUGUI>();
            if (font) theEndText.font = font;
            theEndText.fontSize = 64f;
            theEndText.alignment = TextAlignmentOptions.Center;
            theEndText.color = new Color(0.92f, 0.17f, 0.29f, 0f);
            theEndText.raycastTarget = false;
            OverlayCanvas.Stretch(theEndText.rectTransform);
        }

        private static Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var image = OverlayCanvas.CreateImage(parent, name, Color.white, false);
            var rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            image.preserveAspect = true;
            return image;
        }

        private static Vector2 ArtPosition(float x, float y) => new((x - 80f) * Scale, (45f - y) * Scale);

        private IEnumerator HospitalEnding()
        {
            hospitalRoom.gameObject.SetActive(true);
            SetAtlasFrame(hospitalRoom, hospitalFrames, 0);
            kneelingA.gameObject.SetActive(true);
            SetAtlasFrame(kneelingA, kneelingFrames, 0);
            yield return Fade(whiteOverlay, 0f, 1.4f);
            yield return new WaitForSecondsRealtime(1.5f);
            AddTrack(hospitalRoom, hospitalFrames, 10, 10, FrameSeconds, false);
            yield return new WaitForSecondsRealtime(1.05f);
            AddTrack(kneelingA, kneelingFrames, 2, 2, 0.12f, false);
            yield return new WaitForSecondsRealtime(0.28f);
            SetAtlasFrame(kneelingA, kneelingFrames, 4);
            yield return new WaitForSecondsRealtime(0.6f);
            AddTrack(kneelingA, kneelingFrames, 5, 4, 0.12f, true);
            yield return PlayHospitalDialogue("HospitalEnding_A");
            SetAtlasFrame(kneelingA, kneelingFrames, 4);
            yield return PlayHospitalDialogue("HospitalEnding_B");
            yield return new WaitForSecondsRealtime(1.1f);
            yield return Fade(whiteOverlay, 1f, 1.6f);
            theEndText.text = Localization.Get("ending.the_end");
            theEndText.transform.SetAsLastSibling();
            var color = theEndText.color;
            color.a = 1f;
            theEndText.color = color;
            yield return new WaitForSecondsRealtime(2.5f);
        }

        private void ShowRedFinalDialogue(string actorId)
        {
            if (dialogueManager.DialogueIsPlaying) dialogueManager.ExitDialogue();
            
            string knot = actorId switch
            {
                "blue" => "RedFinal_Blue",
                "mom" => "RedFinal_Mom",
                "doctor" => "RedFinal_Doctor",
                "green" => "RedFinal_Zoro",
                "bunny" => "RedFinal_Usagi",
                "reaper" => "RedFinal_Reaper",
                "teen" => "RedFinal_Teen",
                "pink" => "RedFinal_Pink",
                "red" => "RedFinal_Thanks",
                _ => null,
            };

            if (actorId == "blue")
            {
                SoundManager.Instance.CreateSoundBuilder().Play(clappingSfx);
            }
            else if (actorId == "red")
            {
                SoundManager.Instance.StopAll();
            }
            if (knot != null)
            {
                if(omedetouSounds.TryGetValue(actorId, out var sound)) 
                    SoundManager.Instance.CreateSoundBuilder().Play(sound);
                
                dialogueManager.EnterDialogue(knot);
            }
        }

        private IEnumerator PlayHospitalDialogue(string knot)
        {
            dialogueManager.EnterDialogue(knot);
            while (dialogueManager.DialogueIsPlaying) yield return null;
        }

        private IEnumerator Fade(Graphic graphic, float alpha, float duration)
        {
            graphic.transform.SetAsLastSibling();
            var color = graphic.color;
            float from = color.a;
            float started = Time.unscaledTime;
            while (Time.unscaledTime - started < duration)
            {
                color.a = Mathf.Lerp(from, alpha, Mathf.Clamp01((Time.unscaledTime - started) / duration));
                graphic.color = color;
                yield return null;
            }
            color.a = alpha;
            graphic.color = color;
        }

        private void AddTrack(Image image, Sprite[] frames, int first, int count, float secondsPerFrame, bool loop)
        {
            tracks.RemoveAll(track => track.image == image);
            image.sprite = frames[first];
            tracks.Add(new FrameTrack { image = image, frames = frames, first = first, count = count,
                secondsPerFrame = secondsPerFrame, started = Time.unscaledTime, loop = loop });
        }

        private void SetAtlasFrame(Image image, Sprite[] frames, int index)
        {
            tracks.RemoveAll(track => track.image == image);
            image.sprite = frames[index];
        }
    }
}
