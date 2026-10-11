#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.UI.Popup;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.MobileControls.Tests
{
    public class PauseMenuSettingsTests
    {
        private static readonly string[] PreferenceKeys = { "MasterVolume", "MusicVolume", "SFXVolume" };
        private readonly bool[] hadPreference = new bool[3];
        private readonly float[] savedPreferences = new float[3];
        private readonly float[] savedVolumes = new float[3];
        private AudioManager audio;
        private bool ownsAudio;
        private GameObject canvasObject;
        private GameObject pause;
        private GameObject eventSystemObject;
        private float originalTimeScale;
        private bool originalCheat;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalTimeScale = Time.timeScale;
            Time.timeScale = 1f;
            originalCheat = CheatSettings.Enabled;
            CheatSettings.Enabled = false;
            for (int i = 0; i < PreferenceKeys.Length; i++)
            {
                hadPreference[i] = PlayerPrefs.HasKey(PreferenceKeys[i]);
                savedPreferences[i] = PlayerPrefs.GetFloat(PreferenceKeys[i]);
            }

            audio = AudioManager.TryGetInstance();
            ownsAudio = !audio;
            if (ownsAudio)
            {
                var audioObject = new GameObject("Pause Test Audio");
                audioObject.SetActive(false);
                audio = audioObject.AddComponent<AudioManager>();
                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>("Assets/Audio/Main.mixer");
                Assert.IsNotNull(mixer, "the actual game mixer is required for audio binding checks");
                var fields = new SerializedObject(audio);
                fields.FindProperty("masterMixerGroup").objectReferenceValue = mixer.FindMatchingGroups("Master").First();
                fields.FindProperty("musicMixerGroup").objectReferenceValue = mixer.FindMatchingGroups("Music").First();
                fields.FindProperty("sfxMixerGroup").objectReferenceValue = mixer.FindMatchingGroups("SoundFX").First();
                fields.ApplyModifiedPropertiesWithoutUndo();
                audioObject.SetActive(true);
            }
            yield return null; // AudioManager loads the saved settings in Start.
            savedVolumes[0] = audio.GetMasterVolume();
            savedVolumes[1] = audio.GetMusicVolume();
            savedVolumes[2] = audio.GetSFXVolume();

            canvasObject = new GameObject("Pause Test Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(640f, 360f);
            scaler.referencePixelsPerUnit = 16f;
            scaler.matchWidthOrHeight = 0.5f;
            if (!EventSystem.current) eventSystemObject = new GameObject("Pause Test EventSystem", typeof(EventSystem));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/Pause Popup.prefab");
            Assert.IsNotNull(prefab);
            pause = Object.Instantiate(prefab, canvas.transform, false);
            yield return null;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(canvasObject);
            if (eventSystemObject) Object.DestroyImmediate(eventSystemObject);
            if (audio)
            {
                audio.SetVolume(EVolumeType.Master, savedVolumes[0]);
                audio.SetVolume(EVolumeType.Music, savedVolumes[1]);
                audio.SetVolume(EVolumeType.SFX, savedVolumes[2]);
                if (ownsAudio) Object.DestroyImmediate(audio.gameObject);
            }
            for (int i = 0; i < PreferenceKeys.Length; i++)
            {
                if (hadPreference[i]) PlayerPrefs.SetFloat(PreferenceKeys[i], savedPreferences[i]);
                else PlayerPrefs.DeleteKey(PreferenceKeys[i]);
            }
            CheatSettings.Enabled = originalCheat;
            Time.timeScale = originalTimeScale;
        }

        [UnityTest]
        public IEnumerator TheRealPausePrefabKeepsItsActionsAndFitsLandscapeScreens()
        {
            Assert.IsNotNull(pause.GetComponent<PauseMenuSettings>(), "every existing level uses this pause prefab");
            CollectionAssert.AreEquivalent(new[] { "Play Button", "Retry Button", "Home Button" },
                pause.GetComponentsInChildren<Button>().Select(button => button.name).ToArray());
            var volumes = pause.GetComponentsInChildren<VolumeSlider>();
            Assert.AreEqual(3, volumes.Length);
            CollectionAssert.AreEquivalent(new[] { EVolumeType.Master, EVolumeType.Music, EVolumeType.SFX },
                volumes.Select(volume => volume.VolumeType).ToArray());
            foreach (var volume in volumes)
            {
                var slider = volume.GetComponent<Slider>();
                Assert.IsNotNull(slider.handleRect.GetComponent<Image>().sprite, "main menu handle art missing");
                Assert.IsNotNull(slider.transform.Find("Background").GetComponent<Image>().sprite,
                    "main menu slider art missing");
            }
            var overlay = pause.transform.Find("Panel").GetComponent<RectTransform>();
            Assert.AreEqual(Vector2.zero, overlay.anchorMin);
            Assert.AreEqual(Vector2.one, overlay.anchorMax);
            Assert.AreEqual(Vector2.zero, overlay.offsetMin);
            Assert.AreEqual(Vector2.zero, overlay.offsetMax);

            yield return Capture(1280, 720, "pause-menu-settings-16-9.png");
            GetToggle().isOn = true;
            yield return Capture(1280, 576, "pause-menu-settings-20-9.png");
        }

        [UnityTest]
        public IEnumerator TheCheckboxControlsCheatAndRemembersItWhenPauseReopens()
        {
            var toggle = GetToggle();
            Assert.AreEqual("cheat", toggle.GetComponentInChildren<TMP_Text>().text);
            Assert.IsFalse(toggle.isOn);
            int changes = 0;
            System.Action<bool> listener = _ => changes++;
            CheatSettings.Changed += listener;
            try
            {
                ExecuteEvents.Execute(toggle.gameObject, new PointerEventData(EventSystem.current),
                    ExecuteEvents.pointerClickHandler);
                Assert.IsTrue(CheatSettings.Enabled);
                Assert.AreEqual(1, changes, "one click should publish exactly one settings change");
                int controlCount = pause.GetComponentsInChildren<Selectable>().Length;
                pause.SetActive(false);
                CheatSettings.Enabled = false;
                pause.SetActive(true);
                yield return null;
                Assert.IsFalse(toggle.isOn, "reopened pause must read the shared cheat setting");
                Assert.AreEqual(controlCount, pause.GetComponentsInChildren<Selectable>().Length,
                    "reopening must not duplicate controls or listeners");
                CheatSettings.Enabled = true;
                Assert.IsTrue(toggle.isOn, "external settings changes also update the visible checkbox");
            }
            finally
            {
                CheatSettings.Changed -= listener;
            }
        }

        [UnityTest]
        public IEnumerator AudioSlidersChangeOnlyTheirChannelAndRefreshOnReopenWhilePaused()
        {
            Time.timeScale = 0f;
            FindSlider(EVolumeType.Master).value = 0.41f;
            FindSlider(EVolumeType.Music).value = 0.27f;
            FindSlider(EVolumeType.SFX).value = 0.63f;
            Assert.AreEqual(0.41f, audio.GetMasterVolume(), 0.0001f);
            Assert.AreEqual(0.27f, audio.GetMusicVolume(), 0.0001f);
            Assert.AreEqual(0.63f, audio.GetSFXVolume(), 0.0001f);
            Assert.AreEqual(0.41f, PlayerPrefs.GetFloat("MasterVolume"), 0.0001f);
            Assert.AreEqual(0.27f, PlayerPrefs.GetFloat("MusicVolume"), 0.0001f);
            Assert.AreEqual(0.63f, PlayerPrefs.GetFloat("SFXVolume"), 0.0001f);

            pause.SetActive(false);
            audio.SetVolume(EVolumeType.Master, 0.72f);
            audio.SetVolume(EVolumeType.Music, 0.18f);
            audio.SetVolume(EVolumeType.SFX, 0.55f);
            pause.SetActive(true);
            yield return null;
            Assert.AreEqual(0.72f, FindSlider(EVolumeType.Master).value, 0.0001f);
            Assert.AreEqual(0.18f, FindSlider(EVolumeType.Music).value, 0.0001f);
            Assert.AreEqual(0.55f, FindSlider(EVolumeType.SFX).value, 0.0001f);
            Assert.AreEqual(0f, Time.timeScale, "audio UI must not resume gameplay");
        }

        private Toggle GetToggle() => pause.GetComponentInChildren<Toggle>();
        private Slider FindSlider(EVolumeType type) => pause.GetComponentsInChildren<VolumeSlider>()
            .Single(volume => volume.VolumeType == type).GetComponent<Slider>();

        private IEnumerator Capture(int width, int height, string filename)
        {
            var canvas = canvasObject.GetComponent<Canvas>();
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            var cameraObject = new GameObject("Pause Capture Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.2f, 0.23f);
            camera.cullingMask = 1 << 30;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var previousActive = RenderTexture.active;
            try
            {
                foreach (var child in canvas.GetComponentsInChildren<Transform>()) child.gameObject.layer = 30;
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                scaler.enabled = false;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                canvas.scaleFactor = Mathf.Sqrt((width / 640f) * (height / 360f));
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var canvasRect = canvas.GetComponent<RectTransform>();
                var pauseTitle = pause.transform.Find("Pause Contents/Title");
                var settingsTitle = pause.transform.Find("Pause Contents/Pause Settings/Title");
                Assert.AreEqual(canvasRect.InverseTransformPoint(pauseTitle.position).y,
                    canvasRect.InverseTransformPoint(settingsTitle.position).y, 0.05f,
                    "both panel headers should line up");
                var corners = new Vector3[4];
                foreach (var panelName in new[] { "Pause Contents/Pause Settings", "Pause Contents/Game Over Panel" })
                {
                    pause.transform.Find(panelName).GetComponent<RectTransform>().GetWorldCorners(corners);
                    foreach (var corner in corners)
                    {
                        var point = canvasRect.InverseTransformPoint(corner);
                        Assert.IsTrue(canvasRect.rect.Contains(point), $"{panelName} leaves the {width}x{height} viewport");
                    }
                }
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    image.Apply();
                    Directory.CreateDirectory("Logs");
                    File.WriteAllBytes(Path.Combine("Logs", filename), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
            finally
            {
                camera.targetTexture = null;
                canvas.worldCamera = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                scaler.enabled = true;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
#endif
