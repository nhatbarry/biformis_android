using System;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.InkDialogue;
using TMPro;
using UnityEngine;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// Speaker-name colours and full-width dialogue. Portrait assets stay assigned for a future redesign,
    /// but the current dialogue presentation never shows them.
    /// </summary>
    public class DialogueSpeakerStyle : MonoBehaviour
    {
        [Header("Portrait")]
        [SerializeField] private GameObject portraitFrame;
        [SerializeField] private StageActorAnimation portraitAnimation;
        [SerializeField] private string[] shakeSpeakers = { "Villain" };
        [SerializeField] private float shakeDistance = 2f;
        [SerializeField] private float shakeInterval = 0.05f;

        [Header("Text")]
        [Tooltip("Speaker name and dialogue text, stretched across the panel: their left edge moves clear of the portrait")]
        [SerializeField] private RectTransform[] textBlocks;
        [SerializeField] private float textLeft = 10f;
        [SerializeField] private float textLeftBesidePortrait = 190f;

        [Header("Name")]
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private Color defaultNameColor = Color.white;
        [SerializeField] private SerializeKeyValuePair<string, Color>[] nameColors;

        //Start, not OnEnable: DialogueManager creates its events in its Awake
        private void Start()
        {
            var dialogueManager = DialogueManager.Instance;
            dialogueManager.OnDisplayDialogue.Subscribe(OnDisplayDialogue);
            dialogueManager.OnStagePause.Subscribe(OnStagePause);
            dialogueManager.OnDialogueEnd.Subscribe(OnDialogueEnd);
            ShowPortrait(null);
        }

        private void OnDestroy()
        {
            if (!DialogueManager.HasInstance) return;
            DialogueManager.Instance.OnDisplayDialogue.Unsubscribe(OnDisplayDialogue);
            DialogueManager.Instance.OnStagePause.Unsubscribe(OnStagePause);
            DialogueManager.Instance.OnDialogueEnd.Unsubscribe(OnDialogueEnd);
        }

        private void OnDisplayDialogue(DialogueInfo info)
        {
            if (info.line == null) return; //a "skip typing" request, not a new line

            string speaker = string.IsNullOrEmpty(info.speaker) || info.speaker == DialogueManager.DEFAULT_SPEAKER ? null : info.speaker;
            ShowPortrait(speaker);
            if (speakerNameText)
            {
                speakerNameText.color = speaker != null && nameColors.TryGetValue(speaker, out Color color) ? color : defaultNameColor;
            }
        }

        //the stage takes the screen: the portrait goes with the panel and appears again with the next line
        private void OnStagePause(bool paused)
        {
            if (paused) ShowPortrait(null);
        }

        private void OnDialogueEnd() => ShowPortrait(null);

        private void ShowPortrait(string speaker)
        {
            if (portraitFrame) portraitFrame.SetActive(false);

            //the blocks stretch across the panel: only their left edge moves, the right edge stays put
            foreach (var block in textBlocks)
            {
                if (block) block.offsetMin = new Vector2(textLeft, block.offsetMin.y);
            }
        }

    }
}
