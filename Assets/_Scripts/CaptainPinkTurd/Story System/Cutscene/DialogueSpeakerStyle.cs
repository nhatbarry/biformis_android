using System;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.InkDialogue;
using TMPro;
using UnityEngine;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// The dialogue panel's look for each speaker: their portrait in a frame at the left of the panel, their name
    /// colour, and the text moved clear of the portrait.
    /// Portraits are clips of portraitAnimation named after the #speaker value: "Speaker" plays (usually loops) while
    /// they talk, "Speaker_appear" plays first when the speaker changes to them, and "Speaker_talk", if there is one,
    /// plays while their line is typing (A's mouth moves). A speaker without a clip has no portrait.
    /// Speakers in shakeSpeakers jitter while their line is typing (the villain's portrait has no talking mouth).
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

        private string portraitSpeaker;
        private bool shaking;
        private float nextShake;

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
            shaking = speaker != null && Array.IndexOf(shakeSpeakers, speaker) >= 0;
        }

        //the stage takes the screen: the portrait goes with the panel and appears again with the next line
        private void OnStagePause(bool paused)
        {
            if (paused) ShowPortrait(null);
        }

        private void OnDialogueEnd() => ShowPortrait(null);

        private void ShowPortrait(string speaker)
        {
            bool hasPortrait = speaker != null && portraitAnimation && portraitAnimation.HasClip(speaker);
            if (portraitFrame) portraitFrame.SetActive(hasPortrait);

            if (hasPortrait && speaker != portraitSpeaker)
            {
                string appear = speaker + "_appear";
                portraitAnimation.Play(portraitAnimation.HasClip(appear) ? appear : speaker);
            }
            portraitSpeaker = hasPortrait ? speaker : null;

            //the blocks stretch across the panel: only their left edge moves, the right edge stays put
            foreach (var block in textBlocks)
            {
                if (block) block.offsetMin = new Vector2(hasPortrait ? textLeftBesidePortrait : textLeft, block.offsetMin.y);
            }
        }

        private void Update()
        {
            if (!portraitAnimation) return;
            bool typing = portraitSpeaker != null && DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsTyping;
            if (portraitSpeaker != null)
            {
                string talk = portraitSpeaker + "_talk";
                if (portraitAnimation.HasClip(talk))
                {
                    if (typing && portraitAnimation.CurrentClip != talk) portraitAnimation.Play(talk);
                    else if (!typing && portraitAnimation.CurrentClip == talk) portraitAnimation.Play(portraitSpeaker);
                }
            }

            var rect = (RectTransform)portraitAnimation.transform;
            bool talking = shaking && typing;

            if (!talking)
            {
                rect.anchoredPosition = Vector2.zero;
                return;
            }
            if (Time.unscaledTime < nextShake) return;
            nextShake = Time.unscaledTime + shakeInterval;
            rect.anchoredPosition = new Vector2(UnityEngine.Random.Range(-1, 2), UnityEngine.Random.Range(-1, 2)) * shakeDistance;
        }
    }
}
