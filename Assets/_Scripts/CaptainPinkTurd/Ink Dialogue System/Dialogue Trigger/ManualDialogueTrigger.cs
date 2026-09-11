namespace CaptainPinkTurd.InkDialogue.Trigger
{
    public class ManualDialogueTrigger : DialogueTriggerBase
    {
        public void TriggerDialogue(bool force = false) => StartDialogue(force);
    }
}