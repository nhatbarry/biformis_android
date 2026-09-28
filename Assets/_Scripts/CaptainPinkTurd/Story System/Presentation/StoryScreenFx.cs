using CaptainPinkTurd.Scene.Manager;
using CaptainPinkTurd.TopDownControllerSystem;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// Whole-screen story moments inside a level: fading in from white after a dream, B fainting at the end of
    /// Level 2, and the collapse when Level 5's clock runs out. Uses its own overlay and Volume, so the shared
    /// volume profile asset is never modified.
    /// </summary>
    public class StoryScreenFx : MonoBehaviour
    {
        [SerializeField] private LevelManager levelManager;
        [Tooltip("Level starts behind a white screen that fades away (e.g. waking up from the white room)")]
        [SerializeField] private bool startFromWhite;
        [SerializeField] private float startFadeSeconds = 1.5f;
        [Tooltip("-100 (grey) to 100. Below 0 gives a washed-out, dream-like look")]
        [SerializeField, Range(-100f, 100f)] private float baseSaturation;

        private Image overlay;
        private ColorAdjustments colorAdjustments;
        private bool busy;

        private void Awake()
        {
            var canvas = OverlayCanvas.Create(transform, "Story Screen Fx", 90);
            overlay = OverlayCanvas.CreateImage(canvas.transform, "Overlay", new Color(1f, 1f, 1f, startFromWhite ? 1f : 0f), true);

            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            colorAdjustments = profile.Add<ColorAdjustments>(true);
            colorAdjustments.saturation.value = baseSaturation;
            volume.profile = profile;
        }

        private void Start()
        {
            if (startFromWhite) overlay.DOFade(0f, startFadeSeconds).SetDelay(0.3f).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>
        /// Colour drains away, the screen goes white and the player can no longer move.
        /// </summary>
        public void Faint()
        {
            if (busy) return;
            busy = true;

            FreezePlayer();
            DOTween.To(() => colorAdjustments.saturation.value, v => colorAdjustments.saturation.value = v, -100f, 1.5f)
                .SetUpdate(true).SetLink(gameObject);
            overlay.color = new Color(1f, 1f, 1f, 0f);
            overlay.DOFade(1f, 2f).SetDelay(0.6f).SetUpdate(true).SetLink(gameObject);
        }

        /// <summary>
        /// Red flash into white, then the level restarts.
        /// </summary>
        public void CollapseAndRestart()
        {
            if (busy) return;
            busy = true;

            FreezePlayer();
            overlay.color = new Color(0.8f, 0.05f, 0.1f, 0f);
            DOTween.Sequence()
                .Append(overlay.DOFade(0.8f, 0.25f))
                .Append(overlay.DOColor(Color.white, 1f))
                .AppendInterval(0.3f)
                .OnComplete(levelManager.Restart)
                .SetUpdate(true)
                .SetLink(gameObject);
        }

        private static void FreezePlayer()
        {
            var movement = FindAnyObjectByType<PlayerFreeMovementTopDownController2D>();
            if (movement && movement.MovementEnabled) movement.ToggleMovement(false);
        }
    }
}
