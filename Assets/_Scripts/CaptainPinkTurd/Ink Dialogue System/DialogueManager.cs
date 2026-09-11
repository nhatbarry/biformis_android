using System;
using System.Collections.Generic;
using CaptainPinkTurd.Core;
using CaptainPinkTurd.Core.DesignPattern.Singleton;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
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
        
        [Header("Dialogue Events")]
        [SerializeField] private VoidEvent onDialogueStart;
        [SerializeField] private VoidEvent onDialogueEnd;
        
        private InkDialogueVariables inkDialogueVariables;
        private Story story;
        private Animator layoutAnimator;

        private string currentSpeaker = "Default";
        private int currentChoiceIndex = -1;
        
        //very specific guard that prevent when interact input from InteractionDetector2D trigger
        //the EnterDialogue method first before the interact input over here trigger the Continue method after
        private bool firstFrameDialogueGuard; 

        private const string SPEAKER_TAG = "speaker";
        private const string PORTRAIT_TAG = "portrait";
        private const string LAYOUT_TAG = "layout";
        
        public GameEvent OnDialogueStart { get; private set; } 
        public GameEvent OnDialogueEnd { get; private set; } 
        public GameEvent<int> OnChoiceChosen { get; private set; } 
        public GameEvent<DialogueInfo> OnDisplayDialogue { get; private set; } 
        
        public bool DialogueIsPlaying { get; private set; }
        public bool DialogueIsTyping { get; internal set; }

        protected override void Awake()
        {
            base.Awake();
            //layoutAnimator = dialoguePanel.GetComponent<Animator>();
            
            story = new Story(inkJson.text);
            inkDialogueVariables = new InkDialogueVariables(story);
            
            DialogueIsPlaying = false;

            OnDialogueStart = new GameEvent();
            OnDisplayDialogue = new GameEvent<DialogueInfo>();
            OnDialogueEnd = new GameEvent();
            OnChoiceChosen = new GameEvent<int>();
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
            Story dummyStory = new Story(inkJson.text);
    
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
            currentSpeaker = "Default";
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
            DialogueIsPlaying = false;
            inkDialogueVariables.StopListening(story);
            story.ResetState();
            OnDialogueEnd.Raise();
        }
        
        //if race condition ever happens to input in the future, then you should implement an input events and context to your input assembly and
        //follow the same structure as the guy who made this system
        private void ContinueOrExitStory(InputAction.CallbackContext ctx)
        {
            if (!DialogueIsPlaying || firstFrameDialogueGuard) return;
            
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

                while (IsLineBlank(dialogueLine) && story.canContinue)
                {
                    dialogueLine = story.Continue();
                }
                if (IsLineBlank(dialogueLine) && !story.canContinue)
                {
                    ExitDialogue();
                }
                else
                {
                    HandleTags(story.currentTags);
                    DialogueIsTyping = true;
                    OnDisplayDialogue.Raise(new DialogueInfo()
                    {
                        speaker = currentSpeaker,
                        line = dialogueLine,
                        choices = story.currentChoices
                    });
                }
            }
            else if(story.currentChoices.Count == 0)
            {
                ExitDialogue();
            }
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
                    Debug.LogError("Tag could not be appropriately parsed: " + tag);
                }
                string tagKey = splitTag[0].Trim();
                string tagValue = splitTag[1].Trim();
            
                // handle the tag
                switch (tagKey) 
                {
                    case SPEAKER_TAG:
                        currentSpeaker = tagValue;
                        //displayNameText.text = tagValue;
                        break;
                    case PORTRAIT_TAG:
                        //portraitAnimator.Play(tagValue);
                        break;
                    case LAYOUT_TAG:
                        layoutAnimator.Play(tagValue);
                        break;
                    default:
                        Debug.LogWarning("Tag came in but is not currently being handled: " + tag);
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