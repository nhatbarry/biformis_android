using System;
using System.Collections.Generic;
using System.Globalization;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Enum;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.Core.InputPaths;
using CaptainPinkTurd.Core.Localization;
using CaptainPinkTurd.InkDialogue;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// Stages a cutscene from ink tags, so the writer controls what is on screen from the script:
    ///   #bg:white | black | hospital | past | RRGGBB[:seconds]   backdrop colour (a name from backdropColors or hex)
    ///   #cast:A,B,Teen | none                    which StageActors stand on stage
    ///   #fx:shake | flash | red | fade_black | fade_white | fade_in
    ///   #sfx:beep | stop | &lt;name in sounds&gt;     "beep" loops a heart monitor, "stop" ends any loop
    ///   #music:&lt;name in music&gt; | stop            plays a music track (looping) or stops the one playing
    ///   #anim:ActorId:clip[:seconds]               plays a clip of an actor's StageActorAnimation, optionally after a delay
    ///                                              (e.g. anim:B_Bed:wake:1.2)
    ///   #move:ActorId:x[,y][:seconds]              slides an actor to a canvas position (instantly without seconds)
    ///   #flip:ActorId:on|off                       mirrors an actor (faces the other way)
    ///   #fade:RRGGBB[:seconds] | #fade:in[:seconds]  fades the whole screen to a colour, or back from it
    ///   #banging:ActorId:on|off                    someone pounds on that actor (a door) every ~0.6 s: its "bang" clip,
    ///                                              a small screen shake, and the bang text while the door is off screen
    ///   #knock:ActorId:level                       one weaker knock: "bang" once and the knock text at that opacity
    ///   #attach:ActorId:TargetId:dx,dy | :none     keeps an actor (a prop) at another's position plus an offset, e.g. a box
    ///                                              in someone's hand, until attached elsewhere or to none
    ///   #reach:ActorId:TargetId:maxX[:promptKey]   the player walks the actor (left/right keys, A/D, a stick - on phones
    ///                                              the touch HUD's joystick, shown for it) up to the target, then acts
    ///                                              with Interact (E / Space, the HUD's "!" button, lit once close
    ///                                              enough); taps on the stage don't count; put #hold after it. The
    ///                                              prompt reads promptKey / promptKey_touch (default stage.take)
    ///   #pixel:cut[:seconds]                       freezes what is on stage, then dissolves that picture away in 2x2 art
    ///                                              pixel blocks over whatever the following tags put on stage
    ///   #pixel:RRGGBB[:seconds] | #pixel:in[:seconds]  covers the screen with blocks of a colour, or uncovers it
    ///   #alpha:ActorId:opacity[:seconds]           fades a whole actor (e.g. a shadow on the wall)
    ///   #shake:units[:seconds]                     shakes the stage (4 units = one art pixel)
    ///   #follow:WorldId:WalkerId:minX,maxX | none  a camera on the walker (a child of the world actor): the world
    ///                                              slides so the walker keeps its place on screen, within minX..maxX
    ///                                              (the world's x); e.g. B walking through the ending's cage room
    ///   #struggle:ActorId:count                    a struggle the player taps (or presses Space / Interact) through:
    ///                                              each press plays the actor's "struggle" clip and fills a segment of
    ///                                              the meter; put #hold after it so the dialogue waits for the last one
    /// The actor who is speaking is lit, the others take listeningTint. An actor with a "talk" clip plays it while its
    /// line types, if it is standing at its default clip (an actor crying, walking... keeps doing that), then goes back. When the villain speaks without being on stage
    /// (no actor speaking as the villain is cast), its lines tint the screen red and shake it.
    /// </summary>
    public class CutsceneStage : MonoBehaviour
    {
        [Header("Backdrop")]
        [SerializeField] private Image backdrop;
        [SerializeField] private SerializeKeyValuePair<string, Color>[] backdropColors =
        {
            new() { Key = "white", Value = Color.white },
            new() { Key = "black", Value = Color.black },
            new() { Key = "hospital", Value = new Color(0.06f, 0.09f, 0.1f) },
            new() { Key = "past", Value = new Color(0.16f, 0.11f, 0.08f) },
        };
        [SerializeField] private float backdropFadeTime = 0.8f;

        [Header("Cast")]
        [SerializeField] private StageActor[] actors;
        [SerializeField] private Color speakingTint = Color.white;
        [SerializeField] private Color listeningTint = new(0.55f, 0.55f, 0.6f, 1f);

        [Header("Villain")]
        [SerializeField] private string villainSpeakerId = "Villain";
        [Tooltip("Full-screen red overlay faded in while the villain speaks")]
        [SerializeField] private Image villainOverlay;
        [SerializeField] private float villainOverlayAlpha = 0.22f;

        [Header("Effects")]
        [Tooltip("Full-screen overlay used for fades and flashes")]
        [SerializeField] private Image fadeOverlay;
        [SerializeField] private float fadeTime = 0.8f;
        [SerializeField] private RectTransform shakeTarget;

        [Header("Door")]
        [Tooltip("Shown at the screen's edge for each bang while the door being pounded is off screen")]
        [SerializeField] private RectTransform bangText;
        [SerializeField] private float bangInterval = 0.56f;
        [SerializeField] private float bangIntervalRandom = 0.12f;
        [Tooltip("Shown by the door for each weak knock, fading out")]
        [SerializeField] private Graphic knockText;

        [Header("Reach")]
        [Tooltip("Shown over the walker while it is close enough to act; blinks")]
        [SerializeField] private RectTransform reachPrompt;
        [Tooltip("Shown over the walker until the player first moves it; blinks")]
        [SerializeField] private RectTransform reachHint;
        [Tooltip("Canvas units per second (40 art pixels)")]
        [SerializeField] private float reachSpeed = 160f;
        [Tooltip("The walker can't get closer to the target than this")]
        [SerializeField] private float reachStopDistance = 60f;
        [Tooltip("Close enough to act")]
        [SerializeField] private float reachNearDistance = 76f;
        [Tooltip("How far above the walker's centre the prompt and hint stand")]
        [SerializeField] private float reachPromptHeight = 76f;

        [Header("Pixel Transition")]
        [Tooltip("Canvas units per block (2 art pixels, like the team's preview)")]
        [SerializeField] private float pixelBlockSize = 8f;
        [Tooltip("How many steps a pixel transition takes")]
        [SerializeField] private int pixelSteps = 8;
        [SerializeField] private float pixelTime = 0.56f;

        [Header("Struggle")]
        [Tooltip("Shown during a struggle; its child Images are the segments, filled left to right")]
        [SerializeField] private RectTransform struggleMeter;
        [SerializeField] private Color struggleEmpty = new(0.161f, 0.290f, 0.318f, 1f);
        [SerializeField] private Color struggleFilled = new(0.631f, 0.200f, 0.216f, 1f);
        [Tooltip("Full-screen tap target used while the stage holds the dialogue (the dialogue panel's is hidden then)")]
        [SerializeField] private GameObject stageTapTarget;

        [Header("Sound")]
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private SerializeKeyValuePair<string, AudioClip>[] sounds;
        [SerializeField] private SerializeKeyValuePair<string, AudioClip>[] music;

        private const string TalkClip = "talk";
        private const string DefaultReachPrompt = "stage.take";
        private readonly HashSet<string> castOnStage = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<StageActorAnimation> talking = new();
        private StageActorAnimation struggler;
        private int strugglePresses;
        private int struggleTarget;
        private StageActor bangingDoor;
        private float nextBang, bangTextLeft, knockLeft, knockLevel;
        private Vector2 bangTextHome;
        private readonly Dictionary<RectTransform, (RectTransform target, Vector2 offset)> attached = new();
        private StageActor reachWalker, reachTarget;
        private float reachMaxX, reachTime;
        private bool reachMoved;
        private float shakeEnds, shakeStrength;
        private RectTransform followWorld, followWalker;
        private float followMin, followMax, followScreenX;
        private AudioClip monitorBeepClip;
        private Tween villainTween;
        //pixel transitions: a cover of coloured blocks, and a frozen copy of the stage masked away block by block
        private RawImage pixelCover, pixelCutMask;
        private Texture2D pixelCoverTexture, pixelCutTexture;
        private float[] pixelOrder;
        private Tween pixelCoverTween, pixelCutTween;

        private void Awake()
        {
            SetCast(Array.Empty<string>());
            if (villainOverlay) SetAlpha(villainOverlay, 0f);
            if (fadeOverlay) SetAlpha(fadeOverlay, 0f);
            if (struggleMeter) struggleMeter.gameObject.SetActive(false);
            if (stageTapTarget) stageTapTarget.SetActive(false);
            if (bangText)
            {
                bangTextHome = bangText.anchoredPosition;
                bangText.gameObject.SetActive(false);
            }
            if (knockText) knockText.gameObject.SetActive(false);
            if (reachPrompt) reachPrompt.gameObject.SetActive(false);
            if (reachHint) reachHint.gameObject.SetActive(false);
        }

        //Start, not OnEnable: DialogueManager creates these events in its Awake, which may run after our OnEnable
        private void Start()
        {
            var dialogueManager = DialogueManager.Instance;
            dialogueManager.OnStageTag.Subscribe(HandleStageTag);
            dialogueManager.OnDisplayDialogue.Subscribe(OnDisplayDialogue);
            dialogueManager.OnStageInput.Subscribe(OnStageInput);
            dialogueManager.OnStagePause.Subscribe(OnStagePause);
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);
            if (reachWalker) EndReach();
            if (pixelCoverTexture) Destroy(pixelCoverTexture);
            if (pixelCutTexture) Destroy(pixelCutTexture);

            if (!DialogueManager.HasInstance) return;
            DialogueManager.Instance.OnStageTag.Unsubscribe(HandleStageTag);
            DialogueManager.Instance.OnDisplayDialogue.Unsubscribe(OnDisplayDialogue);
            DialogueManager.Instance.OnStageInput.Unsubscribe(OnStageInput);
            DialogueManager.Instance.OnStagePause.Unsubscribe(OnStagePause);
        }

        private void HandleStageTag(string tag)
        {
            int separator = tag.IndexOf(':');
            string key = (separator < 0 ? tag : tag[..separator]).Trim().ToLowerInvariant();
            string value = separator < 0 ? "" : tag[(separator + 1)..].Trim();

            switch (key)
            {
                case "bg":
                    SetBackdrop(value);
                    break;
                case "cast":
                    SetCast(value.Equals("none", StringComparison.OrdinalIgnoreCase)
                        ? Array.Empty<string>()
                        : value.Split(','));
                    break;
                case "fx":
                    PlayEffect(value.ToLowerInvariant());
                    break;
                case "sfx":
                    PlaySound(value.ToLowerInvariant());
                    break;
                case "anim":
                    PlayAnimation(value);
                    break;
                case "move":
                    MoveActor(value);
                    break;
                case "struggle":
                    StartStruggle(value);
                    break;
                case "flip":
                    FlipActor(value);
                    break;
                case "fade":
                    Fade(value);
                    break;
                case "banging":
                    SetBanging(value);
                    break;
                case "knock":
                    Knock(value);
                    break;
                case "attach":
                    Attach(value);
                    break;
                case "reach":
                    StartReach(value);
                    break;
                case "alpha":
                    FadeActor(value);
                    break;
                case "shake":
                    ShakeTag(value);
                    break;
                case "pixel":
                    PixelTransition(value);
                    break;
                case "follow":
                    Follow(value);
                    break;
                case "music":
                    PlayMusic(value);
                    break;
                default:
                    Debug.LogWarning($"Cutscene tag not handled: {tag}");
                    break;
            }
        }

        private void OnDisplayDialogue(DialogueInfo info)
        {
            if (info.line == null) return; //a "skip typing" request, not a new line

            foreach (var actor in actors)
            {
                actor.SetTint(actor.SpeakerId.Equals(info.speaker, StringComparison.OrdinalIgnoreCase) ? speakingTint : listeningTint);
            }

            //a villain standing on stage speaks for itself; one without a body is shown by the red tint and a shake
            StartTalking(info.speaker);

            bool villainSpeaking = info.speaker == villainSpeakerId && !VillainOnStage();
            if (villainOverlay)
            {
                villainTween?.Kill();
                villainTween = villainOverlay.DOFade(villainSpeaking ? villainOverlayAlpha : 0f, 0.4f).SetUpdate(true).SetId(this);
            }
            if (villainSpeaking && shakeTarget) Shake(4f);
        }

        /// <summary>
        /// "name" or "RRGGBB", optionally followed by ":seconds" for the fade.
        /// </summary>
        private void SetBackdrop(string value)
        {
            if (!backdrop) return;
            var parts = value.Split(':');
            string colorName = parts[0].Trim();
            float fade = backdropFadeTime;
            if (parts.Length > 1) float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out fade);

            if (!backdropColors.TryGetValue(colorName.ToLowerInvariant(), out Color color) &&
                !(colorName.Length == 6 && ColorUtility.TryParseHtmlString("#" + colorName, out color)))
            {
                Debug.LogWarning($"Unknown backdrop colour: {colorName}");
                return;
            }
            backdrop.DOKill();
            backdrop.DOColor(color, fade).SetUpdate(true).SetId(this);
        }

        /// <summary>
        /// "ActorId:x" or "ActorId:x:seconds", x in canvas units from the centre of the stage.
        /// </summary>
        private void MoveActor(string value)
        {
            var parts = value.Split(':');
            var position = parts.Length > 1 ? parts[1].Split(',') : Array.Empty<string>();
            float y = 0f;
            if (position.Length is < 1 or > 2 || !TryParse(position[0], out float x) || (position.Length == 2 && !TryParse(position[1], out y)))
            {
                Debug.LogWarning($"Cutscene move is not Actor:x[,y][:seconds]: {value}");
                return;
            }
            float seconds = 0f;
            if (parts.Length > 2) TryParse(parts[2], out seconds);

            var actor = FindActor(parts[0]);
            if (!actor)
            {
                Debug.LogWarning($"Cutscene actor not found: {value}");
                return;
            }
            var rect = (RectTransform)actor.transform;
            rect.DOKill();
            var target = new Vector2(x, position.Length == 2 ? y : rect.anchoredPosition.y);
            if (seconds <= 0f) rect.anchoredPosition = target;
            else rect.DOAnchorPos(target, seconds).SetEase(Ease.Linear).SetUpdate(true).SetId(this);
        }

        // ------------------------------------------------------------------ struggle

        private void StartStruggle(string value)
        {
            var parts = value.Split(':');
            var actor = FindActor(parts[0]);
            if (!actor || !actor.TryGetComponent(out struggler) || parts.Length < 2 || !int.TryParse(parts[1], out struggleTarget))
            {
                Debug.LogWarning($"Cutscene struggle is not Actor:count with an animated actor: {value}");
                struggleTarget = 0;
                DialogueManager.Instance.ReleaseStageHold();
                return;
            }
            strugglePresses = 0;
            if (struggleMeter) struggleMeter.gameObject.SetActive(true);
            UpdateStruggleMeter();
        }

        //taps reach us through the dialogue manager (stage tap target, Interact); Space is read in Update
        private void OnStageInput()
        {
            if (reachWalker) return; //only Interact acts in a reach (read in UpdateReach), not a tap on the stage
            if (struggleTarget <= 0) return;

            strugglePresses++;
            struggler.Play("struggle");
            UpdateStruggleMeter();
            if (strugglePresses < struggleTarget) return;

            struggleTarget = 0;
            DOVirtual.DelayedCall(0.35f, () =>
            {
                if (struggleMeter) struggleMeter.gameObject.SetActive(false);
                if (DialogueManager.HasInstance) DialogueManager.Instance.ReleaseStageHold();
            }, true).SetId(this);
        }

        private void Update()
        {
            if (struggleTarget > 0 && Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) OnStageInput();
            UpdateDoor(Time.unscaledDeltaTime);
            UpdateReach(Time.unscaledDeltaTime);

            //the line has finished typing: whoever was talking goes back to standing
            if (talking.Count > 0 && DialogueManager.HasInstance && !DialogueManager.Instance.DialogueIsTyping) StopTalking();
        }

        private void StartTalking(string speaker)
        {
            StopTalking();
            foreach (var actor in actors)
            {
                if (!castOnStage.Contains(actor.ActorId) || !actor.SpeakerId.Equals(speaker, StringComparison.OrdinalIgnoreCase)) continue;
                if (!actor.TryGetComponent(out StageActorAnimation animation) || !animation.HasClip(TalkClip)) continue;
                if (animation.CurrentClip != animation.DefaultClip) continue;
                animation.Play(TalkClip);
                talking.Add(animation);
            }
        }

        private void StopTalking()
        {
            foreach (var animation in talking)
            {
                if (animation && animation.CurrentClip == TalkClip) animation.Play(animation.DefaultClip);
            }
            talking.Clear();
        }

        private void UpdateStruggleMeter()
        {
            if (!struggleMeter) return;
            for (int i = 0; i < struggleMeter.childCount; i++)
            {
                if (struggleMeter.GetChild(i).TryGetComponent(out Graphic segment))
                    segment.color = i < strugglePresses ? struggleFilled : struggleEmpty;
            }
        }

        //while the stage holds the dialogue its panel (and the panel's tap area) is hidden: take the taps ourselves
        private void OnStagePause(bool paused)
        {
            if (stageTapTarget) stageTapTarget.SetActive(paused);
        }

        private StageActor FindActor(string id)
        {
            foreach (var actor in actors)
            {
                if (actor.ActorId.Equals(id.Trim(), StringComparison.OrdinalIgnoreCase)) return actor;
            }
            return null;
        }

        private bool VillainOnStage()
        {
            foreach (var actor in actors)
            {
                if (castOnStage.Contains(actor.ActorId) && actor.SpeakerId.Equals(villainSpeakerId, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static bool TryParse(string text, out float value) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

        private void SetCast(IEnumerable<string> ids)
        {
            castOnStage.Clear();
            foreach (var id in ids) castOnStage.Add(id.Trim());

            foreach (var actor in actors)
            {
                actor.SetVisible(castOnStage.Contains(actor.ActorId));
                actor.SetTint(listeningTint);
            }
        }

        /// <summary>
        /// "ActorId:clip" or "ActorId:clip:delaySeconds". Works on an actor that is not on stage yet, so it may come
        /// before or after the cast tag.
        /// </summary>
        private void PlayAnimation(string value)
        {
            var parts = value.Split(':');
            string id = parts[0].Trim();
            string clip = parts.Length > 1 ? parts[1].Trim() : "";
            float delay = 0f;
            if (parts.Length > 2) float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out delay);

            foreach (var actor in actors)
            {
                if (!actor.ActorId.Equals(id, StringComparison.OrdinalIgnoreCase)) continue;
                if (actor.TryGetComponent(out StageActorAnimation animation))
                {
                    animation.Play(clip, delay);
                    return;
                }
            }
            Debug.LogWarning($"Cutscene animation not found: {value}");
        }

        private void PlayEffect(string effect)
        {
            switch (effect)
            {
                case "shake":
                    Shake(14f);
                    break;
                case "flash":
                    Flash(Color.white);
                    break;
                case "red":
                    Flash(new Color(0.8f, 0.05f, 0.1f));
                    break;
                case "fade_black":
                    FadeOverlayTo(Color.black, 1f);
                    break;
                case "fade_white":
                    FadeOverlayTo(Color.white, 1f);
                    break;
                case "fade_in":
                    if (fadeOverlay) fadeOverlay.DOFade(0f, fadeTime).SetUpdate(true).SetId(this);
                    break;
                default:
                    Debug.LogWarning($"Unknown cutscene effect: {effect}");
                    break;
            }
        }

        private void Shake(float strength, float duration = 0.35f)
        {
            if (!shakeTarget) return;
            //a door banging during a big hit must not cut the big shake short
            if (Time.unscaledTime < shakeEnds && strength < shakeStrength) return;
            shakeEnds = Time.unscaledTime + duration;
            shakeStrength = strength;
            shakeTarget.DOComplete();
            shakeTarget.DOShakeAnchorPos(duration, strength, 30).SetUpdate(true).SetId(this);
        }

        // ------------------------------------------------------------------ flip, fade, door

        private void FlipActor(string value)
        {
            var parts = value.Split(':');
            var actor = FindActor(parts[0]);
            if (!actor)
            {
                Debug.LogWarning($"Cutscene actor not found: {value}");
                return;
            }
            bool on = parts.Length < 2 || parts[1].Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
            var scale = actor.transform.localScale;
            scale.x = Mathf.Abs(scale.x) * (on ? -1f : 1f);
            actor.transform.localScale = scale;
        }

        //"RRGGBB[:seconds]" fades the overlay in to that colour, "in[:seconds]" fades it away
        private void Fade(string value)
        {
            if (!fadeOverlay) return;
            var parts = value.Split(':');
            float seconds = fadeTime;
            if (parts.Length > 1) TryParse(parts[1], out seconds);

            fadeOverlay.DOKill();
            if (parts[0].Trim().Equals("in", StringComparison.OrdinalIgnoreCase))
            {
                if (seconds <= 0f) SetAlpha(fadeOverlay, 0f);
                else fadeOverlay.DOFade(0f, seconds).SetUpdate(true).SetId(this);
                return;
            }
            if (!ColorUtility.TryParseHtmlString("#" + parts[0].Trim(), out Color color))
            {
                Debug.LogWarning($"Cutscene fade is not RRGGBB[:seconds] or in[:seconds]: {value}");
                return;
            }
            fadeOverlay.color = new Color(color.r, color.g, color.b, seconds <= 0f ? 1f : fadeOverlay.color.a);
            if (seconds > 0f) fadeOverlay.DOFade(1f, seconds).SetUpdate(true).SetId(this);
        }

        private void SetBanging(string value)
        {
            var parts = value.Split(':');
            bool on = parts.Length < 2 || !parts[1].Trim().Equals("off", StringComparison.OrdinalIgnoreCase);
            bangingDoor = on ? FindActor(parts[0]) : null;
            nextBang = 0f;
        }

        private void Knock(string value)
        {
            var parts = value.Split(':');
            var door = FindActor(parts[0]);
            if (door) PlayBang(door);
            knockLevel = 1f;
            if (parts.Length > 1) TryParse(parts[1], out knockLevel);
            knockLeft = 0.6f;
        }

        private void PlayBang(StageActor door)
        {
            if (door.TryGetComponent(out StageActorAnimation animation)) animation.Play("bang");
        }

        private void UpdateDoor(float dt)
        {
            if (bangingDoor)
            {
                nextBang -= dt;
                if (nextBang <= 0f)
                {
                    nextBang = bangInterval + UnityEngine.Random.value * bangIntervalRandom;
                    PlayBang(bangingDoor);
                    Shake(4f, 0.14f);
                    if (!OnScreen((RectTransform)bangingDoor.transform)) bangTextLeft = 0.38f;
                }
            }

            if (bangText)
            {
                bangTextLeft -= dt;
                //gone as soon as the door itself comes into view (the camera pans towards it)
                bool show = bangTextLeft > 0f && !(bangingDoor && OnScreen((RectTransform)bangingDoor.transform));
                bangText.gameObject.SetActive(show);
                //jitters a pixel sideways while it shows, like the bang it stands for
                if (show) bangText.anchoredPosition = bangTextHome + new Vector2(Mathf.FloorToInt(bangTextLeft / 0.06f) % 2 * 4f, 0f);
            }

            if (knockText)
            {
                knockLeft -= dt;
                knockText.gameObject.SetActive(knockLeft > 0f);
                if (knockLeft > 0f) SetAlpha(knockText, Mathf.Min(1f, knockLeft / 0.3f) * knockLevel);
            }
        }

        // ------------------------------------------------------------------ attach, alpha, shake

        //"Follower:Target:dx,dy" or "Follower:none"; both actors must share a parent
        private void Attach(string value)
        {
            var parts = value.Split(':');
            var follower = FindActor(parts[0]);
            if (!follower)
            {
                Debug.LogWarning($"Cutscene actor not found: {value}");
                return;
            }
            var rect = (RectTransform)follower.transform;
            attached.Remove(rect);
            if (parts.Length < 2 || parts[1].Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) return;

            var target = FindActor(parts[1]);
            var offset = parts.Length > 2 ? parts[2].Split(',') : Array.Empty<string>();
            float dx = 0f, dy = 0f;
            if (!target || (offset.Length > 0 && !TryParse(offset[0], out dx)) || (offset.Length > 1 && !TryParse(offset[1], out dy)))
            {
                Debug.LogWarning($"Cutscene attach is not Actor:Target:dx,dy: {value}");
                return;
            }
            rect.DOKill();
            attached[rect] = ((RectTransform)target.transform, new Vector2(dx, dy));
            LateUpdate();
        }

        //after the tweens and the reach walk have moved the targets this frame
        private void LateUpdate()
        {
            foreach (var pair in attached)
            {
                if (pair.Key && pair.Value.target) pair.Key.anchoredPosition = pair.Value.target.anchoredPosition + pair.Value.offset;
            }
            if (followWorld && followWalker)
            {
                var position = followWorld.anchoredPosition;
                position.x = Mathf.Clamp(followScreenX - followWalker.anchoredPosition.x, followMin, followMax);
                followWorld.anchoredPosition = position;
            }
        }

        //"World:Walker:minX,maxX" or "none"; the walker keeps the place on screen it has when this starts
        private void Follow(string value)
        {
            followWorld = followWalker = null;
            var parts = value.Split(':');
            if (parts[0].Trim().Equals("none", StringComparison.OrdinalIgnoreCase)) return;
            var world = parts.Length == 3 ? FindActor(parts[0]) : null;
            var walker = world ? FindActor(parts[1]) : null;
            var range = parts.Length == 3 ? parts[2].Split(',') : Array.Empty<string>();
            if (!walker || walker.transform.parent != world.transform || range.Length != 2 ||
                !TryParse(range[0], out followMin) || !TryParse(range[1], out followMax))
            {
                Debug.LogWarning($"Cutscene follow is not World:Walker:minX,maxX with the walker inside the world: {value}");
                return;
            }
            followWorld = (RectTransform)world.transform;
            followWalker = (RectTransform)walker.transform;
            followWorld.DOKill();
            followScreenX = followWorld.anchoredPosition.x + followWalker.anchoredPosition.x;
        }

        //a CanvasGroup, so the opacity survives the per-line tint that sets each graphic's colour
        private void FadeActor(string value)
        {
            var parts = value.Split(':');
            var actor = FindActor(parts[0]);
            if (!actor || parts.Length < 2 || !TryParse(parts[1], out float alpha))
            {
                Debug.LogWarning($"Cutscene alpha is not Actor:opacity[:seconds]: {value}");
                return;
            }
            float seconds = 0f;
            if (parts.Length > 2) TryParse(parts[2], out seconds);
            if (!actor.TryGetComponent(out CanvasGroup group)) group = actor.gameObject.AddComponent<CanvasGroup>();
            group.DOKill();
            if (seconds <= 0f) group.alpha = alpha;
            else group.DOFade(alpha, seconds).SetEase(Ease.Linear).SetUpdate(true).SetId(this);
        }

        private void ShakeTag(string value)
        {
            var parts = value.Split(':');
            if (!TryParse(parts[0], out float strength))
            {
                Debug.LogWarning($"Cutscene shake is not units[:seconds]: {value}");
                return;
            }
            float seconds = 0.35f;
            if (parts.Length > 1) TryParse(parts[1], out seconds);
            Shake(strength, seconds);
        }

        // ------------------------------------------------------------------ reach

        //"Walker:Target:maxX[:promptKey]": the walker stays on its side of the target, can't pass it, nor go beyond maxX
        private void StartReach(string value)
        {
            var parts = value.Split(':');
            reachWalker = FindActor(parts[0]);
            reachTarget = parts.Length > 1 ? FindActor(parts[1]) : null;
            if (!reachWalker || !reachTarget || parts.Length < 3 || !TryParse(parts[2], out reachMaxX))
            {
                Debug.LogWarning($"Cutscene reach is not Walker:Target:maxX[:promptKey]: {value}");
                reachWalker = reachTarget = null;
                DialogueManager.Instance.ReleaseStageHold();
                return;
            }
            reachMoved = false;
            reachTime = 0f;
            //what the walker does once close: take the box, hold a hand...
            string prompt = parts.Length > 3 ? parts[3].Trim() : DefaultReachPrompt;
            var label = reachPrompt ? reachPrompt.GetComponentInChildren<LocalizedText>(true) : null;
            if (label) label.SetKeys(prompt, prompt + "_touch");
            //on phones the touch HUD (otherwise only in levels) brings its joystick and "!" button, in B's red
            InteractPrompt.ShowCutsceneControls(EColor.Red);
        }

        /// <summary>A reach is waiting for the player to walk up to its target and act.</summary>
        public bool IsReaching => reachWalker;
        public RectTransform ReachTarget => reachTarget ? (RectTransform)reachTarget.transform : null;
        public RectTransform ReachWalker => reachWalker ? (RectTransform)reachWalker.transform : null;

        private bool ReachNear() =>
            Mathf.Abs(((RectTransform)reachWalker.transform).anchoredPosition.x - ((RectTransform)reachTarget.transform).anchoredPosition.x) <= reachNearDistance + 0.01f;

        private void UpdateReach(float dt)
        {
            if (!reachWalker) return;
            var walker = (RectTransform)reachWalker.transform;
            float targetX = ((RectTransform)reachTarget.transform).anchoredPosition.x;
            var position = walker.anchoredPosition;
            float side = position.x >= targetX ? 1f : -1f;

            float direction = ReachDirection(walker);
            float stop = targetX + side * reachStopDistance;
            float x = Mathf.Clamp(position.x + direction * reachSpeed * dt, Mathf.Min(stop, reachMaxX), Mathf.Max(stop, reachMaxX));
            bool walking = Mathf.Abs(x - position.x) > 0.001f;
            if (direction != 0f)
            {
                reachMoved = true;
                var scale = walker.localScale;
                scale.x = Mathf.Abs(scale.x) * (direction < 0f ? -1f : 1f); //the art faces right
                walker.localScale = scale;
            }
            position.x = x;
            walker.anchoredPosition = position;
            if (reachWalker.TryGetComponent(out StageActorAnimation animation))
            {
                string clip = walking ? "run" : "idle";
                if (animation.CurrentClip != clip) animation.Play(clip);
            }

            reachTime += dt;
            bool close = ReachNear();
            InteractPrompt.SetInReach(this, close);
            ShowOver(reachPrompt, walker, close && reachTime % 0.8f < 0.4f);
            ShowOver(reachHint, walker, !close && !reachMoved && reachTime % 0.9f < 0.45f);
            if (close && InteractPressed()) FinishReach();
        }

        //the Interact input as the player knows it: E (Space in the team's preview), or Interact on a gamepad - which is
        //where the touch HUD's "!" button lands
        private static bool InteractPressed()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && (keyboard.eKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) return true;
            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.buttonNorth.wasPressedThisFrame) return true;
            }
            return false;
        }

        //-1, 0 or 1: the keys, or any gamepad's stick - the touch HUD's joystick drives a virtual one, which needn't be
        //Gamepad.current while a real pad is connected
        private static float ReachDirection(RectTransform walker)
        {
            float direction = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) direction -= 1f;
                if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) direction += 1f;
            }
            if (direction != 0f) return direction;
            foreach (var gamepad in Gamepad.all)
            {
                float stick = gamepad.leftStick.x.ReadValue() + gamepad.dpad.x.ReadValue();
                if (Mathf.Abs(stick) > 0.5f) return Mathf.Sign(stick);
            }
            return 0f;
        }

        private void ShowOver(RectTransform text, RectTransform walker, bool show)
        {
            if (!text) return;
            text.gameObject.SetActive(show);
            if (show) text.position = walker.parent.TransformPoint(walker.localPosition + Vector3.up * reachPromptHeight);
        }

        private void FinishReach()
        {
            if (reachWalker.TryGetComponent(out StageActorAnimation animation)) animation.Play("idle");
            EndReach();
            if (DialogueManager.HasInstance) DialogueManager.Instance.ReleaseStageHold();
        }

        private void EndReach()
        {
            reachWalker = reachTarget = null;
            if (reachPrompt) reachPrompt.gameObject.SetActive(false);
            if (reachHint) reachHint.gameObject.SetActive(false);
            InteractPrompt.SetInReach(this, false);
            InteractPrompt.HideCutsceneControls();
        }

        // ------------------------------------------------------------------ pixel transition

        //"cut[:seconds]", "in[:seconds]" or "RRGGBB[:seconds]"; the blocks go in one fixed random order, a step at a time
        private void PixelTransition(string value)
        {
            if (!shakeTarget) return;
            var parts = value.Split(':');
            string mode = parts[0].Trim();
            float seconds = pixelTime;
            if (parts.Length > 1) TryParse(parts[1], out seconds);
            EnsurePixelLayers();

            if (mode.Equals("cut", StringComparison.OrdinalIgnoreCase))
            {
                if (pixelCutTween != null && pixelCutTween.IsActive()) pixelCutTween.Complete();
                FreezeStage();
                pixelCutTween = PixelTween(pixelCutTexture, false, seconds,
                    () => DestroyChildren(pixelCutMask.transform, pixelCutMask.gameObject));
                return;
            }

            bool cover = !mode.Equals("in", StringComparison.OrdinalIgnoreCase);
            if (cover)
            {
                if (!ColorUtility.TryParseHtmlString("#" + mode, out Color color))
                {
                    Debug.LogWarning($"Cutscene pixel is not cut|in|RRGGBB[:seconds]: {value}");
                    return;
                }
                pixelCover.color = color;
            }
            if (pixelCoverTween != null && pixelCoverTween.IsActive()) pixelCoverTween.Kill();
            pixelCover.gameObject.SetActive(true);
            //covering grows the blocks from none, uncovering takes them away from all
            pixelCoverTween = PixelTween(pixelCoverTexture, cover, seconds, cover ? null : () => pixelCover.gameObject.SetActive(false));
        }

        private Tween PixelTween(Texture2D texture, bool growing, float seconds, TweenCallback done)
        {
            int shown = -1;
            void Step(float progress)
            {
                int step = Mathf.CeilToInt(progress * pixelSteps);
                if (step == shown) return;
                shown = step;
                float level = (float)step / pixelSteps;
                FillBlocks(texture, growing ? level : 1f - level);
            }
            Step(0f);
            if (seconds <= 0f)
            {
                Step(1f);
                done?.Invoke();
                return null;
            }
            return DOVirtual.Float(0f, 1f, seconds, Step).SetEase(Ease.Linear).SetUpdate(true).SetId(this).OnComplete(done);
        }

        //a block is opaque while its place in the order is below the level
        private void FillBlocks(Texture2D texture, float level)
        {
            var pixels = new Color32[pixelOrder.Length];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, (byte)(pixelOrder[i] < level ? 255 : 0));
            texture.SetPixels32(pixels);
            texture.Apply();
        }

        //one block grid over the whole stage, wide screens included, lined up with the art's 2x2 pixels
        private void EnsurePixelLayers()
        {
            if (pixelCover) return;
            var size = shakeTarget.rect.size;
            //wide enough for any phone (up to 2.5:1), so a later resize or rotation never leaves the sides uncovered
            size.x = Mathf.Max(size.x, size.y * 2.5f);
            int columns = 2 * Mathf.CeilToInt(size.x / pixelBlockSize / 2f);
            int rows = 2 * Mathf.CeilToInt((size.y / pixelBlockSize - 1f) / 2f) + 1;
            var random = new System.Random(7);
            var order = new int[columns * rows];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            pixelOrder = new float[order.Length];
            for (int i = 0; i < order.Length; i++) pixelOrder[order[i]] = (i + 0.5f) / order.Length;

            var gridSize = new Vector2(columns, rows) * pixelBlockSize;
            pixelCutMask = CreateBlockLayer("Pixel Cut", gridSize, columns, rows, out pixelCutTexture);
            pixelCutMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            pixelCover = CreateBlockLayer("Pixel Cover", gridSize, columns, rows, out pixelCoverTexture);
            pixelCover.gameObject.SetActive(false);
        }

        private RawImage CreateBlockLayer(string layerName, Vector2 size, int columns, int rows, out Texture2D texture)
        {
            var rect = new GameObject(layerName, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(shakeTarget, false);
            rect.sizeDelta = size;
            texture = new Texture2D(columns, rows, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;
            FillBlocks(texture, 0f);
            return image;
        }

        //copies of every picture on stage as it is this frame, in drawing order, under the block mask
        private void FreezeStage()
        {
            var cut = (RectTransform)pixelCutMask.transform;
            cut.gameObject.SetActive(true);
            cut.SetAsLastSibling();
            pixelCover.transform.SetAsLastSibling();
            foreach (var source in shakeTarget.GetComponentsInChildren<Image>())
            {
                if (!source.enabled || source.transform.IsChildOf(cut)) continue;
                var copy = new GameObject(source.name, typeof(RectTransform)).GetComponent<RectTransform>();
                copy.SetParent(cut, false);
                var from = source.rectTransform;
                copy.pivot = from.pivot;
                copy.sizeDelta = from.rect.size;
                copy.SetPositionAndRotation(from.position, from.rotation);
                var scale = from.lossyScale;
                var parentScale = cut.lossyScale;
                copy.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, 1f);

                var image = copy.gameObject.AddComponent<Image>();
                image.sprite = source.sprite;
                image.type = source.type;
                image.preserveAspect = source.preserveAspect;
                image.material = source.material;
                image.raycastTarget = false;
                var color = source.color;
                color.a *= source.canvasRenderer.GetInheritedAlpha();
                image.color = color;
            }
        }

        private static void DestroyChildren(Transform parent, GameObject hide)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
            hide.SetActive(false);
        }

        //is any part of the rect inside the stage (the visible screen)?
        private bool OnScreen(RectTransform rect)
        {
            if (!shakeTarget) return true;
            var corners = new Vector3[4];
            var screen = new Vector3[4];
            rect.GetWorldCorners(corners);
            shakeTarget.GetWorldCorners(screen);
            return corners[2].x > screen[0].x && corners[0].x < screen[2].x;
        }

        private void Flash(Color color)
        {
            if (!fadeOverlay) return;
            fadeOverlay.color = new Color(color.r, color.g, color.b, 0.9f);
            fadeOverlay.DOFade(0f, 0.5f).SetUpdate(true).SetId(this);
        }

        private void FadeOverlayTo(Color color, float alpha)
        {
            if (!fadeOverlay) return;
            fadeOverlay.color = new Color(color.r, color.g, color.b, fadeOverlay.color.a);
            fadeOverlay.DOFade(alpha, fadeTime).SetUpdate(true).SetId(this);
        }

        private void PlaySound(string soundName)
        {
            switch (soundName)
            {
                case "beep":
                    if (!loopSource) return;
                    monitorBeepClip ??= CreateMonitorBeepClip();
                    loopSource.clip = monitorBeepClip;
                    loopSource.loop = true;
                    loopSource.Play();
                    return;
                case "stop":
                    if (loopSource) loopSource.Stop();
                    return;
            }

            if (oneShotSource && sounds.TryGetValue(soundName, out AudioClip clip) && clip)
            {
                oneShotSource.PlayOneShot(clip);
            }
            else
            {
                Debug.LogWarning($"Unknown cutscene sound: {soundName}");
            }
        }

        private void PlayMusic(string trackName)
        {
            if (!MusicManager.HasInstance) return;
            if (trackName.Equals("stop", StringComparison.OrdinalIgnoreCase)) MusicManager.Instance.StopCurrentTrack();
            else if (music != null && music.TryGetValue(trackName, out AudioClip clip) && clip) MusicManager.Instance.Play(clip, loop: true);
            else Debug.LogWarning($"Unknown cutscene music: {trackName}");
        }

        /// <summary>
        /// One second of a hospital heart monitor: a short 1 kHz beep followed by silence. The project has no such clip.
        /// </summary>
        private static AudioClip CreateMonitorBeepClip()
        {
            const int sampleRate = 44100;
            const float beepLength = 0.12f;
            var samples = new float[sampleRate];
            int beepSamples = (int)(sampleRate * beepLength);

            for (int i = 0; i < beepSamples; i++)
            {
                float envelope = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (beepSamples - i) / 400f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * 1000f * i / sampleRate) * 0.25f * envelope;
            }

            var clip = AudioClip.Create("Heart Monitor Beep", samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static void SetAlpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }
    }
}
