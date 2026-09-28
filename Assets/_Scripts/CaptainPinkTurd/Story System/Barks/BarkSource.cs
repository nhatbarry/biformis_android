using CaptainPinkTurd.Core.Localization;
using UnityEngine;

namespace CaptainPinkTurd.Story.Barks
{
    /// <summary>
    /// Reads short in-game lines ("barks") from the ink story, so they are written and translated alongside the dialogue.
    /// Keeps its own Story instance, separate from DialogueManager, so barking never opens the dialogue panel;
    /// ink variables (e.g. a counter stepping through lines) persist for as long as this component lives.
    /// </summary>
    public class BarkSource : MonoBehaviour
    {
        [SerializeField] private TextAsset inkJson;
        [Tooltip("English build of the same story; used when the language is English")]
        [SerializeField] private TextAsset inkJsonEnglish;

        private const string SPEAKER_TAG_PREFIX = "speaker:";

        private Ink.Runtime.Story story;

        private void Awake()
        {
            var json = Localization.CurrentLanguage == ELanguage.English && inkJsonEnglish ? inkJsonEnglish : inkJson;
            story = new Ink.Runtime.Story(json.text);
        }

        /// <summary>
        /// Runs a knot to its end and returns its first line of text, with the speaker from its #speaker tag.
        /// </summary>
        public bool TryGetLine(string knot, out string speaker, out string line)
        {
            speaker = null;
            line = null;

            story.ChoosePathString(knot);
            //run the whole knot, not just the first line: logic after the line (e.g. "~ counter += 1") must still happen
            while (story.canContinue)
            {
                string text = story.Continue().Trim();
                if (line != null) continue;

                foreach (var tag in story.currentTags)
                {
                    if (tag.StartsWith(SPEAKER_TAG_PREFIX)) speaker = tag[SPEAKER_TAG_PREFIX.Length..].Trim();
                }
                if (text.Length > 0) line = text;
            }
            return line != null;
        }
    }
}
