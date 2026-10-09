#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Core.Localization;
using CaptainPinkTurd.InkDialogue;
using CaptainPinkTurd.Story.Cutscene;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using UnityEditor;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// On a phone the only way through a cutscene is tapping the screen. Once, the panel switched its own pointer
    /// input off after every line (a leftover of the choice buttons), so from the second line on nothing advanced it,
    /// and the panel's own pieces swallowed taps meant for the tap area behind them. A line also moves on by itself.
    /// </summary>
    public class DialogueTouchTests
    {
        private int linesShown;
        private string saveBackup;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            saveBackup = Path.Combine(Path.GetTempPath(), "biformis_save_backup_" + Guid.NewGuid().ToString("N"));
            Copy(Application.persistentDataPath, saveBackup);
        }

        [TearDown]
        public void TearDown()
        {
            if (DialogueManager.HasInstance) DialogueManager.Instance.OnDisplayDialogue.Unsubscribe(OnLine);
            foreach (var f in Directory.GetFiles(Application.persistentDataPath, "*", SearchOption.AllDirectories)) File.Delete(f);
            Copy(saveBackup, Application.persistentDataPath);
            Directory.Delete(saveBackup, true);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TappingAdvancesEveryLineAndIdleLinesMoveOnByThemselves()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return OpenIntro();
            var manager = DialogueManager.Instance;

            //tap on the panel itself, then outside it, each time on a line that has finished typing
            foreach (string where in new[] { "panel", "above the panel" })
            {
                yield return WaitFor(() => !manager.DialogueIsTyping, 20f, "the line to finish typing");
                int before = linesShown;
                Tap(where == "panel" ? PanelCentre() : new Vector2(Screen.width / 2f, Screen.height * 0.85f), where);
                yield return WaitFor(() => linesShown > before, 3f, $"a tap {where} to show the next line (line {before})");
            }

            //then leave it alone: the line moves on after its reading time
            yield return WaitFor(() => !manager.DialogueIsTyping, 20f, "the line to finish typing");
            int idleFrom = linesShown;
            yield return WaitFor(() => linesShown > idleFrom, 15f, "an untouched line to move on by itself");
        }

        /// <summary>
        /// At the end of Level 4 the player walks B up to A and takes the box. On a phone the touch HUD comes up with
        /// just its joystick and "!" button: steer B to A, and the button lights up; pressing it takes the box. A tap on
        /// the stage, or the button while B is still far away, does nothing.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator TheBoxIsWalkedToAndTakenWithTheTouchControls()
        {
            LogAssert.ignoreFailingMessages = true;
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<Scene.Story.StoryData>("Assets/Game Data/Story/Story Data.asset");
            int step = 0;
            while (data.Steps[step].knotName != "Level4_End") step++;
            SceneManager.LoadScene("Core");
            yield return WaitFor(() => GameObject.Find(StoryMenu.LEVEL_SELECT_BUTTON_NAME) != null, 40f, "main menu");
            TouchHud.Ensure();
            yield return WaitFor(() => !Scene.SceneController.Instance.IsBusy, 10f, "menu transition");
            Assert.IsNull(TouchHud.Find("Move Zone"), "the touch controls show in the menu");
            Object.FindAnyObjectByType<StoryMenu>().StartAtStep(step);
            yield return WaitFor(() => DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsPlaying, 40f, "the end of Level 4");
            linesShown = 0;
            DialogueManager.Instance.OnDisplayDialogue.Subscribe(OnLine);

            var stage = Object.FindAnyObjectByType<CutsceneStage>();
            yield return WaitFor(() => stage.IsReaching, 15f, "B to wait for the player");
            var a = stage.ReachTarget;
            var b = (RectTransform)a.parent.Find("B4");
            var box = (RectTransform)a.parent.Find("Box");
            yield return null;

            CollectionAssert.AreEquivalent(new[] { "Move Zone", "Interact" },
                TouchHud.Shown("Move Zone", "Interact", "Run And Dash", "Switch Dimension", "Pause"), "the touch controls shown for walking B");
            Assert.AreEqual("red_disabled", TouchHud.Clip("Interact"), "the button is lit while B is far from A");

            Tap(new Vector2(Screen.width * 0.95f, Screen.height / 2f), "while B is far from A");
            yield return TouchHud.Tap("Interact");
            Assert.IsTrue(stage.IsReaching, "a tap or the button while B is far from A took the box");

            //push the joystick towards A: B walks up to it, turns to it, and stops before walking into it
            var thumb = TouchHud.PushStick(Vector2.left);
            yield return WaitFor(() => b.anchoredPosition.x - a.anchoredPosition.x <= 76f, 6f, "B to walk up to A");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.GreaterOrEqual(b.anchoredPosition.x - a.anchoredPosition.x, 59.9f, "B walked into A");
            Assert.Less(b.localScale.x, 0f, "B doesn't face A");
            TouchHud.LetGoOfStick(thumb);
            Assert.AreEqual("red_ready", TouchHud.Clip("Interact"), "the button doesn't light up with B next to A");

            Tap(new Vector2(Screen.width / 2f, Screen.height / 2f), "next to A");
            yield return null;
            Assert.IsTrue(stage.IsReaching, "a tap on the stage took the box: only the button should");

            yield return TouchHud.Tap("Interact");
            Assert.IsFalse(stage.IsReaching, "the lit button didn't take the box");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.Less(Vector2.Distance(box.anchoredPosition, b.anchoredPosition + new Vector2(-18f, 4f)), 0.5f, "the box isn't in B's hand");
            Assert.IsNull(TouchHud.Find("Move Zone"), "the touch controls stay up after the box is taken");
            yield return WaitFor(() => linesShown > 0, 15f, "the first line of the past");
        }

        /// <summary>
        /// The brothers' memory after the end of Level 4: A holds out a hand first, and only then is the player handed B,
        /// to walk up with the joystick and take the hand with the lit "!" button. The drowning's lines stand at the top,
        /// the joined hands get their close-up, and the story moves on to the corridor.
        /// </summary>
        [UnityTest, Timeout(240000)]
        public IEnumerator TheBrothersHandsAreJoinedWithTheTouchControls()
        {
            LogAssert.ignoreFailingMessages = true;
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<Scene.Story.StoryData>("Assets/Game Data/Story/Story Data.asset");
            int step = 0;
            while (data.Steps[step].knotName != "Level4_End") step++;
            SceneManager.LoadScene("Core");
            yield return WaitFor(() => GameObject.Find(StoryMenu.LEVEL_SELECT_BUTTON_NAME) != null, 40f, "main menu");
            TouchHud.Ensure();
            yield return WaitFor(() => !Scene.SceneController.Instance.IsBusy, 10f, "menu transition");
            Object.FindAnyObjectByType<StoryMenu>().StartAtStep(step);
            yield return WaitFor(() => DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsPlaying, 40f, "the end of Level 4");

            var manager = DialogueManager.Instance;
            var stage = Object.FindAnyObjectByType<CutsceneStage>();
            var panel = (RectTransform)Object.FindAnyObjectByType<DialoguePanelUI>().transform;
            bool drowningLineAtTop = false, drowningLineAtBottom = false;
            float end = Time.realtimeSinceStartup + 150f;
            //read on (the box is taken on the way) until B is handed over for the hands
            while (!(stage.IsReaching && stage.ReachWalker.name == "B_FB"))
            {
                Assert.Less(Time.realtimeSinceStartup, end, "the brothers' hands never came");
                if (stage.IsReaching) yield return TouchHud.TakeTheBox(stage);
                var drown = GameObject.Find("Drown");
                if (drown && !manager.IsStaging && manager.DialogueIsPlaying)
                {
                    drowningLineAtTop |= panel.pivot.y > 0.5f;
                    drowningLineAtBottom |= panel.pivot.y < 0.5f;
                }
                manager.RequestContinue();
                yield return null;
                yield return null;
            }
            Assert.IsTrue(drowningLineAtTop && !drowningLineAtBottom, "the drowning's lines should stand at the top, off A");
            Assert.Less(panel.pivot.y, 0.5f, "the panel goes back to the bottom after the drowning");
            var cut = GameObject.Find("Pixel Cut");
            Assert.IsTrue(!cut || cut.transform.childCount == 0, "a pixel cut left its frozen pictures behind");

            var a = stage.ReachTarget;
            var b = stage.ReachWalker;
            yield return null; //the HUD draws its buttons in its own Update
            Assert.AreEqual("A_FB", a.name);
            Assert.AreEqual("wait", a.GetComponent<StageActorAnimation>().CurrentClip, "A should hold out a hand before B moves");
            Assert.AreEqual("red_disabled", TouchHud.Clip("Interact"), "the button is lit while B is far from A");
            yield return TouchHud.Tap("Interact");
            Assert.IsTrue(stage.IsReaching, "the button took the hand while B was far from A");

            yield return TouchHud.TakeTheBox(stage);
            Assert.IsFalse(stage.IsReaching, "the lit button didn't take the hand");
            var label = new SerializedObject(Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(text => text.name == "Take Prompt"));
            Assert.AreEqual("stage.hold", label.FindProperty("key").stringValue, "the prompt should say what B does");
            Assert.AreEqual("stage.hold_touch", label.FindProperty("touchKey").stringValue, "the prompt should say what B does");

            var closeUp = a.parent.Find("CloseUp");
            yield return WaitFor(() => closeUp.gameObject.activeInHierarchy, 5f, "the close-up of the joined hands");
            Assert.AreEqual(48f, a.anchoredPosition.x - b.anchoredPosition.x, 0.01f, "B should stand where the two hands meet");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Level Story Corridor", 30f, "the corridor after the memory");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TheEndingIsLeftThroughTheDoorOfLightWithTheTouchControls()
        {
            LogAssert.ignoreFailingMessages = true;
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<Scene.Story.StoryData>("Assets/Game Data/Story/Story Data.asset");
            int step = 0;
            while (data.Steps[step].knotName != "Ending") step++;
            SceneManager.LoadScene("Core");
            yield return WaitFor(() => GameObject.Find(StoryMenu.LEVEL_SELECT_BUTTON_NAME) != null, 40f, "main menu");
            TouchHud.Ensure();
            yield return WaitFor(() => !Scene.SceneController.Instance.IsBusy, 10f, "menu transition");
            Object.FindAnyObjectByType<StoryMenu>().StartAtStep(step);
            yield return WaitFor(() => DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsPlaying, 40f, "the ending");

            var manager = DialogueManager.Instance;
            var stage = Object.FindAnyObjectByType<CutsceneStage>();
            var speakers = new List<string>();
            void Heard(DialogueInfo info)
            {
                if (info.line != null) speakers.Add(info.speaker);
            }
            manager.OnDisplayDialogue.Subscribe(Heard);
            bool sawSplit = false, sawDust = false, brothersOnScreen = true;
            var screen = (RectTransform)stage.GetComponentInParent<Canvas>().rootCanvas.transform;
            float end = Time.realtimeSinceStartup + 90f;
            try
            {
                while (!stage.IsReaching)
                {
                    Assert.Less(Time.realtimeSinceStartup, end, "B was never handed over to walk to the door");
                    var merged = GameObject.Find("ABMerged");
                    sawSplit |= merged && merged.GetComponent<StageActorAnimation>().CurrentClip == "split";
                    var boss = GameObject.Find("BossEnd");
                    sawDust |= boss && boss.GetComponent<StageActorAnimation>().CurrentClip == "dust";
                    //once apart, the brothers stay in view all the way (their x is the world's, not its picture's centre)
                    foreach (var name in new[] { "A_End", "B_End" })
                    {
                        var brother = GameObject.Find(name);
                        if (brother) brothersOnScreen &= OnScreen((RectTransform)brother.transform, screen);
                    }
                    manager.RequestContinue();
                    yield return null;
                    yield return null;
                }
            }
            finally { manager.OnDisplayDialogue.Unsubscribe(Heard); }

            CollectionAssert.AreEqual(Enumerable.Range(0, 10).Select(i => i % 2 == 0 ? "Villain" : "AB").Append("A"), speakers,
                "the boss and A&B take turns, then A calls B home");
            Assert.IsTrue(sawSplit, "A&B should flicker apart");
            Assert.IsTrue(sawDust, "the boss should turn to dust");
            Assert.IsTrue(brothersOnScreen, "A and B left the screen after they split");
            Assert.IsNull(GameObject.Find("BossEnd"), "the boss is gone before the brothers run");

            //the run ends in the cage room: A waits by the door, B is the player's to walk
            var world = (RectTransform)GameObject.Find("EndWorld").transform;
            var a = stage.ReachTarget;
            var b = stage.ReachWalker;
            Assert.AreEqual("A_End", a.name);
            Assert.AreEqual("B_End", b.name);
            Assert.AreEqual(0f, world.anchoredPosition.x, 0.5f, "the run should stop with the cage room on screen");
            Assert.Greater(b.anchoredPosition.x, a.anchoredPosition.x + 100f, "B should start well away from A and the door");

            yield return null; //the HUD draws its buttons in its own Update
            Assert.AreEqual("red_disabled", TouchHud.Clip("Interact"), "the button is lit while B is far from the door");
            yield return TouchHud.Tap("Interact");
            Assert.IsTrue(stage.IsReaching, "the button opened the door while B was far from it");

            float bOnScreen = world.anchoredPosition.x + b.anchoredPosition.x;
            yield return TouchHud.TakeTheBox(stage);
            Assert.IsFalse(stage.IsReaching, "the lit button didn't take B through the door");
            Assert.IsTrue(OnScreen(a, screen) && OnScreen(b, screen), "A and B should be in view at the door");
            Assert.Greater(world.anchoredPosition.x, 100f, "the camera should follow B along the room towards the door");
            Assert.AreEqual(bOnScreen, world.anchoredPosition.x + b.anchoredPosition.x, 4f, "B keeps its place on screen while the room slides");
            var label = new SerializedObject(Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .First(text => text.name == "Take Prompt"));
            Assert.AreEqual("stage.enter", label.FindProperty("key").stringValue, "the prompt should say B goes in");

            yield return WaitFor(() => SceneManager.GetActiveScene().name == "Story Ending", 30f, "the hospital after the door");
        }

        //the rect's centre lies inside the stage (the screen)
        private static bool OnScreen(RectTransform rect, RectTransform screen)
        {
            var corners = new Vector3[4];
            screen.GetWorldCorners(corners);
            var centre = rect.TransformPoint(rect.rect.center);
            return centre.x > corners[0].x && centre.x < corners[2].x && centre.y > corners[0].y && centre.y < corners[2].y;
        }

        private IEnumerator OpenIntro()
        {
            SceneManager.LoadScene("Core");
            yield return WaitFor(() => GameObject.Find(StoryMenu.LEVEL_SELECT_BUTTON_NAME) != null, 40f, "main menu");
            yield return WaitFor(() => !Scene.SceneController.Instance.IsBusy, 10f, "menu transition");
            Object.FindAnyObjectByType<StoryMenu>().StartAtStep(0);
            yield return WaitFor(() => DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsPlaying, 40f, "the intro cutscene");
            linesShown = 0;
            DialogueManager.Instance.OnDisplayDialogue.Subscribe(OnLine);

            //the intro opens with a struggle the player taps through before the first line: tap the screen like a finger
            var manager = DialogueManager.Instance;
            yield return WaitFor(() => manager.IsStaging, 10f, "the opening struggle");
            int taps = 0;
            while (linesShown == 0)
            {
                if (manager.IsStaging && GameObject.Find("Struggle Meter") != null)
                {
                    Tap(new Vector2(Screen.width / 2f, Screen.height / 2f), "during the struggle");
                    Assert.Less(++taps, 12, "the struggle doesn't end after its taps");
                }
                yield return new WaitForSecondsRealtime(0.2f);
            }
            Assert.GreaterOrEqual(taps, 6, "the struggle ended before the player tapped it through");
        }

        private void OnLine(DialogueInfo info)
        {
            if (info.line == null) return;
            linesShown++;
            var style = Object.FindAnyObjectByType<DialogueSpeakerStyle>(FindObjectsInactive.Include);
            if (!style) return;
            var settings = new SerializedObject(style);
            var portrait = settings.FindProperty("portraitFrame").objectReferenceValue as GameObject;
            Assert.IsNotNull(portrait);
            Assert.IsFalse(portrait.activeSelf, "dialogue must not reveal an avatar when the speaker changes");
            var blocks=settings.FindProperty("textBlocks");
            for(int i=0;i<blocks.arraySize;i++)
            {
                var block=blocks.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;
                Assert.Less(block.offsetMin.x,30f,"text must reclaim the portrait column");
            }
        }

        private static Vector2 PanelCentre()
        {
            var panel = Object.FindAnyObjectByType<DialoguePanelUI>();
            var back = (RectTransform)panel.transform.Find("Content Parent/Back Panel");
            var corners = new Vector3[4];
            back.GetWorldCorners(corners);
            return RectTransformUtility.WorldToScreenPoint(null, (corners[0] + corners[2]) / 2f);
        }

        /// <summary>
        /// What a finger does: raycast the UI at that point and click whatever is hit first.
        /// </summary>
        private static void Tap(Vector2 screenPoint, string where)
        {
            var eventSystem = EventSystem.current;
            Assert.IsNotNull(eventSystem, "no EventSystem");
            var pointer = new PointerEventData(eventSystem) { position = screenPoint };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, $"a tap {where} hits nothing: is the dialogue canvas' raycaster off?");

            var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.IsNotNull(target, $"a tap {where} lands on {hits[0].gameObject.name}, which doesn't take clicks");
            Assert.IsNotNull(target.GetComponent<DialogueTapToContinue>(), $"a tap {where} goes to {target.name}, not the dialogue");
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static IEnumerator WaitFor(Func<bool> condition, float timeoutSeconds, string what)
        {
            float end = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        private static void Copy(string source, string target)
        {
            Directory.CreateDirectory(target);
            if (!Directory.Exists(source)) return;
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string destination = Path.Combine(target, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(file, destination, true);
            }
        }
    }
}
#endif
