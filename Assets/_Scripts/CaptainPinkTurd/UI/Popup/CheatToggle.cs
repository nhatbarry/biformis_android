using CaptainPinkTurd.Core.InputPaths;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.UI.Popup
{
    /// <summary>Binds the authored pause checkbox to the gameplay and touch HUD cheat setting.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Toggle))]
    public sealed class CheatToggle : MonoBehaviour
    {
        private Toggle toggle;

        private void Awake() => toggle = GetComponent<Toggle>();

        private void OnEnable()
        {
            toggle.SetIsOnWithoutNotify(CheatSettings.Enabled);
            toggle.onValueChanged.AddListener(SetEnabled);
            CheatSettings.Changed += Refresh;
        }

        private void OnDisable()
        {
            toggle.onValueChanged.RemoveListener(SetEnabled);
            CheatSettings.Changed -= Refresh;
        }

        private static void SetEnabled(bool enabled) => CheatSettings.Enabled = enabled;

        private void Refresh(bool enabled) => toggle.SetIsOnWithoutNotify(enabled);
    }
}
