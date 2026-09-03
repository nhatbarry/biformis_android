using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CaptainPinkTurd.Core.Rendering
{
    /// <summary>
    /// Keeps the camera showing the same width of the world it shows at the 16:9 the game is designed for,
    /// cropping the top and bottom instead on taller screens.
    /// </summary>
    /// <remarks>
    /// An orthographic camera fixes the *vertical* extent, so on a phone's 20:9 screen the levels were showing
    /// about a quarter more world horizontally than intended, revealing what sits outside the map bounds.
    /// This trades that away for a little vertical crop: no black bars, no seeing past the level, and the
    /// horizontal framing matches the desktop build exactly.
    ///
    /// It writes to the Camera rather than to Cinemachine, from a LateUpdate ordered to run after everything
    /// else. Levels are driven by a CinemachineBrain that pushes the virtual camera's lens onto the Camera
    /// every LateUpdate, so this is the one place the value is guaranteed to survive to the render, and it
    /// works identically in the tutorial levels, which have no Cinemachine at all.
    ///
    /// This is a no-op at 16:9, so the desktop build is unaffected unless it runs ultra-wide, where the same
    /// correction applies for the same reason.
    /// </remarks>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public class CameraFraming : MonoBehaviour
    {
        private const float DesignAspect = 16f / 9f;

        /// <summary>The size each camera was authored with, so repeated passes never compound the shrink.</summary>
        private readonly Dictionary<Camera, float> authoredSizes = new();

        private readonly List<Camera> targets = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoSpawn()
        {
            if (FindAnyObjectByType<CameraFraming>(FindObjectsInactive.Include)) return;

            var host = new GameObject(nameof(CameraFraming));
            DontDestroyOnLoad(host);
            host.AddComponent<CameraFraming>();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            RefreshTargets();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => RefreshTargets();

        private void LateUpdate()
        {
            if (Screen.height <= 0) return;

            float aspect = (float)Screen.width / Screen.height;

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                var camera = targets[i];
                if (!camera)
                {
                    targets.RemoveAt(i);
                    continue;
                }

                if (!camera.orthographic) continue;

                camera.orthographicSize = SizeFor(camera, aspect);
            }
        }

        private void RefreshTargets()
        {
            targets.Clear();
            targets.AddRange(FindObjectsByType<Camera>(FindObjectsSortMode.None));

            Prune();
        }

        private float SizeFor(Camera camera, float aspect)
        {
            if (!authoredSizes.TryGetValue(camera, out float authored))
            {
                // First sight: whatever is on the camera now is the size the level was framed with. Read it
                // before we ever write, or the correction would feed on its own output.
                authored = camera.orthographicSize;
                authoredSizes[camera] = authored;
            }

            // authored * DesignAspect is the half-width the game was framed around. Never grow past the
            // authored size, so a screen narrower than 16:9 crops horizontally rather than revealing more.
            return Mathf.Min(authored, authored * DesignAspect / aspect);
        }

        /// <summary>Drops cameras that went away with an unloaded scene.</summary>
        private void Prune()
        {
            if (authoredSizes.Count == 0) return;

            var dead = new List<Camera>();
            foreach (var key in authoredSizes.Keys)
            {
                if (!key) dead.Add(key);
            }

            foreach (var key in dead) authoredSizes.Remove(key);
        }
    }
}
