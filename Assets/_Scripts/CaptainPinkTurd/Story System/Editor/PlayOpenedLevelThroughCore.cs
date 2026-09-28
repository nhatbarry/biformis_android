using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CaptainPinkTurd.Story.Editor
{
    /// <summary>
    /// Pressing Play with a level open normally runs it without the Core scene, so its managers are auto-generated
    /// half-configured and nothing works. With this on (menu Biformis > Play Opened Level Through Core, on by
    /// default), Play starts from Core instead and the main menu jumps straight into the opened level: story scenes
    /// start at their story step, any other "Level ..." scene loads as the menu would load it.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayOpenedLevelThroughCore
    {
        private const string MENU_PATH = "Biformis/Play Opened Level Through Core";
        private const string ENABLED_PREF = "Biformis.PlayOpenedLevelThroughCore";
        private const string CORE_SCENE = "Assets/Scenes/CaptainPinkTurd/Bootstrap Scenes/Core.unity";

        static PlayOpenedLevelThroughCore()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(ENABLED_PREF, true);
            set => EditorPrefs.SetBool(ENABLED_PREF, value);
        }

        [MenuItem(MENU_PATH)]
        private static void Toggle() => Enabled = !Enabled;

        [MenuItem(MENU_PATH, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MENU_PATH, Enabled);
            return true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    EditorSceneManager.playModeStartScene = null;
                    SessionState.EraseString(StoryMenu.EDITOR_PLAY_SCENE_KEY);

                    //only for playable levels: the test runner's own scenes, Core and the menu are left alone
                    string opened = EditorSceneManager.GetActiveScene().name;
                    if (!Enabled || !IsPlayableLevel(opened)) return;

                    SessionState.SetString(StoryMenu.EDITOR_PLAY_SCENE_KEY, opened);
                    EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(CORE_SCENE);
                    Debug.Log($"[Play Through Core] starting from Core, then into {opened}. Turn off in menu {MENU_PATH}.");
                    break;

                case PlayModeStateChange.EnteredEditMode:
                    EditorSceneManager.playModeStartScene = null;
                    break;
            }
        }

        private static bool IsPlayableLevel(string sceneName) =>
            sceneName.StartsWith("Level") || sceneName == "Story Cutscene" || sceneName == "Story Ending";
    }
}
