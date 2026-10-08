using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.MobileControls
{
    /// <summary>
    /// Plays a <see cref="MobileControlsArt.Clip"/> on a HUD Image, in unscaled time (the game freezes the clock on
    /// pause and hit-stop, and a button that stops animating reads as broken). Asking for the clip that is already
    /// playing keeps it going, so the HUD can simply ask every frame for the state each control is in.
    /// The Aseprite importer trims every frame to its pixels, so each frame is sized to them and placed by its pivot
    /// (the art's canvas centre) on the centre of the parent, which keeps the control's full size for touches.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class HudSpriteAnimation : MonoBehaviour
    {
        private Image image;
        private float unitsPerPixel = 4f;
        private MobileControlsArt.Clip clip;
        private int frame;
        private float frameTime;

        public string CurrentClip => clip?.name;

        public void Configure(float canvasUnitsPerPixel) => unitsPerPixel = canvasUnitsPerPixel;

        public void Play(MobileControlsArt.Clip next)
        {
            if (next == null || next == clip) return;
            clip = next;
            frame = 0;
            frameTime = 0f;
            Show();
        }

        private void Update()
        {
            if (clip == null || clip.frames.Length < 2) return;

            frameTime += Time.unscaledDeltaTime;
            while (frameTime >= Duration(frame))
            {
                frameTime -= Duration(frame);
                if (frame + 1 < clip.frames.Length) frame++;
                else if (clip.loop) frame = 0;
                else
                {
                    frameTime = 0f;
                    break;
                }
            }
            Show();
        }

        private float Duration(int index) =>
            clip.durations != null && index < clip.durations.Length ? Mathf.Max(0.01f, clip.durations[index]) : 0.1f;

        private void Show()
        {
            if (!image) image = GetComponent<Image>();
            var sprite = clip.frames[frame];
            image.sprite = sprite;
            image.enabled = sprite;
            if (!sprite) return;

            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = sprite.pivot / sprite.rect.size;
            rect.sizeDelta = sprite.rect.size * unitsPerPixel;
            rect.anchoredPosition = Vector2.zero;
        }
    }
}
