#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using CaptainPinkTurd.Core.Rendering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// CameraFraming keeps the world width the game was framed for at 16:9 on wider phones (20:9 and up), cropping
    /// top and bottom instead. It was once deleted by accident and phones showed empty bands of background past the
    /// map on both sides; this fails if it's gone or no longer running.
    /// </summary>
    public class CameraFramingTests
    {
        [UnityTest]
        public IEnumerator A20By9PhoneSeesTheSameWorldWidthAs16By9()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 1");
            yield return null;

            var framing = Object.FindAnyObjectByType<CameraFraming>();
            Assert.IsNotNull(framing, "CameraFraming is not running");

            var cam = Camera.main;
            var sizeFor = typeof(CameraFraming).GetMethod("SizeFor", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(sizeFor, "CameraFraming.SizeFor(Camera, float) not found");

            float authoredHalfWidth = (float)sizeFor.Invoke(framing, new object[] { cam, 16f / 9f }) * 16f / 9f;
            foreach (float aspect in new[] { 20f / 9f, 2400f / 1080f, 21f / 9f })
            {
                float size = (float)sizeFor.Invoke(framing, new object[] { cam, aspect });
                Assert.AreEqual(authoredHalfWidth, size * aspect, 0.001f, $"at aspect {aspect:F2} the camera shows a different world width");
            }
        }

        [UnityTest]
        public IEnumerator BossArenaKeepsItsFloorDepthOnWidePhones()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 6");
            yield return null;
            yield return null;
            var framing = Object.FindAnyObjectByType<CameraFraming>();
            var sizeFor = typeof(CameraFraming).GetMethod("SizeFor", BindingFlags.Instance | BindingFlags.NonPublic);
            var arena = Object.FindAnyObjectByType<CaptainPinkTurd.Game.Enemy.BossArenaController>();
            var camera = Camera.main;
            float height = (float)sizeFor.Invoke(framing, new object[] { camera, 16f / 9f });
            foreach (float aspect in new[] { 20f / 9f, 21f / 9f, 2.4f })
            {
                camera.aspect = aspect;
                Assert.AreEqual(height, (float)sizeFor.Invoke(framing, new object[] { camera, aspect }), 0.001f,
                    "wide phones must not crop the arena into a horizontal strip");
                Assert.Greater(arena.PlayArea.height, 8f);
            }
        }
    }
}
#endif
