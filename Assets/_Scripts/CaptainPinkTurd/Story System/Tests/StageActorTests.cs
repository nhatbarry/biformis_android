#if UNITY_EDITOR
using CaptainPinkTurd.Story.Cutscene;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Tests
{
    public class StageActorTests
    {
        [Test]
        public void AnActorCastLaterStillLightsUpFully()
        {
            //the stage dims every actor in its Awake, before an actor that starts off stage has run its own Awake
            var go = new GameObject("Actor", typeof(RectTransform), typeof(Image));
            go.SetActive(false);
            var image = go.GetComponent<Image>();
            var actor = go.AddComponent<StageActor>();
            var so = new SerializedObject(actor);
            so.FindProperty("tintedGraphics").arraySize = 1;
            so.FindProperty("tintedGraphics").GetArrayElementAtIndex(0).objectReferenceValue = image;
            so.ApplyModifiedPropertiesWithoutUndo();

            try
            {
                actor.SetTint(new Color(0.55f, 0.55f, 0.6f, 1f));
                go.SetActive(true); //cast: Awake runs now
                actor.SetTint(Color.white); //speaking

                Assert.AreEqual(Color.white, image.color, "the actor kept the dimmed colour as its own");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
#endif
