using System;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.Scene;
using CaptainPinkTurd.UI.Popup;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CaptainPinkTurd.MobileControls
{
    /// <summary>
    /// The touch HUD: a floating joystick on the left, the action buttons on the bottom right, and pause on
    /// the top right.
    /// </summary>
    /// <remarks>
    /// Nothing here talks to gameplay. Every control simulates a virtual Gamepad through Unity's
    /// <c>OnScreenControl</c> using the paths in <see cref="MobileControlPaths"/>, all of which are already
    /// bound to the same actions as the desktop keys, so touch input travels the exact same path as WASD,
    /// shift and escape do and the gameplay behaves identically.
    ///
    /// The whole hierarchy is built in code so the HUD needs no prefab, no sprites and no scene edits - the
    /// scene list and load order stay exactly as they were. It spawns itself on touch platforms (see
    /// <see cref="AutoSpawn"/>), but if you want to tune it in the inspector you can drop this component on a
    /// GameObject in the Core scene instead and the auto-spawn will step aside.
    /// </remarks>
    [DisallowMultipleComponent]
    public class MobileControlsHUD : MonoBehaviour
    {
        private static MobileControlsHUD instance;

        [Header("Canvas")]
        [Tooltip("Matches the game's own UI canvas so the controls scale with the rest of the HUD.")]
        [SerializeField] private Vector2 referenceResolution = new(640f, 360f);
        [Tooltip("Just under the Core scene's UI canvas, so the loading overlay still covers the controls.")]
        [SerializeField] private int sortingOrder = 9998;

        [Header("Joystick (left half)")]
        [Tooltip("Fraction of the screen width, from the left edge, where a finger can summon the stick.")]
        [Range(0.2f, 0.7f)]
        [SerializeField] private float zoneWidth = 0.45f;
        [SerializeField] private float baseSize = 104f;
        [SerializeField] private float knobSize = 46f;
        [SerializeField] private float movementRange = 34f;
        [Range(0f, 0.9f)]
        [SerializeField] private float deadZone = 0.15f;

        [Header("Buttons (bottom right, offsets from that corner)")]
        [Tooltip("Switch dimension - the J key on desktop. Sits closest to the corner because it is the " +
                 "button the thumb reaches for most.")]
        [SerializeField] private float dimensionSize = 86f;
        [SerializeField] private Vector2 dimensionPosition = new(-64f, 56f);

        [Tooltip("Sprint while held, dash when tapped - the left shift key on desktop, which drives both.")]
        [SerializeField] private float runSize = 70f;
        [SerializeField] private Vector2 runPosition = new(-154f, 108f);

        [Tooltip("The E key. Off by default: no scene in the build list drives the Interact action yet " +
                 "(doors open by walking into them), so the button would sit there doing nothing. " +
                 "Turn it on the moment something reads Interact.")]
        [SerializeField] private bool showInteractButton;
        [SerializeField] private float interactSize = 66f;
        [SerializeField] private Vector2 interactPosition = new(-64f, 156f);

        [Header("Pause (top right, offset from that corner)")]
        [SerializeField] private float pauseSize = 58f;
        [SerializeField] private Vector2 pausePosition = new(-48f, -42f);

        [Header("Look")]
        [Range(0f, 1f)]
        [SerializeField] private float restAlpha = 0.55f;
        [Range(0f, 1f)]
        [SerializeField] private float pressedAlpha = 1f;
        [SerializeField] private Color dimensionRed = new(0.90f, 0.26f, 0.33f, 1f);
        [SerializeField] private Color dimensionBlue = new(0.28f, 0.55f, 0.94f, 1f);

        private GameObject canvasRoot;

        /// <summary>Everything except pause, so the pause menu is not fighting a joystick for touches.</summary>
        private CanvasGroup gameplayControls;

        private PopupManager popupManager;
        private Image pauseIcon;
        private Sprite pauseGlyph;
        private Sprite resumeGlyph;
        private bool wasMenuOpen;

        /// <summary>
        /// Creates the HUD on touch platforms without anyone having to place it in a scene. Runs after the
        /// first scene (Core) has loaded, so it lives alongside the managers that are already there.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (!TouchControlsWanted()) return;
            if (instance) return;
            if (FindAnyObjectByType<MobileControlsHUD>(FindObjectsInactive.Include)) return;

            new GameObject("Mobile Controls HUD").AddComponent<MobileControlsHUD>();
        }

        private static bool TouchControlsWanted()
        {
#if UNITY_ANDROID || UNITY_IOS
            // Also true in the editor once the build target is Android, so the HUD can be tested in play mode.
            return true;
#else
            return Application.isMobilePlatform;
#endif
        }

        private void Awake()
        {
            if (instance && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            // Only when we spawned ourselves - a HUD authored into the Core scene is already persistent.
            if (!transform.parent) DontDestroyOnLoad(gameObject);

            Build();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            RefreshVisibility();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode) => RefreshVisibility();

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) => RefreshVisibility();

        private void Update()
        {
            // Ask the popup system directly rather than watching Time.timeScale. A frozen clock is not the
            // same thing as an open menu: HitStop zeroes the time scale for a moment on every hit, and
            // treating that as a pause used to yank the controls out from under the player's thumb.
            bool menuOpen = popupManager && popupManager.AnyPopupShowing();
            if (menuOpen == wasMenuOpen) return;

            wasMenuOpen = menuOpen;

            // Faded and untouchable rather than deactivated: switching a held joystick off loses the finger
            // that is on it, and the player would have to lift and press again to get moving.
            if (gameplayControls)
            {
                gameplayControls.alpha = menuOpen ? 0f : 1f;
                gameplayControls.blocksRaycasts = !menuOpen;
            }

            if (pauseIcon) pauseIcon.sprite = menuOpen ? resumeGlyph : pauseGlyph;
        }

        /// <summary>
        /// Shows the controls only while a gameplay level is loaded, so they stay out of the main menu.
        /// Hiding deactivates the canvas, which releases the virtual gamepad and zeroes the stick - the player
        /// can never be left walking into a scene transition.
        /// </summary>
        private void RefreshVisibility()
        {
            if (!canvasRoot) return;

            // The Popup Manager prefab lives in each level scene, so it is a different instance every level.
            popupManager = FindAnyObjectByType<PopupManager>();

            bool show = IsGameplaySceneLoaded();
            if (canvasRoot.activeSelf != show) canvasRoot.SetActive(show);
        }

        private static bool IsGameplaySceneLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name.StartsWith(SceneDatabase.Scenes.Level, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        #region Construction

        private void Build()
        {
            canvasRoot = new GameObject("Touch HUD Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasRoot.transform.SetParent(transform, false);

            // Built inactive: the on-screen controls create their virtual device in OnEnable, and they must be
            // fully wired up before that happens. RefreshVisibility turns it on at the end.
            canvasRoot.SetActive(false);

            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var safeArea = CreateChild("Safe Area", canvasRoot.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            var gameplay = CreateChild("Gameplay Controls", safeArea);
            Stretch(gameplay);
            gameplayControls = gameplay.gameObject.AddComponent<CanvasGroup>();

            BuildStick(gameplay);

            BuildButton(gameplay, "Switch Dimension", MobileControlPaths.SwitchDimension, dimensionSize,
                dimensionPosition, new Vector2(1f, 0f),
                MobileControlGraphics.SplitDisc(dimensionRed, dimensionBlue, Color.white, 0.09f),
                icon: null, iconScale: 0f);

            BuildButton(gameplay, "Run And Dash", MobileControlPaths.RunAndDash, runSize, runPosition,
                new Vector2(1f, 0f),
                MobileControlGraphics.Disc(new Color(1f, 1f, 1f, 0.30f), Color.white, 0.09f),
                MobileControlGraphics.Chevrons(Color.white), iconScale: 0.62f);

            if (showInteractButton)
            {
                BuildButton(gameplay, "Interact", MobileControlPaths.Interact, interactSize, interactPosition,
                    new Vector2(1f, 0f),
                    MobileControlGraphics.Disc(new Color(1f, 1f, 1f, 0.30f), Color.white, 0.09f),
                    MobileControlGraphics.Disc(Color.white, Color.white, 0f), iconScale: 0.34f);
            }

            pauseGlyph = MobileControlGraphics.Bars(Color.white);
            resumeGlyph = MobileControlGraphics.Triangle(Color.white);

            // Outside the gameplay group: it has to stay reachable while the pause menu is up.
            pauseIcon = BuildButton(safeArea, "Pause", MobileControlPaths.Pause, pauseSize, pausePosition,
                new Vector2(1f, 1f),
                MobileControlGraphics.Disc(new Color(1f, 1f, 1f, 0.30f), Color.white, 0.09f),
                pauseGlyph, iconScale: 0.46f);

            RefreshVisibility();
        }

        private void BuildStick(RectTransform parent)
        {
            var zone = CreateChild("Move Zone", parent);
            zone.anchorMin = Vector2.zero;
            zone.anchorMax = new Vector2(zoneWidth, 1f);
            zone.pivot = new Vector2(0.5f, 0.5f);
            zone.offsetMin = Vector2.zero;
            zone.offsetMax = Vector2.zero;

            // An invisible graphic that still takes raycasts: this is what makes the whole zone touchable.
            var catcher = zone.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            catcher.raycastTarget = true;

            var stickBase = CreateChild("Stick Base", zone);
            Centre(stickBase, baseSize);
            AddImage(stickBase, MobileControlGraphics.Disc(new Color(1f, 1f, 1f, 0.22f), Color.white, 0.08f),
                restAlpha);

            var knob = CreateChild("Stick Knob", stickBase);
            Centre(knob, knobSize);
            AddImage(knob, MobileControlGraphics.Disc(new Color(1f, 1f, 1f, 0.85f), Color.white, 0.12f),
                pressedAlpha);

            stickBase.gameObject.SetActive(false);

            var stick = zone.gameObject.AddComponent<FloatingOnScreenStick>();
            stick.Configure(stickBase, knob, MobileControlPaths.Move, movementRange, deadZone);
        }

        /// <summary>Builds one round button and returns its icon, for callers that want to swap the glyph.</summary>
        private Image BuildButton(RectTransform parent, string name, string controlPath, float size,
            Vector2 position, Vector2 corner, Sprite background, Sprite icon, float iconScale)
        {
            var button = CreateChild(name, parent);
            button.anchorMin = button.anchorMax = corner;
            button.pivot = new Vector2(0.5f, 0.5f);
            button.sizeDelta = new Vector2(size, size);
            button.anchoredPosition = position;

            var backgroundImage = AddImage(button, background, restAlpha);
            backgroundImage.raycastTarget = true;

            Image iconImage = null;
            if (icon)
            {
                var iconRect = CreateChild("Icon", button);
                Centre(iconRect, size * iconScale);
                iconImage = AddImage(iconRect, icon, restAlpha);
            }

            var tinted = iconImage
                ? new Graphic[] { backgroundImage, iconImage }
                : new Graphic[] { backgroundImage };

            button.gameObject.AddComponent<OnScreenButton>().controlPath = controlPath;
            button.gameObject.AddComponent<MobileHudButton>().Configure(tinted, restAlpha, pressedAlpha);

            return iconImage;
        }

        private static RectTransform CreateChild(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Centre(RectTransform rect, float size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = Vector2.zero;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, float alpha)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
            return image;
        }

        #endregion
    }
}
