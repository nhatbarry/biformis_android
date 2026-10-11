#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Game.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace CaptainPinkTurd.Story.Tests
{
    /// <summary>
    /// Not a check: re-renders the pictures the intro and the white room cut to ("Next Scene - Level Story 1/3.png"),
    /// the live level around the player's start at one art pixel per pixel, without the player and the hints. Run it
    /// by name after changing what those levels look like there; it writes to Logs/Environment Snapshots, copy the
    /// files over the ones in Assets/Sprites/Story once they look right.
    /// </summary>
    [Explicit("writes pictures, run by name")]
    public class CutsceneFrameCapture
    {
        //the cutscene's 224 x 90 picture is centred this far above the player's start
        private static readonly Vector2 Offset = new(0f, 0.5f);

        [UnityTest]
        public IEnumerator CaptureLevelStartPictures([Values("Level Story 1", "Level Story 3")] string level)
        {
            yield return StoryTestLoading.LoadLevelThroughCore(level);
            yield return new WaitForSeconds(1f);

            var player = Object.FindAnyObjectByType<PlayerUnit>();
            foreach (var renderer in player.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Hint"))
                    foreach (var renderer in t.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            yield return null;

            Save(Capture((Vector2)player.transform.position + Offset, 90f / 32f, 224, 90), $"Next Scene - {level}.png");
        }

        //the levels the brothers run back through at the end ("Ending Run - Level Story N.png", 224 x 90 around the
        //player's start); Tools/ending_art.py world joins them with the cage room into the ending's EndWorld picture
        [UnityTest]
        public IEnumerator CaptureEndingRunPictures([Values("Level Story 2", "Level Story 3", "Level Story 4", "Level Story 5")] string level)
        {
            yield return StoryTestLoading.LoadLevelThroughCore(level);
            yield return new WaitForSeconds(1f);

            var player = Object.FindAnyObjectByType<PlayerUnit>();
            foreach (var renderer in player.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name.StartsWith("Hint"))
                    foreach (var renderer in t.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
            yield return null;

            Save(Capture((Vector2)player.transform.position + Offset, 90f / 32f, 224, 90), $"Ending Run - {level}.png");
        }

        //the whole level and the gameplay view at the start, for looking over the scenery
        [UnityTest]
        public IEnumerator CaptureLevelOverviews([Values("Level Story 1", "Level Story 2", "Level Story 3", "Level Story 4",
            "Level Story Corridor", "Level Story 5", "Level Story 6")] string level)
        {
            yield return StoryTestLoading.LoadLevelThroughCore(level);
            yield return new WaitForSeconds(1f);

            var player = Object.FindAnyObjectByType<PlayerUnit>();
            Save(Capture(player.transform.position, 6.5f, 1600, 900), $"{level} start.png");
            var walls = Object.FindObjectsByType<UnityEngine.Tilemaps.Tilemap>(FindObjectsSortMode.None)
                .First(map => map.name.Trim() == "Wall Art");
            var bounds = walls.localBounds;
            float size = Mathf.Max(bounds.extents.y, bounds.extents.x * 9f / 16f) * 0.8f;
            Save(Capture(bounds.center, size, 1600, 900), $"{level} whole.png");
        }

        private static Texture2D Capture(Vector2 centre, float orthographicSize, int width, int height)
        {
            var camera = new GameObject("Capture Camera").AddComponent<Camera>();
            camera.CopyFrom(Camera.main);
            camera.transform.position = new Vector3(centre.x, centre.y, -10f);
            camera.orthographicSize = orthographicSize;
            camera.aspect = (float)width / height;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var picture = new Texture2D(width, height, TextureFormat.RGB24, false);
            picture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            picture.Apply();
            RenderTexture.active = previous;
            Object.Destroy(camera.gameObject);
            target.Release();
            return picture;
        }

        private static void Save(Texture2D picture, string file)
        {
            var folder = Path.GetFullPath("Logs/Environment Snapshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, file), picture.EncodeToPNG());
            Object.Destroy(picture);
        }
    }
}
#endif
