using System;
using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.InkDialogue;
using CaptainPinkTurd.Scene;
using CaptainPinkTurd.Scene.Story;
using CaptainPinkTurd.Story.Cutscene;
using CaptainPinkTurd.TopDownControllerSystem;
using CaptainPinkTurd.UI.Popup;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// Level six's opening: the merged brothers run through a pipe, meet the masked boss, then watch him summon
    /// the already-loaded arena. One screen-space actor alternates forms without restarting either run timeline.
    /// The pipe is removed through a growing circular hole; there is no scene transition at the handover.
    /// </summary>
    [DefaultExecutionOrder(1000)] // after the actors' Awake, before their first Update/physics/render
    public sealed class BossEntranceSequence : MonoBehaviour
    {
        public enum EStage { WaitingForTransition, Running, BossAppearing, Dialogue, Casting, Revealing, Finished }

        [SerializeField] private Sprite pipeBackground;
        [SerializeField] private GameObject dialoguePrefab;
        [SerializeField] private Shader revealShader;
        [SerializeField] private AudioClip entranceMusic;
        [SerializeField] private StageActorAnimation.Clip redRun, blueRun, redIdle, blueIdle, bossIdle, bossCast;
        [SerializeField, Min(0.1f)] private float runSeconds = 2f;
        [SerializeField, Min(0.01f)] private float bossAppearanceSeconds = 0.6f;
        [SerializeField, Min(0.1f)] private float summonSeconds = 1.6f;
        [SerializeField, Min(0.01f)] private float flickerSeconds = 0.14f;

        private const float FloorY = -60f;
        private const float PlayerStagePixelSize = 4f;
        private const int StageOrder = 90;
        private static readonly int RevealCentre = Shader.PropertyToID("_RevealCentre");
        private static readonly int RevealRadius = Shader.PropertyToID("_RevealRadius");
        private static readonly int ScreenAspect = Shader.PropertyToID("_ScreenAspect");

        private readonly List<(Behaviour component, bool enabled)> behaviours = new();
        private readonly List<(Collider2D collider, bool enabled)> colliders = new();
        private readonly List<(Renderer renderer, bool hidden)> renderers = new();
        private readonly List<(Canvas canvas, GraphicRaycaster raycaster, bool raycasts)> hiddenUi = new();
        private readonly HashSet<Canvas> capturedUi = new();
        private PlagueDoctorBoss boss;
        private BossHazards hazards;
        private PlayerUnit player;
        private Camera arenaCamera;
        private SpriteRenderer bossRenderer;
        private Rigidbody2D playerBody;
        private RigidbodyConstraints2D playerConstraints;
        private Vector2 playerPosition;
        private LevelTitleCard title;
        private bool titleWasEnabled, gated, closing;
        private RectTransform stage;
        private RawImage pipe;
        private Image mergedActor, bossActor;
        private Material revealMaterial;
        private GameObject dialogueInstance;
        private DialogueManager dialogue;
        private Coroutine routine;
        private float animationElapsed, runElapsed, idleElapsed, castElapsed;
        private float bossStageUnitsPerWorld;
        private float revealRadiusLimit;
        private AudioSource sfxSource;

        public bool IsPlaying { get; private set; }
        public bool Finished { get; private set; }
        public EStage CurrentStage { get; private set; } = EStage.WaitingForTransition;
        public float RevealProgress { get; private set; }

        private void Awake()
        {
            if (!StoryFlow.IsStoryRunning || StoryFlow.IsReloadingCurrentStep)
            {
                Finished = true;
                CurrentStage = EStage.Finished;
                return;
            }

            boss = FindAnyObjectByType<PlagueDoctorBoss>();
            player = FindAnyObjectByType<PlayerUnit>();
            arenaCamera = Camera.main;
            if (!boss || !player || !arenaCamera || !pipeBackground || !dialoguePrefab || !revealShader ||
                !HasFrames(redRun) || !HasFrames(blueRun) || !HasFrames(redIdle) || !HasFrames(blueIdle) ||
                !HasFrames(bossIdle) || !HasFrames(bossCast))
            {
                Debug.LogError("Boss entrance needs its actors, pipe, shared dialogue prefab, reveal shader and six animation clips.", this);
                Finished = true;
                CurrentStage = EStage.Finished;
                return;
            }

            IsPlaying = true;
            sfxSource = StorySfx.CreateSource(gameObject, "Boss Entrance SFX");
            GateGameplay();
            BuildStage();
        }

        private void Start()
        {
            if (IsPlaying) routine = StartCoroutine(Play());
        }

        private void GateGameplay()
        {
            gated = true;
            bossRenderer = boss.GetComponent<SpriteRenderer>();
            hazards = boss.GetComponent<BossHazards>();
            playerBody = player.rb;
            playerPosition = playerBody.position;
            playerConstraints = playerBody.constraints;
            playerBody.constraints |= RigidbodyConstraints2D.FreezePosition;
            playerBody.linearVelocity = Vector2.zero;

            title = FindAnyObjectByType<LevelTitleCard>();
            if (title)
            {
                titleWasEnabled = title.enabled;
                title.enabled = false; // its Start builds the title only after the arena has been revealed
            }

            Suspend(boss.GetComponent<BossRoaming>());
            Suspend(boss.GetComponent<BossPhaseTwoCombat>());
            Suspend(boss);
            if (hazards) hazards.Bind(null);
            Suspend(hazards);
            Suspend(player.GetComponent<PlayerFreeMovementTopDownController2D>());
            // Canvas hiding alone leaves Escape shortcuts active and can open an invisible, paused popup.
            foreach (var activator in FindObjectsByType<PopupActivator>(FindObjectsSortMode.None)) Suspend(activator);
            foreach (var actor in new[] { boss.transform, player.transform })
            {
                foreach (var collider in actor.GetComponentsInChildren<Collider2D>(true))
                {
                    colliders.Add((collider, collider.enabled));
                    collider.enabled = false;
                }
                foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
                {
                    renderers.Add((renderer, renderer.forceRenderingOff));
                    renderer.forceRenderingOff = true;
                }
            }
            HideGameplayUi();
        }

        private void Suspend(Behaviour component)
        {
            if (!component) return;
            behaviours.Add((component, component.enabled));
            component.enabled = false;
        }

        private void HideGameplayUi()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                // The Core overlay must finish the ordinary Level 5 -> Level 6 transition above this presentation.
                if (canvas.transform.IsChildOf(transform) || canvas.GetComponentInChildren<LoadingOverlay>(true)) continue;
                if (capturedUi.Contains(canvas))
                {
                    canvas.enabled = false;
                    var existingRaycaster = canvas.GetComponent<GraphicRaycaster>();
                    if (existingRaycaster) existingRaycaster.enabled = false;
                    continue;
                }
                if (!canvas.enabled) continue;
                var raycaster = canvas.GetComponent<GraphicRaycaster>();
                hiddenUi.Add((canvas, raycaster, raycaster && raycaster.enabled));
                capturedUi.Add(canvas);
                canvas.enabled = false;
                if (raycaster) raycaster.enabled = false;
            }
        }

        private void BuildStage()
        {
            var canvas = OverlayCanvas.Create(transform, "Boss Entrance Canvas", StageOrder);
            stage = (RectTransform)canvas.transform;
            var pipeObject = new GameObject("Pipe Background", typeof(RectTransform), typeof(RawImage));
            pipeObject.transform.SetParent(stage, false);
            pipe = pipeObject.GetComponent<RawImage>();
            OverlayCanvas.Stretch(pipe.rectTransform);
            pipe.texture = pipeBackground.texture;
            pipe.raycastTarget = false;
            revealMaterial = new Material(revealShader) { name = "Boss entrance circular arena reveal" };
            revealMaterial.SetFloat(RevealRadius, -1f);
            pipe.material = revealMaterial;
            mergedActor = OverlayCanvas.CreateImage(stage, "Merged A&B", Color.white, false);
            bossActor = OverlayCanvas.CreateImage(stage, "Masked Boss", new Color(1f, 1f, 1f, 0f), false);
            var firstBossFrame = bossIdle.frames[0];
            bossStageUnitsPerWorld = 112f / (firstBossFrame.rect.height / firstBossFrame.pixelsPerUnit);
            Canvas.ForceUpdateCanvases();
            UpdatePipeFraming();
            DrawPlayerSideActor(redRun.frames[0], new Vector2(-stage.rect.width * 0.5f - 60f, FloorY));
            DrawBossSideActor(bossIdle.frames[0]);
            bossActor.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
        }

        private IEnumerator Play()
        {
            var controller = SceneController.TryGetInstance();
            while (controller && controller.IsBusy) yield return null;

            if (entranceMusic && MusicManager.HasInstance)
            {
                MusicManager.Instance.Play(entranceMusic, loop: true);
            }

            CurrentStage = EStage.Running;
            float runFrom = -stage.rect.width * 0.5f - 60f;
            float nextFootstep = 0f;
            for (float elapsed = 0f; elapsed < runSeconds; elapsed += Time.unscaledDeltaTime)
            {
                runElapsed = elapsed;
                ShowMerged(true, new Vector2(Mathf.Lerp(runFrom, -100f, Mathf.Clamp01(elapsed / runSeconds)), FloorY));
                if (elapsed >= nextFootstep)
                {
                    StorySfx.Play(sfxSource, "footsteps_metal", 0.42f);
                    nextFootstep = elapsed + 0.22f;
                }
                yield return null;
            }

            CurrentStage = EStage.BossAppearing;
            StorySfx.Play(sfxSource, "appear", 0.75f);
            for (float elapsed = 0f; elapsed < bossAppearanceSeconds; elapsed += Time.unscaledDeltaTime)
            {
                bossActor.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, elapsed / bossAppearanceSeconds));
                yield return null;
            }
            bossActor.color = Color.white;

            // Instantiate after hiding the gameplay canvases; the canonical dialogue must remain above the pipe.
            dialogueInstance = Instantiate(dialoguePrefab, transform);
            foreach (var canvas in dialogueInstance.GetComponentsInChildren<Canvas>(true))
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = StageOrder + 10;
            }
            dialogue = dialogueInstance.GetComponentInChildren<DialogueManager>(true);
            if (!dialogue) throw new InvalidOperationException("Boss entrance dialogue prefab has no DialogueManager.");
            yield return null; // allow the panel/style's subscriptions to attach before the first line
            CurrentStage = EStage.Dialogue;
            dialogue.EnterDialogue("BeforeBoss");
            while (dialogue && dialogue.DialogueIsPlaying) yield return null;

            CurrentStage = EStage.Casting;
            float duration = ClipDuration(bossCast);
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                castElapsed = elapsed;
                yield return null;
            }
            castElapsed = duration;
            yield return RevealArena();
            Complete();
        }

        private void Update()
        {
            if (!IsPlaying || CurrentStage == EStage.WaitingForTransition) return;
            animationElapsed += Time.unscaledDeltaTime;
            if (CurrentStage != EStage.Running) idleElapsed += Time.unscaledDeltaTime;
            if (CurrentStage == EStage.Running || CurrentStage == EStage.Revealing) return;
            ShowMerged(false, new Vector2(-100f, FloorY));
            var clip = CurrentStage == EStage.Casting ? bossCast : bossIdle;
            float elapsed = CurrentStage == EStage.Casting ? castElapsed : idleElapsed;
            DrawBossSideActor(FrameAt(clip, elapsed, clip != bossCast));
        }

        private void LateUpdate()
        {
            if (!IsPlaying) return;
            // Runtime HUDs can be created after our Awake. Keep those canvases hidden without disabling their controls.
            HideGameplayUi();
            if (playerBody)
            {
                playerBody.position = playerPosition;
                playerBody.linearVelocity = Vector2.zero;
            }
            UpdatePipeFraming();
        }

        private void ShowMerged(bool running, Vector2 foot)
        {
            bool blue = Mathf.FloorToInt(animationElapsed / Mathf.Max(0.01f, flickerSeconds)) % 2 != 0;
            var clip = running ? (blue ? blueRun : redRun) : (blue ? blueIdle : redIdle);
            // Form flicker chooses a frame from the shared elapsed clock; it never resets the walk/run clip.
            DrawPlayerSideActor(FrameAt(clip, running ? runElapsed : idleElapsed, true), foot);
        }

        private IEnumerator RevealArena()
        {
            CurrentStage = EStage.Revealing;
            // The native cast has finished; the sound lands when the arena begins expanding from the hand gesture.
            StorySfx.Play(sfxSource, "summon", 0.8f);
            Vector2 playerFrom = mergedActor.rectTransform.anchoredPosition;
            Vector2 bossFrom = bossActor.rectTransform.anchoredPosition;
            for (float elapsed = 0f; elapsed < summonSeconds; elapsed += Time.unscaledDeltaTime)
            {
                float t = Mathf.Clamp01(elapsed / summonSeconds);
                RevealProgress = t;
                DrawTakeoverActors(playerFrom, bossFrom, Mathf.SmoothStep(0f, 1f, t));
                UpdateReveal(t);
                yield return null;
            }
            RevealProgress = 1f;
            DrawTakeoverActors(playerFrom, bossFrom, 1f);
            UpdateReveal(1f);
            yield return null; // publish the fully revealed arena before restoring combat/UI
        }

        private void DrawTakeoverActors(Vector2 playerFrom, Vector2 bossFrom, float t)
        {
            var playerArt = VisiblePlayerArt();
            bool blue = Mathf.FloorToInt(animationElapsed / Mathf.Max(0.01f, flickerSeconds)) % 2 != 0;
            var playerSprite = t >= 0.9f && playerArt ? playerArt.sprite : FrameAt(blue ? blueIdle : redIdle, idleElapsed, true);
            var bossSprite = t >= 0.9f && bossRenderer ? bossRenderer.sprite : FrameAt(bossIdle, idleElapsed, true);
            Vector2 playerTarget = ToCanvas(playerArt ? playerArt.transform.position : player.transform.position);
            Vector2 bossTarget = ToCanvas(bossRenderer.transform.position);
            Vector2 playerScale = playerArt ? ProjectedPixelScale(playerArt, playerSprite) : Vector2.one * PlayerStagePixelSize;
            Vector2 bossScale = ProjectedPixelScale(bossRenderer, bossSprite);
            DrawActor(mergedActor, playerSprite, Vector2.Lerp(playerFrom, playerTarget, t),
                Vector2.Lerp(Vector2.one * PlayerStagePixelSize, playerScale, t));
            DrawActor(bossActor, bossSprite, Vector2.Lerp(bossFrom, bossTarget, t),
                Vector2.Lerp(Vector2.one * (bossStageUnitsPerWorld / bossSprite.pixelsPerUnit), bossScale, t));
            // Match the actual world's final pose and orientation before its renderers take over.
            if (t >= 0.9f)
            {
                mergedActor.rectTransform.localScale = new Vector3(playerArt && playerArt.flipX ? -1f : 1f, 1f, 1f);
                bossActor.rectTransform.localScale = new Vector3(bossRenderer.flipX ? -1f : 1f, 1f, 1f);
            }
        }

        private Vector2 ProjectedPixelScale(SpriteRenderer renderer, Sprite sprite)
        {
            Vector3 scale = renderer.transform.lossyScale;
            return new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) *
                   (stage.rect.height / (arenaCamera.orthographicSize * 2f * sprite.pixelsPerUnit));
        }

        private SpriteRenderer VisiblePlayerArt()
        {
            foreach (var renderer in player.GetComponentsInChildren<SpriteRenderer>())
                if (renderer.enabled && renderer.sprite && (renderer.name == "Red" || renderer.name == "Blue")) return renderer;
            return null;
        }

        private Vector2 ToCanvas(Vector3 world)
        {
            Vector3 viewport = arenaCamera.WorldToViewportPoint(world);
            return new Vector2((viewport.x - 0.5f) * stage.rect.width, (viewport.y - 0.5f) * stage.rect.height);
        }

        private void UpdateReveal(float t)
        {
            Vector2 size = stage.rect.size;
            var rect = bossActor.rectTransform;
            Vector2 centre = rect.anchoredPosition + (Vector2.one * 0.5f - rect.pivot) * rect.sizeDelta;
            Vector2 uv = new(centre.x / size.x + 0.5f, centre.y / size.y + 0.5f);
            float aspect = size.x / size.y;
            float farthest = 0f;
            foreach (var corner in new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one })
                farthest = Mathf.Max(farthest, Vector2.Scale(corner - uv, new Vector2(aspect, 1f)).magnitude);
            // The actor slides toward its world position; keep the radius growing as its centre moves.
            revealRadiusLimit = Mathf.Max(revealRadiusLimit, farthest + 0.02f);
            revealMaterial.SetVector(RevealCentre, new Vector4(uv.x, uv.y, 0f, 0f));
            revealMaterial.SetFloat(ScreenAspect, aspect);
            revealMaterial.SetFloat(RevealRadius, Mathf.Lerp(-0.01f, revealRadiusLimit, Mathf.SmoothStep(0f, 1f, t)));
        }

        private void UpdatePipeFraming()
        {
            if (!pipe || stage.rect.height <= 0f || stage.rect.width <= 0f) return;
            Rect textureRect = pipeBackground.textureRect;
            float sourceAspect = textureRect.width / textureRect.height;
            float targetAspect = stage.rect.width / stage.rect.height;
            float horizontalWindow = targetAspect / sourceAspect;
            // Preserve the pipe's vertical depth and platform. The standalone texture clamps its outer columns
            // to extend the pipe at wide-screen edges, while narrow screens crop horizontally.
            pipe.uvRect = new Rect((textureRect.x + textureRect.width * (1f - horizontalWindow) * 0.5f) / pipeBackground.texture.width,
                textureRect.y / pipeBackground.texture.height,
                textureRect.width * horizontalWindow / pipeBackground.texture.width, textureRect.height / pipeBackground.texture.height);
        }

        private void DrawPlayerSideActor(Sprite sprite, Vector2 foot)
        {
            // The source canvas pivot is eight logical art pixels above the feet, even when the cel is trimmed.
            DrawActor(mergedActor, sprite, foot + Vector2.up * (8f * PlayerStagePixelSize), Vector2.one * PlayerStagePixelSize);
        }

        private void DrawBossSideActor(Sprite sprite)
        {
            // Idle's 512px canvases and Cast's 48px canvases have different PPU but the same authored world scale.
            DrawActor(bossActor, sprite, new Vector2(150f, FloorY), Vector2.one * (bossStageUnitsPerWorld / sprite.pixelsPerUnit));
        }

        private static void DrawActor(Image image, Sprite sprite, Vector2 position, Vector2 pixelSize)
        {
            image.sprite = sprite;
            var rect = image.rectTransform;
            rect.pivot = sprite.pivot / sprite.rect.size;
            rect.sizeDelta = Vector2.Scale(sprite.rect.size, pixelSize);
            rect.anchoredPosition = position;
        }

        private static bool HasFrames(StageActorAnimation.Clip clip) => clip?.frames != null && clip.frames.Length > 0 &&
            Array.TrueForAll(clip.frames, frame => frame);

        private static float ClipDuration(StageActorAnimation.Clip clip)
        {
            float total = 0f;
            for (int i = 0; i < clip.frames.Length; i++) total += Duration(clip, i);
            return total;
        }

        private static float Duration(StageActorAnimation.Clip clip, int index) =>
            clip.durations != null && index < clip.durations.Length ? Mathf.Max(0.01f, clip.durations[index]) : 0.1f;

        private static Sprite FrameAt(StageActorAnimation.Clip clip, float elapsed, bool loop)
        {
            float duration = ClipDuration(clip);
            if (loop) elapsed %= duration;
            for (int i = 0; i < clip.frames.Length - 1; i++)
            {
                float frameSeconds = Duration(clip, i);
                if (elapsed < frameSeconds) return clip.frames[i];
                elapsed -= frameSeconds;
            }
            return clip.frames[^1];
        }

        /// <summary>Ends only this presentation, leaving the current story step and its loaded boss arena intact.</summary>
        public void Skip()
        {
            if (!IsPlaying) return;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            Complete();
        }

        private void Complete()
        {
            // BossFightStoryExit hands over to phase one on the next frame; let its
            // Play call crossfade directly from the entrance cue.
            IsPlaying = false;
            Finished = true;
            CurrentStage = EStage.Finished;
            if (sfxSource) sfxSource.Stop();
            CloseDialogue();
            RestoreGameplay();
            DestroyPresentation();
        }

        private void CloseDialogue()
        {
            if (dialogue && dialogue.DialogueIsPlaying) dialogue.ExitDialogue();
            dialogue = null;
            if (dialogueInstance)
            {
                dialogueInstance.SetActive(false);
                Destroy(dialogueInstance);
            }
            dialogueInstance = null;
        }

        private void RestoreGameplay()
        {
            if (!gated) return;
            gated = false;
            // Re-enabling the boss may reset its colliders, so restore collider snapshots afterward.
            foreach (var (component, enabled) in behaviours) if (component) component.enabled = enabled;
            behaviours.Clear();
            if (hazards && boss && boss.enabled) hazards.Bind(player);
            foreach (var (collider, enabled) in colliders) if (collider) collider.enabled = enabled;
            colliders.Clear();
            if (playerBody)
            {
                playerBody.position = playerPosition;
                playerBody.constraints = playerConstraints;
                playerBody.linearVelocity = Vector2.zero;
            }
            foreach (var (renderer, hidden) in renderers) if (renderer) renderer.forceRenderingOff = hidden;
            renderers.Clear();
            foreach (var (canvas, raycaster, raycasts) in hiddenUi)
            {
                if (canvas) canvas.enabled = true;
                if (raycaster) raycaster.enabled = raycasts;
            }
            hiddenUi.Clear();
            capturedUi.Clear();
            if (title) title.enabled = titleWasEnabled;
        }

        private void DestroyPresentation()
        {
            if (stage)
            {
                stage.gameObject.SetActive(false);
                Destroy(stage.gameObject);
            }
            stage = null;
            if (revealMaterial) Destroy(revealMaterial);
            revealMaterial = null;
        }

        private void OnDisable()
        {
            if (closing) return;
            closing = true;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            IsPlaying = false;
            Finished = true;
            CurrentStage = EStage.Finished;
            if (sfxSource) sfxSource.Stop();
            CloseDialogue();
            RestoreGameplay();
            DestroyPresentation();
        }
    }
}
