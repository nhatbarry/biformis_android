using System;
using System.Collections.Generic;
using System.Globalization;
using CaptainPinkTurd.Core.CustomDataStructure;
using CaptainPinkTurd.Core.Extensions;
using CaptainPinkTurd.InkDialogue;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Cutscene
{
    /// <summary>
    /// Stages a cutscene from ink tags, so the writer controls what is on screen from the script:
    ///   #bg:white | black | hospital | past | RRGGBB[:seconds]   backdrop colour (a name from backdropColors or hex)
    ///   #cast:A,B,Teen | none                    which StageActors stand on stage
    ///   #fx:shake | flash | red | fade_black | fade_white | fade_in
    ///   #sfx:beep | stop | &lt;name in sounds&gt;     "beep" loops a heart monitor, "stop" ends any loop
    ///   #anim:ActorId:clip[:seconds]               plays a clip of an actor's StageActorAnimation, optionally after a delay
    ///                                              (e.g. anim:B_Bed:wake:1.2)
    ///   #move:ActorId:x[:seconds]                  slides an actor to a canvas x position (instantly without seconds)
    /// The actor who is speaking is lit, the others take listeningTint. When the villain speaks without being on stage,
    /// its lines tint the screen red and shake it.
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

        [Header("Sound")]
        [SerializeField] private AudioSource oneShotSource;
        [SerializeField] private AudioSource loopSource;
        [SerializeField] private SerializeKeyValuePair<string, AudioClip>[] sounds;

        private readonly HashSet<string> castOnStage = new(StringComparer.OrdinalIgnoreCase);
        private AudioClip monitorBeepClip;
        private Tween villainTween;

        private void Awake()
        {
            SetCast(Array.Empty<string>());
            if (villainOverlay) SetAlpha(villainOverlay, 0f);
            if (fadeOverlay) SetAlpha(fadeOverlay, 0f);
        }

        //Start, not OnEnable: DialogueManager creates these events in its Awake, which may run after our OnEnable
        private void Start()
        {
            var dialogueManager = DialogueManager.Instance;
            dialogueManager.OnStageTag.Subscribe(HandleStageTag);
            dialogueManager.OnDisplayDialogue.Subscribe(OnDisplayDialogue);
        }

        private void OnDestroy()
        {
            DOTween.Kill(this);

            if (!DialogueManager.HasInstance) return;
            DialogueManager.Instance.OnStageTag.Unsubscribe(HandleStageTag);
            DialogueManager.Instance.OnDisplayDialogue.Unsubscribe(OnDisplayDialogue);
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
            bool villainSpeaking = info.speaker == villainSpeakerId && !castOnStage.Contains(villainSpeakerId);
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
            if (parts.Length < 2 || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
            {
                Debug.LogWarning($"Cutscene move is not Actor:x[:seconds]: {value}");
                return;
            }
            float seconds = 0f;
            if (parts.Length > 2) float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds);

            foreach (var actor in actors)
            {
                if (!actor.ActorId.Equals(parts[0].Trim(), StringComparison.OrdinalIgnoreCase)) continue;
                var rect = (RectTransform)actor.transform;
                rect.DOKill();
                if (seconds <= 0f) rect.anchoredPosition = new Vector2(x, rect.anchoredPosition.y);
                else rect.DOAnchorPosX(x, seconds).SetEase(Ease.Linear).SetUpdate(true).SetId(this);
                return;
            }
            Debug.LogWarning($"Cutscene actor not found: {value}");
        }

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

        private void Shake(float strength)
        {
            if (!shakeTarget) return;
            shakeTarget.DOComplete();
            shakeTarget.DOShakeAnchorPos(0.35f, strength, 30).SetUpdate(true).SetId(this);
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
