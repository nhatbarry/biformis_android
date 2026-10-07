using System.Collections;
using CaptainPinkTurd.Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// The camera is confined to each level's Map Bound. When the view is wider or taller than that box, Cinemachine's
    /// confiner parks the camera in the box's centre for good: the corridor's bound was once 9 units wide, so its
    /// camera never moved and the player started off screen.
    /// </summary>
    public class StoryCameraTests
    {
        private static readonly string[] Levels =
        {
            "Level Story 1", "Level Story 2", "Level Story 3", "Level Story 4", "Level Story Corridor", "Level Story 5", "Level Story 6",
        };

        [SetUp]
        public void SetUp() => LogAssert.ignoreFailingMessages = true;

        [UnityTest]
        public IEnumerator TheCameraKeepsThePlayerInView([ValueSource(nameof(Levels))] string level)
        {
            yield return StoryTestLoading.LoadLevelThroughCore(level);
            yield return new WaitForSecondsRealtime(1.5f);

            var cam = Camera.main;
            var bound = GameObject.Find("Map Bound").GetComponent<BoxCollider2D>().bounds;
            //CameraFraming never lets the view get wider than 16:9 at the authored size, on any screen
            float viewHeight = 2f * cam.orthographicSize;
            float viewWidth = viewHeight * Mathf.Max(cam.aspect, 16f / 9f);
            Assert.GreaterOrEqual(bound.size.x, viewWidth, $"{level}: Map Bound is narrower than the camera view");
            Assert.GreaterOrEqual(bound.size.y, viewHeight, $"{level}: Map Bound is shorter than the camera view");

            var player = Object.FindAnyObjectByType<PlayerUnit>();
            Vector3 viewport = cam.WorldToViewportPoint(player.transform.position);
            Assert.IsTrue(viewport.x > 0.1f && viewport.x < 0.9f && viewport.y > 0.1f && viewport.y < 0.9f,
                $"{level}: the player is at viewport {viewport}, off screen");
        }
    }
}
