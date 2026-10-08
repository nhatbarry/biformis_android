using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Game.Player;
using TMPro;
using UnityEngine;

namespace CaptainPinkTurd.Game.Enemy
{
    [RequireComponent(typeof(PlagueDoctorBoss), typeof(BossPhaseTwoEffects))]
    public class BossPhaseTwoCombat : MonoBehaviour
    {
        public enum EAction { HorizontalSlash, RangedCharge, DashStab, VerticalSlash }
        public enum ECue { SlashHit, SpawnRangedSkill, BeginDash, StabHit, EndDash, SpawnSwordAura }
        [SerializeField] private PlagueDoctorBoss.Clip horizontalSlash;
        [SerializeField] private PlagueDoctorBoss.Clip rangedCharge;
        [SerializeField] private PlagueDoctorBoss.Clip dashStab;
        [SerializeField] private PlagueDoctorBoss.Clip verticalSlash;
        [SerializeField] private int rangedReleaseFrame = 6;
        [SerializeField, Min(0.1f)] private float restSeconds = 1f;
        [SerializeField] private bool automaticAttacks = true;
        [SerializeField] private float slashRange = 2.55f;
        [SerializeField] private float dashOvershoot = 1.6f;
        [SerializeField] private float auraInterval = 0.34f;
        [SerializeField] private TMP_FontAsset warningFont;
        [SerializeField] private SoundData attackSfx;
        [Tooltip("Editor preview only: skip phase one when Play starts.")]
        [SerializeField] private bool previewPhaseTwoOnStart;
        private PlagueDoctorBoss boss;
        private BossHazards rules;
        private BossPhaseTwoEffects effects;
        private BossArenaController arena;
        private SpriteRenderer sprite;
        private BossDirectionalArt directional;
        private PlayerUnit player;
        private Rigidbody2D body;
        private BoxCollider2D damageTrigger;
        private CircleCollider2D feet;
        private TMP_Text warning;
        private Sprite restingPose;
        private MaterialPropertyBlock blade;
        private Coroutine life, attack, burst;
        private EAction? lastAction;
        private Vector2 aim;
        private bool started, meleeHit, dashing, savedFeet, savedTrigger, auraFinished;
        private float dashBegan, dashDuration;
        private readonly List<EColor> auraColours = new(4);
        private static readonly int BladeTint = Shader.PropertyToID("_BladeTint");
        private static readonly int BladeEndpoints = Shader.PropertyToID("_BladeEndpoints");
        private static readonly int BladeAtlasScale = Shader.PropertyToID("_BladeAtlasScale");
        private static readonly int BladeOn = Shader.PropertyToID("_BladeOn");
        private static readonly int BladeWidth = Shader.PropertyToID("_BladeWidth");
        public EAction? CurrentAction { get; private set; }
        public int CurrentFrame { get; private set; } = -1;
        public EColor CurrentColour { get; private set; }
        public Vector2 LockedTarget { get; private set; }
        public Vector2 LastDashOrigin { get; private set; }
        public Vector2 LastDashEnd { get; private set; }
        public Vector2 AttackDirection => aim;
        public bool WarningVisible => warning && warning.gameObject.activeSelf;
        public Color WarningColour => warning ? warning.color : Color.clear;
        public bool IsDashing => dashing;
        public IReadOnlyList<EColor> LastAuraColours => auraColours;
        public GameEvent<ECue> OnSkillCue { get; } = new();
        public float DashRange => arena ? arena.PlayArea.size.magnitude : 30f;
        public bool CanDashAtPlayer
        {
            get
            {
                if (!rules.HasPlayer || !player || Vector2.Distance(body.position, player.transform.position) > DashRange) return false;
                return Vector2.Distance(body.position, player.transform.position) > 0.1f;
            }
        }
        private void Awake()
        {
            boss = GetComponent<PlagueDoctorBoss>();
            rules = GetComponent<BossHazards>();
            effects = GetComponent<BossPhaseTwoEffects>();
            sprite = GetComponent<SpriteRenderer>();
            directional = GetComponent<BossDirectionalArt>();
            body = GetComponent<Rigidbody2D>();
            feet = GetComponent<CircleCollider2D>();
            damageTrigger = GetComponent<BoxCollider2D>();
            blade = new MaterialPropertyBlock();
            var icon = new GameObject("Boss Attack Warning", typeof(RectTransform), typeof(Canvas), typeof(TextMeshProUGUI));
            icon.transform.SetParent(transform, false);
            var warningCanvas = icon.GetComponent<Canvas>();
            warningCanvas.renderMode = RenderMode.WorldSpace;
            warningCanvas.overrideSorting = true;
            warningCanvas.sortingLayerName = "Default";
            warningCanvas.sortingOrder = 70;
            warning = icon.GetComponent<TextMeshProUGUI>();
            warning.font = warningFont;
            warning.fontSize = 110f;
            warning.alignment = TextAlignmentOptions.Center;
            warning.text = "!";
            warning.raycastTarget = false;
            warning.overflowMode = TextOverflowModes.Overflow;
            warning.rectTransform.sizeDelta = new Vector2(100f, 110f);
            icon.transform.localPosition = new Vector3(-0.9f, 3.65f, 0f);
            icon.transform.localScale = Vector3.one * 0.01f;
            icon.SetActive(false);
        }
        private void OnEnable()
        {
            boss.OnPhaseTwoReady.Subscribe(StartCombat);
            boss.OnDeath.Subscribe(OnDeath);
        }
        private void Start() { if (previewPhaseTwoOnStart) boss.PreviewPhaseTwo(); }
        private void OnDisable()
        {
            boss.OnPhaseTwoReady.Unsubscribe(StartCombat);
            boss.OnDeath.Unsubscribe(OnDeath);
            StopCombat();
        }
        private void OnDeath(SDamageData damage) => StopCombat();
        private void StartCombat()
        {
            StopCombat();
            restingPose = sprite.sprite;
            player = FindAnyObjectByType<PlayerUnit>();
            arena = FindAnyObjectByType<BossArenaController>();
            started = true;
            lastAction = null;
            life = StartCoroutine(Live());
        }
        private IEnumerator Live()
        {
            while (boss.Phase == PlagueDoctorBoss.EPhase.RedHaired)
            {
                if (!automaticAttacks || CurrentAction.HasValue || !rules.HasPlayer) { yield return null; continue; }
                yield return new WaitForSeconds(Random.Range(restSeconds * 0.45f, restSeconds * 1.45f));
                if (boss.Phase != PlagueDoctorBoss.EPhase.RedHaired || !automaticAttacks || CurrentAction.HasValue || !rules.HasPlayer) continue;
                var choices = new List<EAction> { EAction.RangedCharge, EAction.VerticalSlash };
                if (Vector2.Distance(body.position, player.transform.position) <= slashRange) choices.Add(EAction.HorizontalSlash);
                if (CanDashAtPlayer) choices.Add(EAction.DashStab);
                if (choices.Count > 1 && lastAction.HasValue) choices.Remove(lastAction.Value);
                TryAttack(choices[Random.Range(0, choices.Count)]);
                while (CurrentAction.HasValue) yield return null;
            }
        }
        /// <summary>Normal AI chooses a random colour; a scripted encounter may supply one explicitly.</summary>
        public bool TryAttack(EAction action, EColor? colour = null)
        {
            if (!isActiveAndEnabled || boss.Phase != PlagueDoctorBoss.EPhase.RedHaired || CurrentAction.HasValue || !rules.HasPlayer) return false;
            if (action == EAction.DashStab && !CanDashAtPlayer) return false;
            CurrentAction = action;
            CurrentColour = colour ?? (Random.value < 0.5f ? EColor.Red : EColor.Blue);
            CurrentFrame = -1;
            LockedTarget = player.transform.position;
            aim = (LockedTarget - body.position).normalized;
            if (aim.sqrMagnitude < 0.001f) aim = sprite.flipX ? Vector2.left : Vector2.right;
            sprite.flipX = !(directional && directional.HasAnimations) && aim.x < 0f;
            meleeHit = false;
            lastAction = action;
            auraColours.Clear();
            auraFinished = true;
            warning.color = BossPhaseTwoEffects.Colour(CurrentColour);
            warning.gameObject.SetActive(action != EAction.VerticalSlash);
            attack = StartCoroutine(Perform(action));
            return true;
        }
        private PlagueDoctorBoss.Clip Clip(EAction action) => directional && directional.HasAnimations
            ? directional.AttackClip(action, aim) : action switch
        {
            EAction.HorizontalSlash => horizontalSlash,
            EAction.RangedCharge => rangedCharge,
            EAction.DashStab => dashStab,
            _ => verticalSlash,
        };
        private IEnumerator Perform(EAction action)
        {
            var clip = Clip(action);
            for (int frame = 0; frame < clip.frames.Length; frame++)
            {
                if (boss.Phase != PlagueDoctorBoss.EPhase.RedHaired) yield break;
                CurrentFrame = frame;
                if ((action == EAction.HorizontalSlash && frame <= 4) || (action == EAction.DashStab && frame <= 3)) AimAtPlayer();
                clip = Clip(action);
                sprite.sprite = clip.frames[frame];
                SetBlade(clip, frame, true);
                RaiseCues(action, frame);
                float end = Time.time + clip.FrameSeconds(frame);
                while (Time.time < end)
                {
                    if (boss.Phase != PlagueDoctorBoss.EPhase.RedHaired) yield break;
                    if ((action == EAction.HorizontalSlash && frame < 4) || (action == EAction.DashStab && frame < 3))
                    {
                        AimAtPlayer();
                        clip = Clip(action);
                        sprite.sprite = clip.frames[frame];
                        SetBlade(clip, frame, true);
                    }
                    int releaseFrame = action == EAction.RangedCharge ? rangedReleaseFrame : 5;
                    if ((action == EAction.RangedCharge || action == EAction.VerticalSlash) && frame >= 1 && frame < releaseFrame)
                    {
                        float release = 0f;
                        for (int i = 0; i < releaseFrame; i++) release += clip.FrameSeconds(i);
                        float elapsed = 0f;
                        for (int i = 0; i < frame; i++) elapsed += clip.FrameSeconds(i);
                        elapsed += clip.FrameSeconds(frame) - (end - Time.time);
                        effects.ShowCharge(CurrentColour, (elapsed - 0.18f) / (release - 0.18f));
                    }
                    if (action == EAction.HorizontalSlash && frame >= 4 && frame <= 5 && !meleeHit)
                        meleeHit = effects.TryHitSector(body.position + Vector2.up * 0.25f, aim, slashRange, CurrentColour);
                    if (dashing) AdvanceDash();
                    yield return null;
                }
            }
            while (!auraFinished && boss.Phase == PlagueDoctorBoss.EPhase.RedHaired) yield return null;
            RestoreDash();
            effects.HideCharge();
            warning.gameObject.SetActive(false);
            SetBlade(clip, 0, false);
            sprite.sprite = restingPose;
            CurrentAction = null;
            CurrentFrame = -1;
            attack = null;
        }
        private void RaiseCues(EAction action, int frame)
        {
            if (action == EAction.HorizontalSlash && frame == 4)
            {
                warning.gameObject.SetActive(false);
                PlayAttackSound();
                effects.ShowSlash(body.position + Vector2.up * 0.25f, aim, slashRange, CurrentColour);
                OnSkillCue.Raise(ECue.SlashHit);
            }
            if (action == EAction.RangedCharge && frame == rangedReleaseFrame)
            {
                warning.gameObject.SetActive(false);
                effects.HideCharge();
                PlayAttackSound();
                LockedTarget = rules.PlayerHitPosition;
                var ground = body.position + Vector2.up * 0.25f;
                var direction = (LockedTarget - ground).normalized;
                aim = direction;
                var releaseClip = Clip(action);
                sprite.sprite = releaseClip.frames[frame];
                SetBlade(releaseClip, frame, true);
                effects.FireShot(ground + direction * 0.65f, direction, CurrentColour);
                rules.PlayAttackImpact();
                OnSkillCue.Raise(ECue.SpawnRangedSkill);
            }
            if (action == EAction.DashStab)
            {
                if (frame == 3)
                {
                    warning.gameObject.SetActive(false);
                    PlayAttackSound();
                    LastDashOrigin = body.position;
                    LastDashEnd = ClampDash(LockedTarget + aim * dashOvershoot);
                    dashBegan = Time.time;
                    dashDuration = dashStab.FrameSeconds(3) + dashStab.FrameSeconds(4) + dashStab.FrameSeconds(5);
                    savedFeet = feet.enabled; savedTrigger = damageTrigger.enabled;
                    // Pass through the target: standing still does not automatically ram the moving boss.
                    feet.enabled = damageTrigger.enabled = false;
                    dashing = true;
                    rules.PlayAttackImpact(0.06f);
                    OnSkillCue.Raise(ECue.BeginDash);
                }
                if (frame == 4) OnSkillCue.Raise(ECue.StabHit);
                if (frame == 6)
                {
                    AdvanceDash(true);
                    RestoreDash();
                    OnSkillCue.Raise(ECue.EndDash);
                }
            }
            if (action == EAction.VerticalSlash && frame == 5)
            {
                effects.HideCharge();
                PlayAttackSound();
                LockedTarget = rules.PlayerHitPosition;
                aim = (LockedTarget - (body.position + Vector2.up * 0.25f)).normalized;
                var releaseClip = Clip(action);
                sprite.sprite = releaseClip.frames[frame];
                SetBlade(releaseClip, frame, true);
                auraFinished = false;
                burst = StartCoroutine(FourAuras(body.position + Vector2.up * 0.25f + aim * 1.1f, aim));
                OnSkillCue.Raise(ECue.SpawnSwordAura);
            }
        }
        private void AimAtPlayer()
        {
            LockedTarget = player.transform.position;
            aim = (LockedTarget - body.position).normalized;
            if (aim.sqrMagnitude < 0.001f) aim = sprite.flipX ? Vector2.left : Vector2.right;
            sprite.flipX = !(directional && directional.HasAnimations) && aim.x < 0f;
        }

        public bool HoldsPosition => CurrentAction == EAction.RangedCharge || CurrentAction == EAction.VerticalSlash || IsDashing;
        private IEnumerator FourAuras(Vector2 origin, Vector2 direction)
        {
            for (int i = 0; i < 4 && boss.Phase == PlagueDoctorBoss.EPhase.RedHaired; i++)
            {
                if (i > 0) CurrentColour = Random.value < 0.5f ? EColor.Red : EColor.Blue;
                auraColours.Add(CurrentColour);
                var clip = Clip(EAction.VerticalSlash);
                SetBlade(clip, Mathf.Clamp(CurrentFrame, 0, clip.frames.Length - 1), true);
                effects.FireWave(origin, direction, CurrentColour);
                if (i < 3) yield return new WaitForSeconds(auraInterval);
            }
            auraFinished = true;
            burst = null;
        }
        private void AdvanceDash(bool finish = false)
        {
            if (!dashing || (!finish && Time.deltaTime <= 0f)) return;
            Vector2 before = body.position;
            float t = finish ? 1f : Mathf.Clamp01((Time.time - dashBegan) / dashDuration);
            Vector2 after = Vector2.Lerp(LastDashOrigin, LastDashEnd, t);
            body.position = after;
            transform.position = new Vector3(after.x, after.y, transform.position.z);
            int samples = Mathf.Min(12, Mathf.CeilToInt(Vector2.Distance(before, after) / 0.24f));
            for (int i = 0; i < samples; i++) effects.DashAfterimage(Vector2.Lerp(before, after, i / (float)samples));
            if (!meleeHit) meleeHit = effects.TryHitSegment(before + Vector2.up * 0.25f + aim * 0.6f,
                after + Vector2.up * 0.25f + aim * 0.6f, 0.65f, CurrentColour);
        }
        private Vector2 ClampDash(Vector2 position) => arena ? arena.ClampPosition(position, 0.55f) : position;
        private void RestoreDash()
        {
            if (!dashing) return;
            dashing = false;
            body.linearVelocity = Vector2.zero;
            if (boss.Phase == PlagueDoctorBoss.EPhase.RedHaired)
            { feet.enabled = savedFeet; damageTrigger.enabled = savedTrigger; }
        }
        private void SetBlade(PlagueDoctorBoss.Clip clip, int frame, bool visible)
        {
            if (!sprite || blade == null) return;
            sprite.GetPropertyBlock(blade);
            blade.SetFloat(BladeOn, visible ? 1f : 0f);
            blade.SetColor(BladeTint, BossPhaseTwoEffects.Colour(CurrentColour));
            blade.SetFloat(BladeWidth, clip.bladeWidths != null && frame < clip.bladeWidths.Length ? clip.bladeWidths[frame] : 0.11f);
            if (clip.bladeEndpoints != null && frame < clip.bladeEndpoints.Length)
            {
                // Use atlas coordinates so sprite flipping, trimming and renderer batching cannot move the mask.
                var art = clip.frames[frame];
                Vector2 offset = (art.rect.position + art.pivot) / art.pixelsPerUnit;
                var endpoints = clip.bladeEndpoints[frame];
                blade.SetVector(BladeEndpoints, endpoints + new Vector4(offset.x, offset.y, offset.x, offset.y));
                blade.SetVector(BladeAtlasScale, new Vector4(art.texture.width / art.pixelsPerUnit,
                    art.texture.height / art.pixelsPerUnit, 0f, 0f));
            }
            sprite.SetPropertyBlock(blade);
        }
        private void PlayAttackSound()
        {
            if (attackSfx?.clip) SoundManager.Instance.CreateSoundBuilder().WithPosition(transform.position).WithRandomPitch().Play(attackSfx);
        }
        private void Update()
        {
            if (started && (boss.Phase != PlagueDoctorBoss.EPhase.RedHaired || !rules.HasPlayer)) StopCombat();
        }
        private void StopCombat()
        {
            started = false;
            if (life != null) StopCoroutine(life);
            if (attack != null) StopCoroutine(attack);
            if (burst != null) StopCoroutine(burst);
            life = attack = burst = null;
            RestoreDash();
            if (effects) effects.ClearAll();
            if (warning) warning.gameObject.SetActive(false);
            if (horizontalSlash != null) SetBlade(horizontalSlash, 0, false);
            CurrentAction = null;
            CurrentFrame = -1;
        }
    }
}
