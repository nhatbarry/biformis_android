#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using CaptainPinkTurd.InkDialogue;
using CaptainPinkTurd.Story.Cutscene;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

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
        /// At the end of Level 4 the player walks B up to A and takes the box. On a phone: hold a finger where B should
        /// go, then tap once B stands next to A. A tap while B is still far away does nothing.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator TheBoxIsWalkedToAndTakenOnATouchScreen()
        {
            LogAssert.ignoreFailingMessages = true;
            var data = UnityEditor.AssetDatabase.LoadAssetAtPath<Scene.Story.StoryData>("Assets/Game Data/Story/Story Data.asset");
            int step = 0;
            while (data.Steps[step].knotName != "Level4_End") step++;
            SceneManager.LoadScene("Core");
            yield return WaitFor(() => GameObject.Find(StoryMenu.LEVEL_SELECT_BUTTON_NAME) != null, 40f, "main menu");
            yield return WaitFor(() => !Scene.SceneController.Instance.IsBusy, 10f, "menu transition");
            Object.FindAnyObjectByType<StoryMenu>().StartAtStep(step);
            yield return WaitFor(() => DialogueManager.HasInstance && DialogueManager.Instance.DialogueIsPlaying, 40f, "the end of Level 4");
            linesShown = 0;
            DialogueManager.Instance.OnDisplayDialogue.Subscribe(OnLine);

            var stage = Object.FindAnyObjectByType<CutsceneStage>();
            yield return WaitFor(() => stage.IsReaching, 15f, "B to wait for the player");
            var a = stage.ReachTarget;
            var b = (RectTransform)a.parent.Find("B4");
            var box = (RectTransform)a.parent.Find("Box");

            Tap(new Vector2(Screen.width * 0.95f, Screen.height / 2f), "while B is far from A");
            yield return null;
            Assert.IsTrue(stage.IsReaching, "a tap while B is far from A took the box");

            //hold a finger on A: B walks up to A, turns to it, and stops before walking into it
            var finger = Press(RectTransformUtility.WorldToScreenPoint(null, a.position), "on A");
            yield return WaitFor(() => b.anchoredPosition.x - a.anchoredPosition.x <= 76f, 6f, "B to walk up to A");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.GreaterOrEqual(b.anchoredPosition.x - a.anchoredPosition.x, 59.9f, "B walked into A");
            Assert.Less(b.localScale.x, 0f, "B doesn't face A");
            ExecuteEvents.Execute(finger.pointerPress, finger, ExecuteEvents.pointerUpHandler);

            yield return WaitFor(() => GameObject.Find("Take Prompt") != null, 2f, "the take prompt over B");
            Tap(new Vector2(Screen.width / 2f, Screen.height / 2f), "next to A");
            yield return null;
            Assert.IsFalse(stage.IsReaching, "a tap with B next to A didn't take the box");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.Less(Vector2.Distance(box.anchoredPosition, b.anchoredPosition + new Vector2(-18f, 4f)), 0.5f, "the box isn't in B's hand");
            yield return WaitFor(() => linesShown > 0, 15f, "the first line of the past");
        }

        //a finger put down (and kept) on the stage, as the event system delivers it
        private static PointerEventData Press(Vector2 screenPoint, string where)
        {
            var eventSystem = EventSystem.current;
            var pointer = new PointerEventData(eventSystem) { position = screenPoint, pointerId = 0 };
            var hits = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, hits);
            Assert.IsNotEmpty(hits, $"a finger {where} hits nothing");
            var target = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject);
            Assert.IsNotNull(target, $"a finger {where} lands on {hits[0].gameObject.name}, which doesn't take presses");
            Assert.IsNotNull(target.GetComponent<StagePointer>(), $"a finger {where} goes to {target.name}, not the stage");
            pointer.pointerCurrentRaycast = pointer.pointerPressRaycast = hits[0];
            pointer.pointerPress = target;
            pointer.pressPosition = screenPoint;
            ExecuteEvents.Execute(target, pointer, ExecuteEvents.pointerDownHandler);
            return pointer;
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
            if (info.line != null) linesShown++;
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
