using TMPro;
using UnityEngine;

namespace CaptainPinkTurd.Core.Localization
{
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Key in Resources/Localization/Strings.txt")]
        [SerializeField] private string key;
        [Tooltip("Optional key used instead on touch devices, e.g. control hints")]
        [SerializeField] private string touchKey;

        private TMP_Text text;

        private void Awake()
        {
            text = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            Localization.OnLanguageChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Localization.OnLanguageChanged -= Refresh;
        }

        public void SetKey(string newKey)
        {
            key = newKey;
            Refresh();
        }

        public void SetKeys(string newKey, string newTouchKey)
        {
            touchKey = newTouchKey;
            SetKey(newKey);
        }

        public void Refresh()
        {
            if (!text) text = GetComponent<TMP_Text>();

            string activeKey = Localization.IsTouchPlatform && !string.IsNullOrEmpty(touchKey) ? touchKey : key;
            if (string.IsNullOrEmpty(activeKey)) return;

            text.text = Localization.Get(activeKey);
        }
    }
}
