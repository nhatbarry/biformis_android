using CaptainPinkTurd.DataPersistence;
using UnityEngine;

namespace CaptainPinkTurd.Scene.Story
{
    /// <summary>
    /// Moves the player through StoryData's steps and saves after every move.
    /// Saving immediately matters: DataPersistenceManager reloads the save file on every scene load,
    /// so an unsaved step change would be reverted by the very transition it triggers.
    /// </summary>
    public static class StoryFlow
    {
        public static bool IsStoryRunning { get; private set; }

        public static void StartNew(StoryData data)
        {
            data.SetStep(0);
            Save();
            IsStoryRunning = true;
            LoadFromMenu(data.CurrentStep.sceneName);
        }

        /// <summary>
        /// Jumps straight to any step (level select for testing). Overwrites the story save, like starting a new game.
        /// </summary>
        public static void StartAt(StoryData data, int step)
        {
            data.SetStep(Mathf.Clamp(step, 0, data.Steps.Count - 1));
            Save();
            IsStoryRunning = true;
            LoadFromMenu(data.CurrentStep.sceneName);
        }

        public static void Continue(StoryData data)
        {
            if (!data.HasProgress)
            {
                StartNew(data);
                return;
            }

            IsStoryRunning = true;
            LoadFromMenu(data.CurrentStep.sceneName);
        }

        public static void Advance(StoryData data)
        {
            int next = data.CurrentStepIndex + 1;
            if (next >= data.Steps.Count)
            {
                data.MarkCompleted();
                Save();
                ReturnToMenu();
                return;
            }

            data.SetStep(next);
            Save();
            LoadInSession(data.CurrentStep.sceneName);
        }

        public static void ReloadCurrent(StoryData data)
        {
            LoadInSession(data.CurrentStep.sceneName);
        }

        public static void ReturnToMenu()
        {
            IsStoryRunning = false;

            SceneController.Instance
                .NewTransition()
                .Load(SceneDatabase.Slots.Menu, SceneDatabase.Scenes.MainMenu, true)
                .Unload(SceneDatabase.Slots.Session)
                .Unload(SceneDatabase.Slots.SessionContent)
                .WithClearUnusedAssets()
                .WithOverlay()
                .Perform();
        }

        public static void Stop() => IsStoryRunning = false;

        private static void LoadFromMenu(string sceneName)
        {
            SceneController.Instance
                .NewTransition()
                .Load(SceneDatabase.Slots.Session, SceneDatabase.Scenes.Session)
                .Load(SceneDatabase.Slots.SessionContent, sceneName, true)
                .Unload(SceneDatabase.Slots.Menu)
                .WithOverlay()
                .Perform();
        }

        private static void LoadInSession(string sceneName)
        {
            SceneController.Instance
                .NewTransition()
                .Load(SceneDatabase.Slots.SessionContent, sceneName, true)
                .WithOverlay()
                .Perform();
        }

        private static void Save()
        {
            var persistence = DataPersistenceManager.Instance;
            if (!persistence.HasGameData) persistence.NewGame();
            persistence.SaveGame();
        }

        //Enter Play Mode Options may skip the domain reload, so drop static state between play sessions
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => IsStoryRunning = false;
    }
}
