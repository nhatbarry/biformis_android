using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CaptainPinkTurd.Story
{
    /// <summary>
    /// Counts a level down and fires onExpired at zero. Runs on scaled time, so pausing the game pauses it too.
    /// </summary>
    public class LevelCountdown : MonoBehaviour
    {
        [SerializeField] private float durationSeconds = 120f;
        [Tooltip("Gives the scene transition time to fade before the clock starts")]
        [SerializeField] private float startDelay = 1f;
        [SerializeField] private TMP_Text display;

        [Header("Warning")]
        [SerializeField] private float warningSeconds = 30f;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningColor = new(1f, 0.3f, 0.3f);
        [SerializeField] private UnityEvent onWarning;

        [SerializeField] private UnityEvent onExpired;

        private float remaining;
        private float delayRemaining;
        private bool warned;
        private bool expired;

        public float Remaining => remaining;

        private void Start()
        {
            remaining = durationSeconds;
            delayRemaining = startDelay;
            UpdateDisplay();
        }

        private void Update()
        {
            if (expired) return;

            if (delayRemaining > 0f)
            {
                delayRemaining -= Time.deltaTime;
                return;
            }

            remaining = Mathf.Max(0f, remaining - Time.deltaTime);

            if (!warned && remaining <= warningSeconds)
            {
                warned = true;
                onWarning?.Invoke();
            }

            UpdateDisplay();

            if (remaining > 0f) return;

            expired = true;
            onExpired?.Invoke();
        }

        private void UpdateDisplay()
        {
            if (!display) return;

            int seconds = Mathf.CeilToInt(remaining);
            display.text = $"{seconds / 60}:{seconds % 60:00}";
            display.color = remaining <= warningSeconds ? warningColor : normalColor;

            //pulse once a second while in the warning zone
            float pulse = remaining <= warningSeconds && remaining > 0f ? 1f + 0.15f * Mathf.Max(0f, Mathf.Cos(remaining * Mathf.PI * 2f)) : 1f;
            display.transform.localScale = Vector3.one * pulse;
        }
    }
}
