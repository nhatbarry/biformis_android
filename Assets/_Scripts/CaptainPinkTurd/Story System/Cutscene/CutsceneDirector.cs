using System.Collections;
using CaptainPinkTurd.InkDialogue;
using CaptainPinkTurd.Scene.Story;
using UnityEngine;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// Plays the ink knot of the current story step, then moves the story on.
    /// All cutscenes share one scene; what happens on screen is driven by stage tags in the ink (see CutsceneStage).
    /// </summary>
    public class CutsceneDirector : MonoBehaviour
    {
        [SerializeField] private StoryData storyData;
        [Tooltip("Knot played when the scene is opened directly in the editor, outside story mode")]
        [SerializeField] private string fallbackKnot = "Intro";
        [Tooltip("Lets the scene transition overlay fade out before the first line")]
        [SerializeField] private float startDelay = 1f;
        [SerializeField] private float endDelay = 0.75f;

        private bool finished;

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(startDelay);

            bool inStory = StoryFlow.IsStoryRunning && storyData.HasProgress &&
                           storyData.CurrentStep.type == EStoryStepType.Cutscene;
            string knot = inStory ? storyData.CurrentStep.knotName : fallbackKnot;

            var dialogueManager = DialogueManager.Instance;
            dialogueManager.EnterDialogue(knot);
            yield return new WaitUntil(() => !dialogueManager.DialogueIsPlaying);
            yield return new WaitForSecondsRealtime(endDelay);

            Finish();
        }

        /// <summary>
        /// Skip button: ends the cutscene straight away.
        /// </summary>
        public void Skip()
        {
            if (DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsPlaying)
            {
                DialogueManager.Instance.ExitDialogue();
            }
            Finish();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;

            if (StoryFlow.IsStoryRunning) StoryFlow.Advance(storyData);
        }
    }
}
