using System.Collections.Generic;
using Ink.Runtime;

namespace CaptainPinkTurd.InkDialogue
{
    public struct DialogueInfo
    {
        public string speaker;
        public string line;
        public List<Choice> choices;
        //"top" or "bottom" (the #layout tag): where the panel stands, e.g. at the top so it doesn't hide the stage
        public string layout;
    }
}