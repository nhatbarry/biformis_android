using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// Pulses a 2D light's intensity (alarm lights), or drifts it from one value to another over time
    /// (a level slowly getting darker).
    /// </summary>
    [RequireComponent(typeof(Light2D))]
    public class LightPulse : MonoBehaviour
    {
        public enum EMode
        {
            Pulse,
            Drift,
        }

        [SerializeField] private EMode mode = EMode.Pulse;
        [SerializeField] private float minIntensity = 0.2f;
        [SerializeField] private float maxIntensity = 1.2f;
        [Tooltip("Pulse: seconds per blink. Drift: seconds to go from max to min")]
        [SerializeField] private float period = 1.2f;

        private Light2D light2D;
        private float time;

        private void Awake()
        {
            light2D = GetComponent<Light2D>();
        }

        private void Update()
        {
            time += Time.deltaTime;

            light2D.intensity = mode == EMode.Pulse
                ? Mathf.Lerp(minIntensity, maxIntensity, 0.5f + 0.5f * Mathf.Sin(time / period * Mathf.PI * 2f))
                : Mathf.Lerp(maxIntensity, minIntensity, time / period);
        }
    }
}
