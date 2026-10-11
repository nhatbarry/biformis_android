using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.AudioSystem
{
    [RequireComponent(typeof(Slider))]
    public class VolumeSlider : MonoBehaviour
    {
        [SerializeField] private EVolumeType volumeType;
        
        private Slider slider;

        public EVolumeType VolumeType => volumeType;
        
        private void Awake()
        {
            slider = GetComponent<Slider>();
            slider.wholeNumbers = false;
            slider.minValue = 0.0001f;
            slider.maxValue = 1f;
        }
        private void OnEnable()
        {
            Refresh();
            slider.onValueChanged.AddListener(OnValueChanged);
        }
        private void OnDisable()
        {
            slider.onValueChanged.RemoveListener(OnValueChanged);
        }
        private void Start()
        {
            Refresh();
        }

        public void Configure(EVolumeType type)
        {
            volumeType = type;
            if (slider) Refresh();
        }

        private void Refresh()
        {
            // Reopening pause or the menu must show the current shared settings, without writing them back.
            var audio = AudioManager.TryGetInstance();
            if (!audio) return;

            switch (volumeType)
            {
                case EVolumeType.Master:
                    slider.SetValueWithoutNotify(audio.GetMasterVolume());
                    break;
                case EVolumeType.Music:
                    slider.SetValueWithoutNotify(audio.GetMusicVolume());
                    break;
                case EVolumeType.SFX:
                    slider.SetValueWithoutNotify(audio.GetSFXVolume());
                    break;
            }
        }

        private void OnValueChanged(float value)
        {
            AudioManager.Instance.SetVolume(volumeType, value);
        }
    }
}
