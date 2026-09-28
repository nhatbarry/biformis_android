using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// Flips through sprite frames on a UI Image, for characters shown outside gameplay (cutscenes, ending).
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SpriteFrameAnimator : MonoBehaviour
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float framesPerSecond = 8f;

        private Image image;
        private float time;

        private void Awake()
        {
            image = GetComponent<Image>();
        }

        public void Play(Sprite[] newFrames, float fps)
        {
            frames = newFrames;
            framesPerSecond = fps;
            time = 0f;
            ShowFrame();
        }

        private void Update()
        {
            if (frames == null || frames.Length == 0) return;

            time += Time.unscaledDeltaTime;
            ShowFrame();
        }

        private void ShowFrame()
        {
            if (frames == null || frames.Length == 0) return;
            image.sprite = frames[(int)(time * framesPerSecond) % frames.Length];
        }
    }
}
