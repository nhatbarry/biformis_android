using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using CaptainPinkTurd.Core;
using CaptainPinkTurd.Core.DesignPattern.Singleton;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Core.Localization;
using CaptainPinkTurd.Core.Utils;
using CaptainPinkTurd.Input;
using Ink.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CaptainPinkTurd.InkDialogue
{
    public class DialogueManager : Singleton<DialogueManager>
    {
        [Header("Ink Story")]
        [SerializeField] private TextAsset inkJson;
        [Tooltip("Optional English build of the same story (same knots and variables). Used when the language is English.")]
        [SerializeField] private TextAsset inkJsonEnglish;

        [Header("Dialogue Events")]
        [SerializeField] private VoidEvent onDialogueStart;
        [SerializeField] private VoidEvent onDialogueEnd;

        [Header("Auto Advance")]
        [Tooltip("Moves on to the next line by itself once a line has been on screen for a while without input")]
        [SerializeField] private bool autoAdvance = true;
        [Tooltip("Reading time for any line, in seconds")]
        [SerializeField] private float autoAdvanceBaseDelay = 2.5f;
        [Tooltip("Extra reading time per character of the line, in seconds")]
        [SerializeField] private float autoAdvancePerCharacter = 0.05f;
        
        private InkDialogueVariables inkDialogueVariables;
        private Story story;
        private Animator layoutAnimator;

        public const string DEFAULT_SPEAKER = "Default";
        public const string DEFAULT_LAYOUT = "bottom";

        private string currentSpeaker = DEFAULT_SPEAKER;
        private string currentLayout = DEFAULT_LAYOUT;
        private int currentChoiceIndex = -1;
        
        //very specific guard that prevent when interact input from InteractionDetector2D trigger
        //the EnterDialogue method first before the interact input over here trigger the Continue method after
        private bool firstFrameDialogueGuard; 

        private const string SPEAKER_TAG = "speaker";
        private const string PORTRAIT_TAG = "portrait";
        private const string LAYOUT_TAG = "layout";
        //"wait:seconds" pauses the dialogue (panel hidden) so the cutscene stage can act out a moment without text
        private const string WAIT_TAG = "wait";
        //"hold" pauses the same way until the stage calls ReleaseStageHold (e.g. a struggle the player taps through)
        private const string HOLD_TAG = "hold";
        private bool stageHeld;

        private Coroutine stagingRoutine;
        private float lineIdleTime;
        private float lineReadingTime;

        public GameEvent OnDialogueStart { get; private set; } 
        public GameEvent OnDialogueEnd { get; private set; } 
        public GameEvent<int> OnChoiceChosen { get; private set; } 
        public GameEvent<DialogueInfo> OnDisplayDialogue { get; private set; }
        //raised with the raw text of every tag this manager doesn't handle itself (e.g. "bg:white"), for cutscene staging
        public GameEvent<string> OnStageTag { get; private set; }
        //true while a "wait" tag holds the dialogue so the stage can act; the panel hides until it is raised with false
        public GameEvent<bool> OnStagePause { get; private set; }

        public bool DialogueIsPlaying { get; private set; }
        public bool IsStaging { get; private set; }
        //raised for every continue input (tap, Interact) while the stage holds the dialogue, so the stage can use it
        public GameEvent OnStageInput { get; private set; }
        public bool DialogueIsTyping { get; internal set; }

        private TextAsset ActiveInkJson =>
            Localization.CurrentLanguage == ELanguage.English && inkJsonEnglish ? inkJsonEnglish : inkJson;

        protected override void Awake()
        {
            base.Awake();
            //layoutAnimator = dialoguePanel.GetComponent<Animator>();

            story = new Story(ActiveInkJson.text);
            inkDialogueVariables = new InkDialogueVariables(story);
            
            DialogueIsPlaying = false;

            OnDialogueStart = new GameEvent();
            OnDisplayDialogue = new GameEvent<DialogueInfo>();
            OnDialogueEnd = new GameEvent();
            OnChoiceChosen = new GameEvent<int>();
            OnStageTag = new GameEvent<string>();
            OnStagePause = new GameEvent<bool>();
            OnStageInput = new GameEvent();
        }

        private void OnEnable()
        {
            InputManager.Instance.InputSystemActions.Player.Interact.performed += ContinueOrExitStory;
            
            OnDialogueStart.Subscribe(onDialogueStart.Raise);
            OnDialogueEnd.Subscribe(onDialogueEnd.Raise);
        }

        private void OnDisable()
        {
            OnDialogueStart.Unsubscribe(onDialogueStart.Raise);
            OnDialogueEnd.Unsubscribe(onDialogueEnd.Raise);
            
            if (!InputManager.HasInstance) return;
            InputManager.Instance.InputSystemActions.Player.Interact.performed -= ContinueOrExitStory;
        }

        public void OnGameOverEvent()
        {
            Story dummyStory = new Story(ActiveInkJson.text);
    
            foreach (string varName in dummyStory.variablesState)
            {
                var defaultValue = dummyStory.variablesState[varName];
                // Assign this back to your Persistent Unity Global Dictionary
                story.variablesState[varName] = defaultValue; 
            }
        }

        public void EnterDialogue(string knotName, bool resetCallstack = true, params object[] arguments)
        {
            DialogueIsPlaying = true;
            OnDialogueStart.Raise();

            if (!knotName.Equals(""))
            {
                story.ChoosePathString(knotName, resetCallstack, arguments);
            }
            else
            {
                Debug.Log("No knot name provided");
            }
            
            //reset portrait, layout and speaker
            currentSpeaker = DEFAULT_SPEAKER;
            currentLayout = DEFAULT_LAYOUT;
            //layoutAnimator.Play("left");
            
            //start listening for variables
            inkDialogueVariables.SyncVariablesAndStartListening(story);
            
            ContinueOrExitStory(default);
            
            firstFrameDialogueGuard = true;
            StartCoroutine(CoroutineUtils.WaitForNextFrames(() =>
            {
                firstFrameDialogueGuard = false;
            }));
        }

        public void ExitDialogue()
        {
            if (stagingRoutine != null) StopCoroutine(stagingRoutine);
            stagingRoutine = null;
            stageHeld = false;
            IsStaging = false;
            DialogueIsPlaying = false;
            inkDialogueVariables.StopListening(story);
            story.ResetState();
            OnDialogueEnd.Raise();
        }

        /// <summary>
        /// Advances the dialogue exactly like the Interact input does. Used by tap-to-continue on touch screens.
        /// </summary>
        public void RequestContinue() => ContinueOrExitStory(default);

        //if race condition ever happens to input in the future, then you should implement an input events and context to your input assembly and
        //follow the same structure as the guy who made this system
        private void ContinueOrExitStory(InputAction.CallbackContext ctx)
        {
            if (!DialogueIsPlaying || firstFrameDialogueGuard) return;
            if (IsStaging)
            {
                OnStageInput.Raise();
                return;
            }
            
            if (DialogueIsTyping)
            {
                OnDisplayDialogue.Raise(new DialogueInfo());
                return;
            }
            
            if (story.currentChoices.Count > 0 && currentChoiceIndex != -1)
            {
                OnChoiceChosen.Raise(currentChoiceIndex);
                story.ChooseChoiceIndex(currentChoiceIndex);
                currentChoiceIndex = -1;
            }
            
            if(story.canContinue)
            {
                string dialogueLine = story.Continue(); //calling continue first is important in here if you want to get the correct execution order
                //blank lines can still carry tags (e.g. a staging tag on its own line), so keep them instead of dropping them with the line
                var tags = new List<string>(story.currentTags);

                while (IsLineBlank(dialogueLine) && story.canContinue)
                {
                    dialogueLine = story.Continue();
                    tags.AddRange(story.currentTags);
                }
                bool ends = IsLineBlank(dialogueLine) && !story.canContinue;

                if (tags.Exists(tag => TryGetWait(tag, out _) || IsHold(tag)))
                {
                    stagingRoutine = StartCoroutine(StageThenShow(tags, dialogueLine, ends));
                    return;
                }
                HandleTags(tags);
                ShowOrExit(dialogueLine, ends);
            }
            else if(story.currentChoices.Count == 0)
            {
                ExitDialogue();
            }
        }

        private void ShowOrExit(string dialogueLine, bool ends)
        {
            if (ends)
            {
                ExitDialogue();
                return;
            }

            DialogueIsTyping = true;
            lineIdleTime = 0f;
            lineReadingTime = autoAdvanceBaseDelay + StripRichText(dialogueLine).Trim().Length * autoAdvancePerCharacter;
            OnDisplayDialogue.Raise(new DialogueInfo()
            {
                speaker = currentSpeaker,
                line = dialogueLine,
                choices = story.currentChoices,
                layout = currentLayout
            });
        }

        /// <summary>
        /// Auto advance: the reading time starts once the line has finished typing, and a line with choices waits for one.
        /// </summary>
        private void Update()
        {
            if (!autoAdvance || !DialogueIsPlaying || DialogueIsTyping || IsStaging || story.currentChoices.Count > 0) return;

            lineIdleTime += Time.unscaledDeltaTime;
            if (lineIdleTime < lineReadingTime) return;

            lineIdleTime = 0f;
            ContinueOrExitStory(default);
        }

        private static string StripRichText(string line) => System.Text.RegularExpressions.Regex.Replace(line, @"<[^>]*>|\[[^\]]*\]", ""); //rich text and [pause=]/[speed=] typewriter tags

        /// <summary>
        /// A line carrying "#wait:seconds" plays out its staging first: the tags before each wait run, the panel is
        /// hidden while the stage acts, then the rest of the tags run and the line shows (or the dialogue ends).
        /// </summary>
        private IEnumerator StageThenShow(List<string> tags, string dialogueLine, bool ends)
        {
            IsStaging = true;
            OnStagePause.Raise(true);

            var pending = new List<string>();
            foreach (string tag in tags)
            {
                if (IsHold(tag))
                {
                    stageHeld = true;
                    HandleTags(pending);
                    pending.Clear();
                    yield return new WaitUntil(() => !stageHeld);
                    continue;
                }
                if (!TryGetWait(tag, out float seconds))
                {
                    pending.Add(tag);
                    continue;
                }
                HandleTags(pending);
                pending.Clear();
                yield return new WaitForSecondsRealtime(seconds);
            }
            HandleTags(pending);

            IsStaging = false;
            stagingRoutine = null;
            if (!ends) OnStagePause.Raise(false);
            ShowOrExit(dialogueLine, ends);
        }

        /// <summary>
        /// Ends a "hold" staging pause; the line's remaining tags run and the line shows.
        /// </summary>
        public void ReleaseStageHold() => stageHeld = false;

        private static bool IsHold(string tag) => tag.Trim() == HOLD_TAG;

        private static bool TryGetWait(string tag, out float seconds)
        {
            seconds = 0f;
            string trimmed = tag.Trim();
            return trimmed.StartsWith(WAIT_TAG + ":") &&
                   float.TryParse(trimmed[(WAIT_TAG.Length + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);
        }

        public void UpdateChoiceIndex(int index) => currentChoiceIndex = index;
        public void UpdateInkDialogueVariable(string name, object value)
        {
            Ink.Runtime.Object inkValue = value switch
            {
                int i => new IntValue(i),
                float f => new FloatValue(f),
                bool b => new BoolValue(b),
                string s => new StringValue(s),
                _ => throw new ArgumentException($"Unsupported Ink variable type: {value?.GetType()}")
            };

            inkDialogueVariables.UpdateVariableToStory(story, name, inkValue);
        }
        private bool IsLineBlank(string dialogueLine) => dialogueLine.Trim().Equals("") || dialogueLine.Trim().Equals("\n");
        
        private void HandleTags(List<string> tags)
        {
            // loop through each tag and handle it accordingly
            foreach (string tag in tags) 
            {
                // parse the tag
                string[] splitTag = tag.Split(':');
                if (splitTag.Length != 2)
                {
                    //not a key:value tag, so it can only be meant for whoever stages the scene
                    OnStageTag.Raise(tag.Trim());
                    continue;
                }
                string tagKey = splitTag[0].Trim();
                string tagValue = splitTag[1].Trim();
            
                // handle the tag
                switch (tagKey) 
                {
                    case SPEAKER_TAG:
                        currentSpeaker = tagValue; //display name is resolved by DialoguePanelUI
                        break;
                    case PORTRAIT_TAG:
                        //portraitAnimator.Play(tagValue);
                        break;
                    case LAYOUT_TAG:
                        currentLayout = tagValue; //kept until changed, like the speaker
                        if (layoutAnimator) layoutAnimator.Play(tagValue);
                        break;
                    default:
                        OnStageTag.Raise(tag.Trim());
                        break;
                }
            }
        }

        #region External Functions Bind

        //Subscribe these to OnDialogueStart and OnDialogueEnd so it could be used externally as well
        public void BindFunction(string functionName, Action action)
        {
            story.BindExternalFunction(functionName, action);
        }
        public void BindFunction<T>(string functionName, Action<T> action)
        {
            story.BindExternalFunction(functionName, action);
        }
        public void UnbindFunction(string functionName)
        {
            story.UnbindExternalFunction(functionName);
        }

        #endregion
    }
}