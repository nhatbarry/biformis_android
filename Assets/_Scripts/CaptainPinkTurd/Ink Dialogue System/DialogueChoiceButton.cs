using System;
using CaptainPinkTurd.AudioSystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CaptainPinkTurd.InkDialogue
{
    public class DialogueChoiceButton : MonoBehaviour, ISelectHandler
    {
        [Header("Components")]
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text choiceText;
        [SerializeField] private SoundData hoverSfx;
        [SerializeField] private SoundData submitSfx;

        private Image buttonImage;
        private int choiceIndex = -1;
        
        public Button Button => button;

        private void Awake()
        {
            buttonImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            buttonImage.SetNativeSize();
        }

        public void SetChoiceText(string choiceTextString)
        {
            choiceText.text = choiceTextString;
        }

        public void SetChoiceIndex(int choiceIndex)
        {
            this.choiceIndex = choiceIndex;
            transform.SetSiblingIndex(choiceIndex);
        }

        public void SelectButton()
        {
            button.Select();
        }

        public void OnSelect(BaseEventData eventData)
        {
            SoundManager.Instance.CreateSoundBuilder().WithRandomPitch().Play(hoverSfx);
            DialogueManager.Instance.UpdateChoiceIndex(choiceIndex);
        }

        public void PlaySubmitSfx()
        {
            SoundManager.Instance.CreateSoundBuilder().WithRandomPitch().Play(submitSfx);
        }
    }
}