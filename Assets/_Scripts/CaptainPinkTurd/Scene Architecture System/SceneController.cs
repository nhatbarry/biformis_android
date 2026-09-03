using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.Core.DesignPattern.Singleton;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Core.Utilities;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaptainPinkTurd.Scene
{
    public class SceneController : Singleton<SceneController>
    {
        [SerializeField] private LoadingOverlay loadingOverlay;
        [SerializeField] private VoidEvent onSceneLoaded;
        [SerializeField] private VoidEvent onBeforeSceneUnload;

        private Dictionary<string, string> loadedSceneBySlot = new();
        private bool isBusy = false;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
        {
            onSceneLoaded.Raise();
        }

        //API
        public SceneTransitionPlan NewTransition()
        {
            return new SceneTransitionPlan();
        }
        
        //Implementations
        private Coroutine ExecutePlan(SceneTransitionPlan plan)
        {
            if(isBusy)
            {
                Debug.LogWarning("Scene change is already in progress");
                return null;
            }

            //in case the time scale is currently 0, and the scene needs to transit right away like the pause menu
            //abort first: a hit-stop still counting down would otherwise outlive the transition and restore
            //its own saved time scale over this one, freezing the level we are about to load
            HitStop.Abort();
            Time.timeScale = 1;
            isBusy = true;
            return StartCoroutine(ChangeSceneRoutine(plan));
        }

        private IEnumerator ChangeSceneRoutine(SceneTransitionPlan plan)
        {
            if (plan.Overlay)
            {
                yield return loadingOverlay.FadeInBlack();
                yield return new WaitForSeconds(0.5f);
            }
            
            foreach (var slotKey in plan.ScenesToUnload)
            {
                yield return UnloadSceneRoutine(slotKey);
            }

            if (plan.ClearUnusedAssets) yield return CleanUpUnusedAssetsRoutine();

            foreach (var kvp in plan.ScenesToLoad)
            {
                if (loadedSceneBySlot.ContainsKey(kvp.Key))
                {
                    yield return UnloadSceneRoutine(kvp.Key);
                }
                yield return LoadAdditiveRoutine(kvp.Key, kvp.Value, plan.ActiveSceneName == kvp.Value);
            }
            if(plan.Overlay)
            {
                yield return loadingOverlay.FadeOutBlack();
            }
            isBusy = false;
        }

        private IEnumerator LoadAdditiveRoutine(string slotKey, string sceneName, bool setActive)
        {
            AsyncOperation loadOp = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            
            if(loadOp == null) yield break;
            
            //Let the scene load first before activate it
            loadOp.allowSceneActivation = false;

            while (loadOp.progress < 0.9f)
            {
                yield return null;
            }
            
            loadOp.allowSceneActivation = true;
            while(!loadOp.isDone)
            {
                yield return null;
            }
            
            if(setActive)
            {
                var newScene = SceneManager.GetSceneByName(sceneName);
                if(newScene.IsValid() && newScene.isLoaded)
                {
                    SceneManager.SetActiveScene(newScene);
                }
            }
            loadedSceneBySlot[slotKey] = sceneName;
        }
        private IEnumerator UnloadSceneRoutine(string slotKey)
        {
            if(!loadedSceneBySlot.TryGetValue(slotKey, out string sceneName)) yield break;
            if(string.IsNullOrEmpty(sceneName)) yield break;
            
            onBeforeSceneUnload.Raise();
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(sceneName);
            if (unloadOp != null)
            {
                while (!unloadOp.isDone)
                {
                    yield return null;
                }
            } 
            loadedSceneBySlot.Remove(slotKey);
        }
        
        //Shouldn't use this too often, but when we have a bg scene change/unload a whole session then we can use this to free up memory
        private IEnumerator CleanUpUnusedAssetsRoutine()
        {
            AsyncOperation cleanUpOp = Resources.UnloadUnusedAssets();
            while (!cleanUpOp.isDone)
            {
                yield return null;
            }
        }

        //Transition Plan Class
        public class SceneTransitionPlan
        {
            public Dictionary<string, string> ScenesToLoad { get; } = new();
            public List<string> ScenesToUnload { get; } = new();
            public string ActiveSceneName { get; private set; } = "";
            public bool ClearUnusedAssets { get; private set; } = false;
            public bool Overlay { get; private set; } = false;

            public SceneTransitionPlan Load(string slotKey, string sceneName, bool setActive = false)
            {
                ScenesToLoad[slotKey] = sceneName;
                if(setActive) ActiveSceneName = sceneName;
                
                return this;
            }
            public SceneTransitionPlan Unload(string slotKey)
            {
                ScenesToUnload.Add(slotKey);
                return this;
            }

            public SceneTransitionPlan WithOverlay()
            {
                Overlay = true;
                return this;
            }

            public SceneTransitionPlan WithClearUnusedAssets()
            {
                ClearUnusedAssets = true;
                return this;
            }
            public Coroutine Perform()
            {
                return SceneController.Instance.ExecutePlan(this);
            }
        }
    }
}