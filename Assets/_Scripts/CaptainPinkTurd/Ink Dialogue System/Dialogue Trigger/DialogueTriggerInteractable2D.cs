using CaptainPinkTurd.Core.Attributes;
using CaptainPinkTurd.Core.Interfaces;
using UnityEngine;

namespace CaptainPinkTurd.InkDialogue.Trigger
{
    [RequireComponent(typeof(Collider2D))]
    public class DialogueTriggerInteractable2D : DialogueTriggerBase, IInteractable
    {
        [Header("Dialogue Trigger Configs")] 
        [SerializeField] private bool useVisualCue;
        
        [ShowIf(nameof(useVisualCue))]
        [SerializeField] private GameObject visualCue;

        public bool CanInteract => DialogueManager.HasInstance && !DialogueManager.Instance.DialogueIsPlaying;

        private void Awake()
        {
            if (!useVisualCue) return;
            
            visualCue.SetActive(false);
        }

        public void Interact()
        {
            StartDialogue();
        }
        public void OnTriggerRangeEnter()
        {
            if (!useVisualCue) return;
            visualCue.SetActive(true);
        }
        public void OnTriggerRangeExit()
        {
            if (!useVisualCue) return;
            visualCue.SetActive(false);
        }
    }
}
