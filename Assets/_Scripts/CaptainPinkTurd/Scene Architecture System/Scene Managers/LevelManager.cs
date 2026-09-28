using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.Attributes;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Scene.Story;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

namespace CaptainPinkTurd.Scene.Manager
{
    public class LevelManager : MonoBehaviour
    {
        [SerializeField] private bool isTutorialLevel;
        
        [ShowIf(nameof(isTutorialLevel))]
        [SerializeField] private bool isLastTutorialLevel;
        [ShowIf(EConditionalLogic.And, nameof(isTutorialLevel), true, nameof(isLastTutorialLevel), false)]
        [SerializeField] private int nextTutorialLevel;
        
        [ShowIf(nameof(isTutorialLevel), false)]
        [SerializeField] private LevelData levelData;
        [ShowIf(nameof(isTutorialLevel), false)]
        [SerializeField] private VoidEvent onRestart;
        
        [SerializeField] private AudioClip levelTheme;

        [Header("Story Mode")]
        [Tooltip("Story levels advance to the next story step instead of a random level, and restart themselves on death")]
        [SerializeField] private bool isStoryLevel;
        [ShowIf(nameof(isStoryLevel))]
        [SerializeField] private StoryData storyData;
        [ShowIf(nameof(isStoryLevel))]
        [Tooltip("Invoked when the exit is reached, before the next story step loads (e.g. a fade-out)")]
        [SerializeField] private UnityEvent onStoryLevelComplete;
        [ShowIf(nameof(isStoryLevel))]
        [SerializeField] private float storyLevelCompleteDelay;

        private bool isLeavingStoryLevel;

        private void Start()
        {
            MusicManager.Instance.Play(levelTheme, loop: true);
        }

        public void NextLevel()
        {
            if (isStoryLevel)
            {
                if (isLeavingStoryLevel) return;
                isLeavingStoryLevel = true;

                onStoryLevelComplete?.Invoke();
                StartCoroutine(AdvanceStoryAfterDelay());
                return;
            }
            
            if (isTutorialLevel && !levelData.hasDoneTutorial)
            {
                if (isLastTutorialLevel)
                {
                    levelData.hasDoneTutorial = true;
                    NextLevel();
                    return;
                }
                SceneController.Instance
                    .NewTransition()
                    .Load(SceneDatabase.Slots.SessionContent, $"{SceneDatabase.Scenes.Level} {nextTutorialLevel} Tutorial", true)
                    .WithOverlay()
                    .Perform();
            }
            else
            {
                int nextLevel = Random.Range(1, levelData.totalLevelInGame + 1);
            
                SceneController.Instance
                    .NewTransition()
                    .Load(SceneDatabase.Slots.SessionContent, $"{SceneDatabase.Scenes.Level} {nextLevel}", true)
                    .WithOverlay()
                    .Perform();
            }
        }

        public void Restart()
        {
            onRestart.Raise();

            if (isStoryLevel)
            {
                StoryFlow.ReloadCurrent(storyData);
                return;
            }

            if (isTutorialLevel)
            {
                SceneController.Instance
                    .NewTransition()
                    .Load(SceneDatabase.Slots.SessionContent, SceneManager.GetActiveScene().name, true)
                    .WithOverlay()
                    .Perform();
            }
            else
            {
                int restartLevel = Random.Range(1, levelData.totalLevelInGame + 1);
            
                SceneController.Instance
                    .NewTransition()
                    .Load(SceneDatabase.Slots.SessionContent, $"{SceneDatabase.Scenes.Level} {restartLevel}", true)
                    .WithOverlay()
                    .Perform();
            }
        }
        public void EndSession()
        {
            onRestart.Raise(); //putting this here cause the GameManager carry the player Unit hp 
            StoryFlow.Stop();
            
            SceneController.Instance
                .NewTransition()
                .Load(SceneDatabase.Slots.Menu, SceneDatabase.Scenes.MainMenu, true)
                .Unload(SceneDatabase.Slots.Session)
                .Unload(SceneDatabase.Slots.SessionContent)
                .WithClearUnusedAssets()
                .WithOverlay()
                .Perform();
        }

        private IEnumerator AdvanceStoryAfterDelay()
        {
            //realtime: a hit-stop or popup may have the clock at 0 when the exit is reached
            if (storyLevelCompleteDelay > 0f) yield return new WaitForSecondsRealtime(storyLevelCompleteDelay);
            StoryFlow.Advance(storyData);
        }
    }
}