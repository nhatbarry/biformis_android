using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.Core.DesignPattern.SOAP.Events;
using CaptainPinkTurd.Core.Localization;
using CaptainPinkTurd.Scene;
using CaptainPinkTurd.Scene.Story;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story
{
    /// <summary>
    /// Main menu entry points for story mode: continue / new story, plus the language toggle.
    /// Also adds a level select (a dev tool for demos) to the menu's button column, built at runtime from the
    /// menu's own buttons so the scene doesn't change.
    /// </summary>
    public class StoryMenu : MonoBehaviour
    {
        //set by the editor's "play the opened level through Core" hook; read once here, in the main menu
        public const string EDITOR_PLAY_SCENE_KEY = "Biformis.PlaySceneThroughCore";
        public const string LEVEL_SELECT_BUTTON_NAME = "Level Select Button";

        [SerializeField] private StoryData storyData;
        [Tooltip("Raised before a story session starts so no HP is carried over from an earlier session")]
        [SerializeField] private VoidEvent onRestart;

        [Header("Panels")]
        [SerializeField] private GameObject mainPanel;
        [Tooltip("Shown instead of starting straight away when there is a story save to continue")]
        [SerializeField] private GameObject storyOptionsPanel;

        [Header("Dev")]
        [Tooltip("Level select button in the main menu, for testing and demos. Turn off for the release build.")]
        [SerializeField] private bool showLevelSelect = true;

        private const string STEP_NAME_KEY_PREFIX = "step.";
        //the level select goes right after the two play modes (Story, Endless)
        private const int LEVEL_SELECT_MENU_INDEX = 2;

        private GameObject levelSelectPanel;

        private IEnumerator Start()
        {
            //the save file is loaded on sceneLoaded, which fires after OnEnable; wait a frame to read it
            yield return null;
            ShowMainPanel();

            if (showLevelSelect) BuildLevelSelect();

#if UNITY_EDITOR
            string requested = UnityEditor.SessionState.GetString(EDITOR_PLAY_SCENE_KEY, "");
            if (requested.Length > 0)
            {
                UnityEditor.SessionState.EraseString(EDITOR_PLAY_SCENE_KEY);
                //the menu's own load transition may still be finishing, and a busy controller drops requests
                yield return new WaitUntil(() => !SceneController.Instance.IsBusy);
                PlayScene(requested);
            }
#endif
        }

        public void OpenStory()
        {
            if (storyData.HasProgress)
            {
                mainPanel.SetActive(false);
                storyOptionsPanel.SetActive(true);
                return;
            }

            StartNewStory();
        }

        public void ContinueStory()
        {
            onRestart.Raise();
            StoryFlow.Continue(storyData);
        }

        public void StartNewStory()
        {
            onRestart.Raise();
            StoryFlow.StartNew(storyData);
        }

        /// <summary>
        /// Starts the story at any step (Level Select). Overwrites the story save.
        /// </summary>
        public void StartAtStep(int step)
        {
            onRestart.Raise();
            StoryFlow.StartAt(storyData, step);
        }

        public void ShowMainPanel()
        {
            storyOptionsPanel.SetActive(false);
            if (levelSelectPanel) levelSelectPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        public void OpenLevelSelect()
        {
            mainPanel.SetActive(false);
            storyOptionsPanel.SetActive(false);
            levelSelectPanel.SetActive(true);
        }

        public void ToggleLanguage() => Localization.ToggleLanguage();

        /// <summary>
        /// Story scenes start at their story step; any other level loads the way the menu would load it.
        /// </summary>
        private void PlayScene(string sceneName)
        {
            for (int i = 0; i < storyData.Steps.Count; i++)
            {
                if (storyData.Steps[i].sceneName != sceneName) continue;
                StartAtStep(i);
                return;
            }

            onRestart.Raise();
            SceneController.Instance.NewTransition()
                .Load(SceneDatabase.Slots.Session, SceneDatabase.Scenes.Session)
                .Load(SceneDatabase.Slots.SessionContent, sceneName, true)
                .Unload(SceneDatabase.Slots.Menu)
                .WithOverlay()
                .Perform();
        }

        // ------------------------------------------------------------------ level select (dev tool)

        private void BuildLevelSelect()
        {
            var column = MenuColumn();
            var template = column[0];
            var canvas = mainPanel.transform.parent;

            //joins the menu's button column: same spacing, the bottom button stays put and the column grows upward
            float spacing = column.Count > 1 ? Y(column[0]) - Y(column[1]) : 32f;
            float bottom = Y(column[column.Count - 1]);
            int index = Mathf.Min(LEVEL_SELECT_MENU_INDEX, column.Count);
            var levelSelectButton = CloneButton(template, mainPanel.transform, LEVEL_SELECT_BUTTON_NAME, "menu.level_select", Width(template), OpenLevelSelect);
            levelSelectButton.transform.SetSiblingIndex(column[index - 1].transform.GetSiblingIndex() + 1);
            column.Insert(index, levelSelectButton);
            for (int i = 0; i < column.Count; i++)
            {
                var rect = (RectTransform)column[i].transform;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, bottom + (column.Count - 1 - i) * spacing);
            }

            levelSelectPanel = new GameObject("Level Select", typeof(RectTransform));
            var panelRect = (RectTransform)levelSelectPanel.transform;
            panelRect.SetParent(canvas, false);
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;

            //three columns of story steps, top to bottom
            for (int i = 0; i < storyData.Steps.Count; i++)
            {
                int step = i;
                var stepButton = CloneButton(template, panelRect, $"Step {i}", StepNameKey(i), 80f, () => StartAtStep(step));
                ((RectTransform)stepButton.transform).anchoredPosition = new Vector2(-170f + (i % 3) * 170f, 105f - (i / 3) * 36f);
            }
            var back = CloneButton(template, panelRect, "Back", "menu.back", 80f, ShowMainPanel);
            ((RectTransform)back.transform).anchoredPosition = new Vector2(0f, -90f);

            levelSelectPanel.SetActive(false);
        }

        /// <summary>
        /// The main menu's own buttons, top to bottom.
        /// </summary>
        private List<Button> MenuColumn()
        {
            var column = new List<Button>();
            foreach (Transform child in mainPanel.transform)
            {
                if (child.TryGetComponent(out Button button)) column.Add(button);
            }
            column.Sort((a, b) => Y(b).CompareTo(Y(a)));
            return column;
        }

        private static float Y(Button button) => ((RectTransform)button.transform).anchoredPosition.y;

        private static float Width(Button button) => ((RectTransform)button.transform).sizeDelta.x;

        private string StepNameKey(int step)
        {
            //steps without a name in the table fall back to their scene (and knot) so a reordered story still reads
            string key = STEP_NAME_KEY_PREFIX + step;
            return Localization.TryGet(key, out _) ? key : $"{storyData.Steps[step].sceneName} {storyData.Steps[step].knotName}".Trim();
        }

        private static Button CloneButton(Button template, Transform parent, string name, string labelKey, float width, UnityAction onClick)
        {
            var go = Instantiate(template.gameObject, parent);
            go.name = name;
            go.SetActive(true);

            var button = go.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent(); //drop the template's persistent listeners
            button.onClick.AddListener(onClick);

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);

            var label = go.GetComponentInChildren<LocalizedText>(true);
            if (label) label.SetKey(labelKey);
            return button;
        }
    }
}
