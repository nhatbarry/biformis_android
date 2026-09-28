using System;
using System.Collections.Generic;
using CaptainPinkTurd.DataPersistence;
using CaptainPinkTurd.DataPersistence.Data;
using UnityEngine;

namespace CaptainPinkTurd.Scene.Story
{
    public enum EStoryStepType
    {
        Level,
        Cutscene,
    }

    [Serializable]
    public struct StoryStep
    {
        public EStoryStepType type;
        [Tooltip("Scene loaded for this step")]
        public string sceneName;
        [Tooltip("Cutscene steps only: the ink knot the cutscene plays")]
        public string knotName;
    }

    /// <summary>
    /// The story mode running order, plus how far the player has got (saved through the Data Persistence system).
    /// </summary>
    [CreateAssetMenu(fileName = "Story Data", menuName = "Game/Story/Story Data")]
    public class StoryData : ScriptableObject, IDataPersistence
    {
        [SerializeField] private List<StoryStep> steps = new();

        [NonSerialized] private int currentStepIndex = -1;
        [NonSerialized] private bool completed;

        public string Name => name;
        public IReadOnlyList<StoryStep> Steps => steps;
        public int CurrentStepIndex => currentStepIndex;
        public bool HasProgress => currentStepIndex >= 0 && currentStepIndex < steps.Count;
        public bool Completed => completed;
        public StoryStep CurrentStep => steps[Mathf.Clamp(currentStepIndex, 0, steps.Count - 1)];

        public void LoadData(GameData data)
        {
            currentStepIndex = data.storyStep;
            completed = data.storyCompleted;
        }

        public void SaveData(GameData data)
        {
            data.storyStep = currentStepIndex;
            data.storyCompleted = completed;
        }

        internal void SetStep(int index) => currentStepIndex = index;

        internal void MarkCompleted()
        {
            completed = true;
            currentStepIndex = -1;
        }
    }
}
