using System;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Core.Enum;
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
    /// The touch HUD: a floating joystick on the left; interact, dash and switch-form buttons on the bottom right;
    /// pause on the top right - the team's UI_Controls_Android art and layout (a 160 x 90 screen), in the colours of the
    /// player's current form (red = B, blue = A).
    /// </summary>
    /// <remarks>
    /// Nothing here talks to gameplay. Every control simulates a virtual Gamepad through Unity's
    /// <c>OnScreenControl</c> using the paths in <see cref="MobileControlPaths"/>, all of which are already
    /// bound to the same actions as the desktop keys, so touch input travels the exact same path as WASD,
    /// shift, E and escape do and the gameplay behaves identically. The interact button lights up while
    /// <see cref="InteractPrompt"/> says something is in reach (an open door, the box at the end of Level 4).
    ///
    /// The hierarchy is built in code so the HUD needs no prefab and no scene edits - the scene list and load
    /// order stay exactly as they were; its art is <see cref="MobileControlsArt"/>, loaded from Resources. It
    /// spawns itself on touch platforms (see <see cref="AutoSpawn"/>), but if you want to tune it in the
    /// inspector you can drop this component on a GameObject in the Core scene instead and the auto-spawn will
    /// step aside.
    /// </remarks>
    [DisallowMultipleComponent]
    public class MobileControlsHUD : MonoBehaviour, IGameEventSOListener<EColor>
    {
        private static MobileControlsHUD instance;

        //the art's sizes, in its own pixels
        private const float StickSize = 32f, KnobSize = 16f, ButtonSize = 24f, PauseSize = 12f;

        [Header("Canvas")]
        [Tooltip("Just under the Core scene's UI canvas, so the loading overlay still covers the controls.")]
        [SerializeField] private int sortingOrder = 9998;
        [Tooltip("The art's screen height in pixels: each art pixel is the whole number of screen pixels nearest " +
                 "to Screen.height / this, so the pixel art stays crisp.")]
        [SerializeField] private float artScreenHeight = 90f;

        [Header("Joystick (left part of the screen; art pixels)")]
        [Tooltip("Fraction of the screen width, from the left edge, where a finger can summon the stick.")]
        [Range(0.2f, 0.7f)]
        [SerializeField] private float zoneWidth = 0.45f;
        [Tooltip("Where the stick waits while nothing touches it: its centre from the bottom-left corner.")]
        [SerializeField] private Vector2 stickRest = new(22f, 20f);
        [Tooltip("How far the knob travels before the stick reads as fully deflected.")]
        [SerializeField] private float movementRange = 9f;
        [Range(0f, 0.9f)]
        [SerializeField] private float deadZone = 0.2f;

        [Header("Buttons (centres in art pixels, from their corner)")]
        [Tooltip("The E key: goes through an open door, takes the box... Lights up while something is in reach.")]
        [SerializeField] private Vector2 interactPosition = new(-44f, 14f);
        [Tooltip("Sprint while held, dash when tapped - the left shift key on desktop, which drives both.")]
        [SerializeField] private Vector2 runPosition = new(-18f, 20f);
        [Tooltip("Switch form - the J key on desktop.")]
        [SerializeField] private Vector2 dimensionPosition = new(-16f, 48f);
        [Tooltip("From the top-right corner.")]
        [SerializeField] private Vector2 pausePosition = new(-10f, -10f);

        [Header("Switching form")]
        [Tooltip("The switch button's to_blue / to_red turn (3 frames x 80 ms)")]
        [SerializeField] private float switchAnimationTime = 0.24f;
        [Tooltip("The rest of the controls change colour halfway through the turn")]
        [SerializeField] private float themeSwitchDelay = 0.12f;

        private MobileControlsArt art;
        private GameObject canvasRoot;
        private CanvasScaler scaler;
        private int screenHeight;

        /// <summary>Everything except pause, so the pause menu is not fighting a joystick for touches.</summary>
        private CanvasGroup gameplayControls;

        private FloatingOnScreenStick stick;
        private HudSpriteAnimation stickBaseArt, stickKnobArt;
        private Control interact, run, dimension, pause;

        private PopupManager popupManager;
        private bool menuOpen;

        private EColor form = EColor.Red;   //the form the game is in
        private EColor theme = EColor.Red;  //the colours the controls show
        private float switchStarted = float.NegativeInfinity;
        private float shownAt = float.NegativeInfinity;
        private bool inCutscene;

        private struct Control
        {
            public GameObject root;
            public MobileHudButton press;
            public HudSpriteAnimation art;
            public bool Pressed => press && press.IsPressed;
        }

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

            art = MobileControlsArt.Load();
            if (!art)
            {
                Debug.LogError($"Mobile controls: no {nameof(MobileControlsArt)} at Resources/{MobileControlsArt.ResourcePath}");
                return;
            }
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
            InteractPrompt.CutsceneControlsChanged += RefreshVisibility;
            if (art && art.onDimensionChange) art.onDimensionChange.Subscribe(this);
            RefreshVisibility();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            InteractPrompt.CutsceneControlsChanged -= RefreshVisibility;
            if (art && art.onDimensionChange) art.onDimensionChange.Unsubscribe(this);
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode) => RefreshVisibility();

        private void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) => RefreshVisibility();

        /// <summary>The game changed form (also raised as each level loads, and by Level 4's lock to A).</summary>
        public void OnEventRaised(EColor data)
        {
            if (data == form) return;
            form = data;
            //a switch while playing turns the button; as a level starts (Level 4 locks A in) the controls just take the colours
            bool playing = canvasRoot && canvasRoot.activeInHierarchy && !inCutscene && Time.unscaledTime - shownAt > 0.5f;
            if (playing) switchStarted = Time.unscaledTime;
            else theme = form;
        }

        private void Update()
        {
            if (!canvasRoot || !canvasRoot.activeSelf) return;
            KeepPixelsWhole();

            // Ask the popup system directly rather than watching Time.timeScale. A frozen clock is not the
            // same thing as an open menu: HitStop zeroes the time scale for a moment on every hit, and
            // treating that as a pause used to yank the controls out from under the player's thumb.
            bool open = popupManager && popupManager.AnyPopupShowing();
            if (open != menuOpen)
            {
                menuOpen = open;
                // Faded and untouchable rather than deactivated: switching a held joystick off loses the finger
                // that is on it, and the player would have to lift and press again to get moving.
                gameplayControls.alpha = open ? 0f : 1f;
                gameplayControls.blocksRaycasts = !open;
            }

            ShowStates();
        }

        /// <summary>Picks every control's clip from its state, in the current theme.</summary>
        private void ShowStates()
        {
            float sinceSwitch = Time.unscaledTime - switchStarted;
            bool turning = sinceSwitch < switchAnimationTime;
            if (inCutscene) theme = InteractPrompt.CutsceneTheme;
            else if (sinceSwitch >= themeSwitchDelay) theme = form;
            string t = theme == EColor.Red ? "red_" : "blue_";

            bool held = stick && stick.IsHeld;
            stickBaseArt.Play(art.joystickBase.Find(t + (held ? "active" : "idle")));
            stickKnobArt.Play(art.joystickKnob.Find(t + (held ? "pressed" : "idle")));

            interact.art.Play(art.interact.Find(t + (interact.Pressed ? "pressed" : InteractPrompt.Available ? "ready" : "disabled")));
            run.art.Play(art.dash.Find(t + (run.Pressed ? "pressed" : "idle")));
            dimension.art.Play(turning
                ? art.swap.Find(form == EColor.Blue ? "to_blue" : "to_red")
                : art.swap.Find(t + (dimension.Pressed ? "pressed" : "idle")));
            pause.art.Play(art.pause.Find(t + (menuOpen ? "play" : "pause") + (pause.Pressed ? "_pressed" : "")));
        }

        /// <summary>
        /// Shows the controls while a gameplay level is loaded, and in a cutscene that asks for them (walking B up to
        /// A at the end of Level 4: only the joystick and the interact button then). Hiding deactivates the canvas,
        /// which releases the virtual gamepad and zeroes the stick - the player can never be left walking into a
        /// scene transition.
        /// </summary>
        private void RefreshVisibility()
        {
            if (!canvasRoot) return;

            // The Popup Manager prefab lives in each level scene, so it is a different instance every level.
            popupManager = FindAnyObjectByType<PopupManager>();

            bool level = IsGameplaySceneLoaded();
            inCutscene = !level && InteractPrompt.CutsceneControls;
            run.root.SetActive(!inCutscene);
            dimension.root.SetActive(!inCutscene);
            pause.root.SetActive(!inCutscene);

            bool show = level || inCutscene;
            if (canvasRoot.activeSelf == show) return;
            canvasRoot.SetActive(show);
            if (show) shownAt = Time.unscaledTime;
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

        /// <summary>
        /// One art pixel is a whole number of screen pixels (Screen.height / 90 rounded), as the team asked, so the
        /// pixel art never comes out with uneven pixels. The canvas is then about 360 units tall on any phone.
        /// </summary>
        private void KeepPixelsWhole()
        {
            if (Screen.height == screenHeight) return;
            screenHeight = Screen.height;
            float screenPixelsPerArtPixel = Mathf.Max(1f, Mathf.Round(screenHeight / artScreenHeight));
            scaler.scaleFactor = screenPixelsPerArtPixel / art.unitsPerPixel;
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
            canvas.pixelPerfect = true;

            scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            KeepPixelsWhole();

            var safeArea = CreateChild("Safe Area", canvasRoot.transform);
            Stretch(safeArea);
            safeArea.gameObject.AddComponent<SafeAreaFitter>();

            var gameplay = CreateChild("Gameplay Controls", safeArea);
            Stretch(gameplay);
            gameplayControls = gameplay.gameObject.AddComponent<CanvasGroup>();

            BuildStick(gameplay);

            interact = BuildButton(gameplay, "Interact", MobileControlPaths.Interact, ButtonSize,
                interactPosition, new Vector2(1f, 0f));
            run = BuildButton(gameplay, "Run And Dash", MobileControlPaths.RunAndDash, ButtonSize,
                runPosition, new Vector2(1f, 0f));
            dimension = BuildButton(gameplay, "Switch Dimension", MobileControlPaths.SwitchDimension, ButtonSize,
                dimensionPosition, new Vector2(1f, 0f));

            // Outside the gameplay group: it has to stay reachable while the pause menu is up.
            pause = BuildButton(safeArea, "Pause", MobileControlPaths.Pause, PauseSize,
                pausePosition, new Vector2(1f, 1f));

            ShowStates();
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

            float unit = art.unitsPerPixel;
            var stickBase = CreateChild("Stick Base", zone);
            Centre(stickBase, StickSize * unit);
            stickBaseArt = AddArt(CreateChild("Art", stickBase));

            var knob = CreateChild("Stick Knob", stickBase);
            Centre(knob, KnobSize * unit);
            stickKnobArt = AddArt(CreateChild("Art", knob));

            stick = zone.gameObject.AddComponent<FloatingOnScreenStick>();
            stick.Configure(stickBase, knob, MobileControlPaths.Move, movementRange * unit, deadZone, stickRest * unit);
        }

        /// <summary>One button: a full-size touch area with the animated art inside it.</summary>
        private Control BuildButton(RectTransform parent, string name, string controlPath, float sizeInPixels,
            Vector2 positionInPixels, Vector2 corner)
        {
            float unit = art.unitsPerPixel;
            var button = CreateChild(name, parent);
            button.anchorMin = button.anchorMax = corner;
            button.pivot = new Vector2(0.5f, 0.5f);
            button.sizeDelta = Vector2.one * sizeInPixels * unit;
            button.anchoredPosition = positionInPixels * unit;

            var touchArea = button.gameObject.AddComponent<Image>();
            touchArea.color = Color.clear;
            touchArea.raycastTarget = true;

            var artRect = CreateChild("Art", button);
            var control = new Control { root = button.gameObject, art = AddArt(artRect) };

            button.gameObject.AddComponent<OnScreenButton>().controlPath = controlPath;
            control.press = button.gameObject.AddComponent<MobileHudButton>();
            return control;
        }

        private HudSpriteAnimation AddArt(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            var animation = rect.gameObject.AddComponent<HudSpriteAnimation>();
            animation.Configure(art.unitsPerPixel);
            return animation;
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

        #endregion
    }
}
