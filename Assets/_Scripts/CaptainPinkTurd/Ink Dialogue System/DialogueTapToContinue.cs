using UnityEngine;
using UnityEngine.EventSystems;

namespace CaptainPinkTurd.InkDialogue
{
    /// <summary>
    /// Put on a full-screen raycast target behind the dialogue panel: tapping (or clicking) anywhere advances
    /// the dialogue the same way the Interact input does. Touch screens have no Interact key in cutscenes.
    /// </summary>
    public class DialogueTapToContinue : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            if (DialogueManager.HasInstance) DialogueManager.Instance.RequestContinue();
        }
    }
}
