#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Game;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.UnitSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Story.Tests
{
    public class BossPresentationTests
    {
        private PlagueDoctorBoss boss;
        private PlayerUnit player;
        private BossArenaController arena;
        private readonly List<string> exceptions = new();

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
        }
        [TearDown]
        public void TearDown()
        {
            Application.logMessageReceived -= OnLog;
            CaptainPinkTurd.Core.Utilities.HitStop.Abort();
            Time.timeScale = 1f;
            Assert.IsEmpty(exceptions, string.Join("\n", exceptions));
        }

        private IEnumerator Load()
        {
            yield return StoryTestLoading.LoadLevelThroughCore("Level Story 6");
            boss = Object.FindAnyObjectByType<PlagueDoctorBoss>();
            boss.GetComponent<BossRoaming>().enabled = false;
            var combat = new SerializedObject(boss.GetComponent<BossPhaseTwoCombat>());
            combat.FindProperty("automaticAttacks").boolValue = false;
            combat.ApplyModifiedProperties();
            player = Object.FindAnyObjectByType<PlayerUnit>();
            arena = Object.FindAnyObjectByType<BossArenaController>();
            var settings = new SerializedObject(boss);
            foreach (var property in new[] { "castWeight", "walkThrowWeight", "levitateWeight" })
                settings.FindProperty(property).floatValue = 0f;
            settings.ApplyModifiedProperties();
            yield return new WaitForSeconds(0.4f);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator FixedCameraContainsWalkingAndDashKnockbackWithoutWallsOrDoor()
        {
            yield return Load();
            var camera = Camera.main;
            Vector3 origin = camera.transform.position;
            float size = camera.orthographicSize;
            var thumb = TouchHud.PushStick(Vector2.right);
            yield return new WaitForSeconds(3f);
            TouchHud.LetGoOfStick(thumb);
            Assert.AreEqual(origin, camera.transform.position);
            Assert.AreEqual(size, camera.orthographicSize, 0.001f);
            Assert.Greater(player.transform.position.x, arena.PlayArea.xMax - 0.5f);
            foreach (var outside in new[]
            {
                new Vector2(-100f, -100f), new Vector2(100f, -100f),
                new Vector2(-100f, 100f), new Vector2(100f, 100f),
            })
            {
                player.rb.position = outside;
                player.rb.linearVelocity = outside * 5f; //dash/knockback can never leave the frame either
                yield return null;
                yield return null;
                Assert.IsTrue(arena.PlayArea.Contains(player.transform.position));
                Vector3 viewport = camera.WorldToViewportPoint(player.transform.position);
                Assert.That(viewport.x, Is.InRange(0.02f, 0.98f));
                Assert.That(viewport.y, Is.InRange(0.02f, 0.98f));
                Assert.AreEqual(origin, camera.transform.position);
            }
            Assert.IsNull(Object.FindAnyObjectByType<Door>());
            Assert.IsNull(Object.FindAnyObjectByType<BossArenaFloor>());
            player.rb.position = arena.ClampPosition((Vector2)boss.transform.position + Vector2.down * 3f);
            yield return new WaitForSeconds(1.5f); //let the title finish before the visual capture
            Capture("arena-hud-20x9", arena, 1280, 576);
            Capture("arena-hud-21x9", arena, 1260, 540);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator IceRevealZoomsPlaysMaskArtAndReturnsBeforePhaseTwoStarts()
        {
            yield return Load();
            var camera = Camera.main;
            Vector3 originalCamera = camera.transform.position;
            var originalPlayer = player.rb.position;
            var sprite = boss.GetComponent<SpriteRenderer>();
            var placeholder = boss.GetComponent<BossPhaseTwoCombat>();
            var settings = new SerializedObject(boss);
            var frames = settings.FindProperty("iceShatter.frames");
            var reveal = new HashSet<Sprite>(Enumerable.Range(0, frames.arraySize)
                .Select(i => (Sprite)frames.GetArrayElementAtIndex(i).objectReferenceValue));
            Assert.AreEqual(12, reveal.Count);
            var ui = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(canvas => canvas.enabled).ToArray();
            Assert.Greater(ui.Length, 2, "portrait, touch HUD and boss health must be present before the cinematic");
            Assert.IsNotNull(UnityEngine.InputSystem.Gamepad.current);
            int ready = 0;
            boss.OnPhaseTwoReady.Subscribe(() => ready++);
            boss.PreviewPhaseTwo();
            var thumb = TouchHud.PushStick(Vector2.right);
            yield return StoryTestLoading.WaitFor(() => arena.CurrentAuthoredSize < 6f, 5f, "zoom in");
            Assert.IsTrue(arena.IsPresentingPhaseTwo);
            Assert.IsTrue(ui.All(canvas => !canvas.enabled), "all UI must disappear during the zoom");
            Assert.IsNotNull(UnityEngine.InputSystem.Gamepad.current, "hiding controls must not remove their virtual device");
            Assert.IsNull(placeholder.CurrentAction);
            Assert.IsTrue(boss.IsInvulnerable);
            Time.timeScale = 0f;
            yield return null; //publish the final pre-pause lens in the camera's LateUpdate
            Vector3 pausedCamera = camera.transform.position;
            float pausedSize = arena.CurrentAuthoredSize;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(pausedCamera, camera.transform.position);
            Assert.AreEqual(pausedSize, arena.CurrentAuthoredSize);
            Time.timeScale = 1f;
            var seen = new HashSet<Sprite>();
            bool capturedAura = false;
            while (boss.Phase != PlagueDoctorBoss.EPhase.RedHaired)
            {
                if (reveal.Contains(sprite.sprite))
                {
                    seen.Add(sprite.sprite);
                    if (sprite.sprite == frames.GetArrayElementAtIndex(7).objectReferenceValue)
                    {
                        Assert.AreEqual(PlagueDoctorBoss.EPhase.Revealing, boss.Phase);
                        Assert.IsTrue(boss.IsInvulnerable, "aura cannot be interrupted by contact damage");
                        if (!capturedAura)
                        {
                            Capture("ice-aura-closeup", arena, 1280, 576);
                            capturedAura = true;
                        }
                    }
                }
                Assert.IsNull(placeholder.CurrentAction);
                Assert.AreEqual(originalPlayer, player.rb.position, "the cinematic must hold the player still");
                yield return null;
            }
            TouchHud.LetGoOfStick(thumb);
            Assert.AreEqual(12, seen.Count, "the entire transformation must play, including the brief aura");
            Assert.AreEqual(1, ready);
            Assert.IsFalse(arena.IsPresentingPhaseTwo);
            Assert.IsTrue(ui.Where(canvas => canvas).All(canvas => canvas.enabled), "UI must return after zoom out");
            Assert.AreEqual(6.5f, arena.CurrentAuthoredSize, 0.001f);
            yield return null;
            Assert.AreEqual(originalCamera, camera.transform.position);
            Assert.AreEqual(settings.FindProperty("phaseTwoRestingPose").objectReferenceValue, sprite.sprite);
            Assert.IsFalse(boss.IsInvulnerable);
            Assert.AreEqual("8/8", boss.GetComponent<BossHealthBar>().DisplayedHealth);
            Capture("phase-two-wide", arena, 1280, 576);
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator InterruptingTheCinematicRestoresUiAndPlayerConstraints()
        {
            yield return Load();
            var ui = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(canvas => canvas.enabled).ToArray();
            var raycasters = ui.Select(canvas => canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>())
                .Where(raycaster => raycaster && raycaster.enabled).ToArray();
            var constraints = player.rb.constraints;
            boss.PreviewPhaseTwo();
            Assert.IsTrue(ui.All(canvas => !canvas.enabled));
            Assert.IsTrue(raycasters.All(raycaster => !raycaster.enabled));
            arena.enabled = false;
            Assert.IsTrue(ui.All(canvas => canvas.enabled));
            Assert.IsTrue(raycasters.All(raycaster => raycaster.enabled));
            Assert.AreEqual(constraints, player.rb.constraints);
            yield return new WaitForSeconds(0.2f);
            Assert.IsTrue(ui.All(canvas => canvas.enabled), "the pending zoom routine must not hide UI again after interruption");
        }

        [UnityTest, Timeout(60000)]
        public IEnumerator PlayerSurvivesNineEnemyBulletsAndTheTenthKillsWithPortraitHealth()
        {
            yield return Load();
            player.OnColorChangeEvents(EColor.Red);
            var health = player.GetComponent<UnitHealth>();
            var bar = Object.FindAnyObjectByType<PlayerAvatarHealthBar>();
            var hazards = boss.GetComponent<BossHazards>();
            Assert.AreEqual(10, health.MaxHealth);
            Assert.AreEqual("10/10", bar.DisplayedHealth);
            int deaths = 0;
            health.OnDeath.Subscribe(_ => deaths++);
            for (int hit = 1; hit <= 10; hit++)
            {
                yield return StoryTestLoading.WaitFor(() => !health.IsInvincibilityFrameOn, 5f, "damage cooldown");
                var centre = player.GetComponentInChildren<CircleCollider2D>().bounds.center;
                hazards.FireProjectile(centre, Vector2.right, EColor.Blue, 1f);
                int remaining = 10 - hit;
                yield return StoryTestLoading.WaitFor(() => health.CurrentHealth == remaining, 5f, $"bullet {hit}");
                yield return null;
                Assert.AreEqual($"{remaining}/10", bar.DisplayedHealth);
                if (hit < 10)
                {
                    Assert.AreEqual(0, deaths);
                    Assert.IsTrue(player.gameObject.activeInHierarchy);
                }
                if (hit == 1) Capture("portrait-health-nine", arena, 1280, 576);
            }
            Assert.AreEqual(1, deaths);
            yield return StoryTestLoading.WaitFor(() => !player.gameObject.activeInHierarchy, 5f, "tenth-bullet death");
        }

        private static void Capture(string name, BossArenaController arena, int width, int height)
        {
            var camera = Camera.main;
            var oldTarget = camera.targetTexture;
            float oldAspect = camera.aspect, oldSize = camera.orthographicSize;
            var oldActive = RenderTexture.active;
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .Select(canvas => (canvas, canvas.worldCamera, canvas.planeDistance)).ToArray();
            var target = RenderTexture.GetTemporary(width, height, 24);
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.aspect = (float)width / height;
                camera.orthographicSize = Mathf.Min(arena.CurrentAuthoredSize,
                    arena.CurrentAuthoredSize * (16f / 9f) / camera.aspect);
                foreach (var (canvas, _, _) in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1f;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                Directory.CreateDirectory("Logs/boss-presentation-shots");
                File.WriteAllBytes($"Logs/boss-presentation-shots/{name}.png", image.EncodeToPNG());
            }
            finally
            {
                foreach (var (canvas, oldCamera, distance) in canvases)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    canvas.worldCamera = oldCamera;
                    canvas.planeDistance = distance;
                }
                camera.targetTexture = oldTarget;
                camera.aspect = oldAspect;
                camera.orthographicSize = oldSize;
                RenderTexture.active = oldActive;
                Canvas.ForceUpdateCanvases();
                RenderTexture.ReleaseTemporary(target);
                Object.Destroy(image);
            }
        }

        private void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Exception && (trace.Contains("Boss") || trace.Contains("Health") || trace.Contains("MobileControls")))
                exceptions.Add(message + "\n" + trace);
        }
    }
}
#endif
