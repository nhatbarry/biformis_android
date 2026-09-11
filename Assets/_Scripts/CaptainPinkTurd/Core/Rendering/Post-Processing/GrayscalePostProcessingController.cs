using DG.Tweening;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CaptainPinkTurd.Core.Rendering.PostProcessing
{
    public class GrayscalePostProcessingController : MonoBehaviour
    {
        [SerializeField] private Volume globalVolume;
        [SerializeField] private Ease defaultEaseType;
        
        private ColorAdjustments colorAdjustments;
        private Tween activeTween;

        private float intensity;

        void Start()
        {
            if (globalVolume.profile.TryGet(out colorAdjustments))
            {
                colorAdjustments.saturation.overrideState = true;
            }
        }

        /// <summary>
        /// Smoothly transitions the screen to a specific grayscale intensity.
        /// </summary>
        /// <param name="targetIntensity">0.0 (full color) to 1.0 (full grayscale)</param>
        /// <param name="duration">How long the transition takes in seconds</param>
        /// <param name="independentUpdate">Ignore timescale</param>
        public void FadeTo(float targetIntensity, float duration, bool independentUpdate = false)
        {
            //Kill any active tween on this object to prevent fighting/stuttering
            activeTween?.Kill();

            targetIntensity = Mathf.Clamp01(targetIntensity);
            activeTween = DOTween.To(() => intensity, UpdateVisuals, targetIntensity, duration)
                .SetEase(defaultEaseType)
                .SetUpdate(UpdateType.Normal, independentUpdate); // Runs independently of Time.timeScale
        }

        /// <summary>
        /// Smoothly transitions the screen to a specific grayscale intensity.
        /// </summary>
        /// <param name="targetIntensity">0.0 (full color) to 1.0 (full grayscale)</param>
        /// <param name="duration">How long the transition takes in seconds</param>
        /// <param name="easeType">The ease type of the transition</param>
        /// <param name="independentUpdate">Ignore timescale</param>
        public void FadeTo(float targetIntensity, float duration, Ease easeType, bool independentUpdate = false)
        {
            //Kill any active tween on this object to prevent fighting/stuttering
            activeTween?.Kill();

            targetIntensity = Mathf.Clamp01(targetIntensity);
            activeTween = DOTween.To(() => intensity, UpdateVisuals, targetIntensity, duration)
                .SetEase(easeType)
                .SetUpdate(UpdateType.Normal, independentUpdate); // Runs independently of Time.timeScale
        }

        // Internal method that maps 0-1 range to Unity's post-processing values
        private void UpdateVisuals(float value)
        {
            intensity = value;

            if (colorAdjustments)
            {
                // Maps 0.0 (Normal) to 1.0 (Grayscale) into Unity's -100 saturation range
                colorAdjustments.saturation.value = Mathf.Lerp(0f, -100f, intensity);
            }
        }

        private void OnDestroy()
        {
            // Clean up tweens to prevent memory leaks if the object is destroyed
            activeTween?.Kill();
        }
    }
}