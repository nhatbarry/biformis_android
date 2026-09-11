using UnityEngine;

namespace CaptainPinkTurd.InkDialogue.Trigger
{
    public abstract class DialogueTriggerBase : MonoBehaviour
    {
        [Header("Dialogue Trigger Base Configs")]
        [SerializeField] protected string knotName;
        
        private object[] arguments;
        
        protected void StartDialogue(bool force = false)
        {
            switch (DialogueManager.Instance.DialogueIsPlaying)
            {
                case true when force:
                    DialogueManager.Instance.ExitDialogue();
                    break;
                case true:
                    return;
            }
            
            DialogueManager.Instance.EnterDialogue(knotName, arguments: arguments);
        }

        public void SetKnotName(string knotName) => this.knotName = knotName;

        public void SetKnotNameAndStartNewDialogue(string knotName)
        {
            SetKnotName(knotName);
            StartDialogue(true);
        }
        public void AddKnotName(string addedName) => knotName += addedName;
        public void WithParameter(params object[] parameter)
        {
            arguments = parameter;
        }
    }
}