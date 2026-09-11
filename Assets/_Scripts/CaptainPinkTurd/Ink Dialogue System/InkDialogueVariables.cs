using System.Collections.Generic;
using Ink.Runtime;
using UnityEngine;

namespace CaptainPinkTurd.InkDialogue
{
    public class InkDialogueVariables //will need to rewatch the conditional dialogue part of the tutorial to understand how this work better in the future
    {
        private readonly Dictionary<string, Ink.Runtime.Object> variables;

        public InkDialogueVariables(Story story) 
        {
            // initialize the dictionary using the global variables in the story
            variables = new Dictionary<string, Ink.Runtime.Object>();
            foreach (string name in story.variablesState)
            {
                var value = story.variablesState.GetVariableWithName(name);
                variables.Add(name, value);
                //Debug.Log("Initialized global dialogue variable: " + name + " = " + value);
            }
        }

        public void SyncVariablesAndStartListening(Story story) 
        {
            // it's important that SyncVariablesToStory is before assigning the listener!
            SyncVariablesToStory(story);
            story.variablesState.variableChangedEvent += UpdateVariableFromStory;
        }

        public void StopListening(Story story)
        {
            story.variablesState.variableChangedEvent -= UpdateVariableFromStory;
        }

        private void UpdateVariableFromStory(string name, Ink.Runtime.Object value)
        {
            // only maintain variables that were initialized from the globals ink file
            if (!variables.ContainsKey(name)) return; 
            
            variables[name] = value;
            //Debug.Log("Updated dialogue variable: " + name + " = " + value);
        }

        internal void UpdateVariableToStory(Story story, string name, Ink.Runtime.Object value)
        {
            // only maintain variables that were initialized from the globals ink file
            if (!variables.ContainsKey(name)) return; 
            
            variables[name] = value;
            SyncVariablesToStory(story);
        }

        private void SyncVariablesToStory(Story story)
        {
            foreach (KeyValuePair<string, Ink.Runtime.Object> variable in variables)
            {
                story.variablesState.SetGlobal(variable.Key, variable.Value);
            }
        }
    }
}