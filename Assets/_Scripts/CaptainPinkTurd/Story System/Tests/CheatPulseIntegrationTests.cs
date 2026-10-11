#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BulletHell;
using CaptainPinkTurd.BulletHell;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.Core.Utilities;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.MobileControls;
using CaptainPinkTurd.Scene;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    public sealed class CheatPulseIntegrationTests
    {
        private readonly List<string> exceptions = new();
        private readonly List<GameObject> owned = new();
        private bool oldIgnoreLogs;

        [SetUp]
        public void SetUp()
        {
            oldIgnoreLogs = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
        }

        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            CheatSettings.Enabled = false;
            HitStop.Abort();
            Time.timeScale = 1f;
            foreach (var root in owned) if (root) Object.DestroyImmediate(root);
            LogAssert.ignoreFailingMessages = oldIgnoreLogs;
            Assert.IsEmpty(exceptions, string.Join("\n\n", exceptions));
        }

        [UnityTest, Timeout(90000)]
        public IEnumerator RealEnemiesAndBossLoseOneHealthAndTheMapBoundaryAbsorbsTheWave()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 6");
            yield return StoryTestLoading.WaitFor(() => !SceneController.Instance.IsBusy && !HitStop.IsWaiting &&
                Time.timeScale > 0f, 10f, "level startup and transition");
            var player = Object.FindAnyObjectByType<PlayerUnit>();
            var boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
            Assert.IsNotNull(player);
            Assert.IsNotNull(boss);
            boss.StopAllCoroutines();
            boss.GetComponent<BossRoaming>().enabled = false;
            boss.GetComponent<BossHazards>().ClearHazards();
            var arena = Object.FindAnyObjectByType<BossArenaController>();
            Vector2 origin = arena.PlayArea.center;
            player.rb.position = origin;
            player.transform.position = player.rb.position;
            player.rb.linearVelocity = Vector2.zero;
            boss.transform.position = origin + Vector2.up * 2.5f;
            boss.GetComponent<Rigidbody2D>().position = boss.transform.position;
            yield return null;
            var left = Spawn("Reaper", origin + Vector2.left * 5f);
            var right = Spawn("Axion", origin + Vector2.right * 5f);
            if (!Object.FindAnyObjectByType<MobileControlsHUD>())
                owned.Add(new GameObject("Cheat Capture Touch HUD", typeof(MobileControlsHUD)));
            CheatSettings.Enabled = true;
            yield return null;
            var skull = Object.FindAnyObjectByType<CheatSkullGraphic>();
            Assert.IsNotNull(skull, "the enabled cheat must expose its skull artwork");
            Assert.AreEqual(1f, skull.transform.parent.GetComponent<CanvasGroup>().alpha);

            int playerHp = player.GetComponent<CaptainPinkTurd.UnitSystem.UnitHealth>().CurrentHealth;
            int leftHp = left.CurrentHealth, rightHp = right.CurrentHealth;
            int bossHp = boss.CurrentHealth;
            Set(boss, "nextDamageTime", Time.time + 1000f);
            Assert.IsTrue(boss.IsInvulnerable);
            var pulse = player.GetComponent<CheatEnergyPulse>();
            Assert.IsNotNull(pulse, "the real player must auto-attach its cheat action");
            Assert.IsTrue(pulse.TryActivate(), $"cheat={CheatSettings.Enabled}; time={Time.timeScale}; " +
                $"hp={playerHp}; scene={player.gameObject.scene.name}; active={player.isActiveAndEnabled}; " +
                $"constraints={player.rb.constraints}; cutscene={InteractPrompt.CutsceneControls}");
            Assert.AreEqual(leftHp - 1, left.CurrentHealth);
            Assert.AreEqual(rightHp - 1, right.CurrentHealth);
            Assert.AreEqual(bossHp - 1, boss.CurrentHealth, "cheat must bypass the boss's temporary hit cooldown");
            Assert.AreEqual(playerHp, player.GetComponent<CaptainPinkTurd.UnitSystem.UnitHealth>().CurrentHealth);
            Assert.Less(player.rb.linearVelocity.sqrMagnitude, 0.01f, "a remote hit must not recoil the caster");

            yield return StoryTestLoading.WaitFor(() => !HitStop.IsWaiting, 5f, "pulse impact hit-stop");
            var visual = Object.FindAnyObjectByType<CheatPulseVisual>();
            yield return StoryTestLoading.WaitFor(() => visual.GetComponent<MeshFilter>().sharedMesh.bounds.size.y > 4.5f,
                5f, "visible expanding energy ring");
            yield return Capture("cheat-energy-wave.png");
            foreach (var old in Object.FindObjectsByType<CheatPulseVisual>(FindObjectsSortMode.None))
                Object.DestroyImmediate(old.gameObject);

            // Interior geometry cannot shield an enemy from this map-wide cheat.
            var wall = new GameObject("Cheat Interior Test Wall", typeof(BoxCollider2D));
            SceneManager.MoveGameObjectToScene(wall, player.gameObject.scene);
            owned.Add(wall);
            wall.transform.position = origin + Vector2.right * 2f;
            wall.GetComponent<BoxCollider2D>().size = new Vector2(0.4f, 10f);
            leftHp = left.CurrentHealth; rightHp = right.CurrentHealth; bossHp = boss.CurrentHealth;
            Set(boss, "nextDamageTime", Time.time + 1000f);
            Assert.IsTrue(pulse.TryActivate());
            Assert.AreEqual(leftHp - 1, left.CurrentHealth);
            Assert.AreEqual(rightHp - 1, right.CurrentHealth, "the energy covers enemies behind interior geometry too");
            Assert.AreEqual(bossHp - 1, boss.CurrentHealth);
            yield return StoryTestLoading.WaitFor(() => !HitStop.IsWaiting, 5f, "second pulse impact");
            visual = Object.FindAnyObjectByType<CheatPulseVisual>();
            yield return StoryTestLoading.WaitFor(() => Get<float[]>(visual, "absorbedAt")[0] >= 0f,
                5f, "map-boundary absorption sparks");
            float[] edgeDistances = Get<float[]>(visual, "walls");
            Assert.AreEqual(arena.PlayArea.xMax - player.transform.position.x, edgeDistances[0], 0.01f,
                "the wave stops at the arena edge, rather than the temporary interior wall");
            yield return Capture("cheat-wall-absorption.png");
            Assert.AreEqual(playerHp, player.GetComponent<CaptainPinkTurd.UnitSystem.UnitHealth>().CurrentHealth);
        }

        private LunebladeEnemy Spawn(string name, Vector2 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/Luneblade/{name}.prefab");
            var root = Object.Instantiate(prefab, position, Quaternion.identity);
            owned.Add(root);
            var enemy = root.GetComponent<LunebladeEnemy>();
            enemy.StopAllCoroutines();
            var body = root.GetComponent<Rigidbody2D>();
            body.linearVelocity = Vector2.zero;
            body.constraints |= RigidbodyConstraints2D.FreezePosition;
            foreach (var emitter in root.GetComponentsInChildren<ProjectileEmitterBiformis>(true))
            {
                emitter.AutoFire = false;
                emitter.enabled = false;
            }
            return enemy;
        }

        private static IEnumerator Capture(string filename)
        {
            const int width = 1280, height = 720;
            var camera = Camera.main;
            var target = RenderTexture.GetTemporary(width, height, 24);
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var canvases = new List<(Canvas canvas, RenderMode mode, Camera camera, float distance)>();
            try
            {
                camera.targetTexture = target;
                foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                {
                    if (!canvas.enabled || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                    canvases.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
                yield return null;
                Canvas.ForceUpdateCanvases();
                var skull = Object.FindAnyObjectByType<CheatSkullGraphic>();
                var skullMesh = skull.canvasRenderer.GetMesh();
                Assert.IsNotNull(skull.canvasRenderer);
                Assert.IsFalse(skull.canvasRenderer.cull, "enabled skull must be rendered in gameplay");
                Assert.Greater(skullMesh.vertexCount, 0, "the skull must produce visible UI geometry");
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    image.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
                    image.Apply();
                    Directory.CreateDirectory("Logs");
                    File.WriteAllBytes(Path.Combine("Logs", filename), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
            finally
            {
                foreach (var (canvas, mode, oldCamera, distance) in canvases)
                {
                    if (!canvas) continue;
                    canvas.renderMode = mode;
                    canvas.worldCamera = oldCamera;
                    canvas.planeDistance = distance;
                }
                camera.targetTexture = oldTarget;
                RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void Set(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string name)
            => (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Exception && (stack.Contains("Cheat") || stack.Contains("BiformisEmitter") ||
                stack.Contains("PlagueDoctorBoss") || stack.Contains("LunebladeEnemy"))) exceptions.Add(condition + "\n" + stack);
        }
    }
}
#endif
