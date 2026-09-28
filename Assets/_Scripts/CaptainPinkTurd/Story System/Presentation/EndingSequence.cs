using System.Collections;
using System.Collections.Generic;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.Localization;
using CaptainPinkTurd.Scene.Story;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CaptainPinkTurd.Story.Presentation
{
    /// <summary>
    /// The ending, built entirely in code (per the script):
    /// the levels burst into pixel blocks on white, B's second personality appears, waves and dissolves into pixels,
    /// then A and B - now two separate people - walk off screen together, and the screen fades to white.
    /// </summary>
    public class EndingSequence : MonoBehaviour
    {
        [SerializeField] private StoryData storyData;
        [SerializeField] private TMP_FontAsset font;
        [SerializeField] private AudioClip music;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip burstSfx;

        [Header("Characters")]
        [SerializeField] private Sprite bIdle;
        [SerializeField] private Sprite[] bWalk;
        [SerializeField] private Sprite aIdle;
        [SerializeField] private Sprite[] aWalk;
        [Tooltip("How the second personality looks: B's sprite in this tint")]
        [SerializeField] private Color secondPersonalityTint = new(0.35f, 0.05f, 0.12f, 1f);
        [SerializeField] private float characterHeight = 96f;

        [Header("Level debris")]
        [SerializeField] private Color[] debrisColors =
        {
            new(0.18f, 0f, 0.38f), new(0.07f, 0.03f, 0.2f), new(0.95f, 0.33f, 0.33f),
            new(0.56f, 0.89f, 0.8f), new(0.24f, 0.45f, 0.5f), new(0.1f, 0.12f, 0.2f),
        };
        [SerializeField] private int debrisColumns = 32;
        [SerializeField] private int debrisRows = 18;

        private RectTransform stage;

        private IEnumerator Start()
        {
            if (music) MusicManager.Instance.Play(music, loop: true);

            var canvas = OverlayCanvas.Create(transform, "Ending Canvas", 10);
            OverlayCanvas.CreateImage(canvas.transform, "White", Color.white, true);
            stage = (RectTransform)new GameObject("Stage", typeof(RectTransform)).transform;
            stage.SetParent(canvas.transform, false);
            OverlayCanvas.Stretch(stage);

            yield return BurstLevels();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return SecondPersonalityFarewell();
            yield return new WaitForSecondsRealtime(1f);
            yield return WalkOffTogether();
            yield return new WaitForSecondsRealtime(1f);
            yield return TheEnd();

            if (StoryFlow.IsStoryRunning) StoryFlow.Advance(storyData);
        }

        private IEnumerator BurstLevels()
        {
            var size = new Vector2(640f / debrisColumns, 360f / debrisRows);
            var blocks = new List<(Image image, float distance)>();

            for (int x = 0; x < debrisColumns; x++)
            {
                for (int y = 0; y < debrisRows; y++)
                {
                    var block = CreateImage("Block", debrisColors[Random.Range(0, debrisColors.Length)], size + Vector2.one);
                    var position = new Vector2((x + 0.5f) * size.x - 320f, (y + 0.5f) * size.y - 180f);
                    block.rectTransform.anchoredPosition = position;
                    blocks.Add((block, position.magnitude));
                }
            }

            yield return new WaitForSecondsRealtime(1.2f);
            if (sfxSource && burstSfx) sfxSource.PlayOneShot(burstSfx);

            //one after another from the middle outwards, like the levels disappearing in turn
            const float spread = 2.5f;
            foreach (var (image, distance) in blocks)
            {
                float delay = distance / 367f * spread + Random.Range(0f, 0.25f);
                FlyApart(image, delay, 1.2f, 260f);
            }
            yield return new WaitForSecondsRealtime(spread + 1.6f);
        }

        private IEnumerator SecondPersonalityFarewell()
        {
            var figure = CreateCharacter("Second Personality", bIdle, Vector2.zero);
            figure.color = new Color(secondPersonalityTint.r, secondPersonalityTint.g, secondPersonalityTint.b, 0f);
            yield return figure.DOFade(1f, 1.2f).SetUpdate(true).WaitForCompletion();
            yield return new WaitForSecondsRealtime(1.5f);

            //wave goodbye
            var rect = figure.rectTransform;
            yield return DOTween.Sequence()
                .Append(rect.DOLocalRotate(new Vector3(0f, 0f, -12f), 0.18f))
                .Append(rect.DOLocalRotate(new Vector3(0f, 0f, 12f), 0.3f))
                .Append(rect.DOLocalRotate(new Vector3(0f, 0f, -12f), 0.3f))
                .Append(rect.DOLocalRotate(new Vector3(0f, 0f, 12f), 0.3f))
                .Append(rect.DOLocalRotate(Vector3.zero, 0.18f))
                .SetUpdate(true).WaitForCompletion();
            yield return new WaitForSecondsRealtime(0.8f);

            //dissolve into pixels that drift away
            var bounds = rect.sizeDelta;
            for (int i = 0; i < 70; i++)
            {
                var pixel = CreateImage("Pixel", secondPersonalityTint, Vector2.one * Random.Range(4f, 8f));
                pixel.rectTransform.anchoredPosition = new Vector2(Random.Range(-0.35f, 0.35f) * bounds.x, Random.Range(-0.5f, 0.5f) * bounds.y);
                FlyApart(pixel, Random.Range(0f, 0.8f), 1.6f, 160f, upward: true);
            }
            figure.DOFade(0f, 1f).SetUpdate(true);
            yield return new WaitForSecondsRealtime(2.6f);
            Destroy(figure.gameObject);
        }

        private IEnumerator WalkOffTogether()
        {
            //apart at first, then they walk off together
            var b = CreateCharacter("B", bIdle, new Vector2(-150f, -20f));
            var a = CreateCharacter("A", aIdle, new Vector2(150f, -20f));
            b.color = a.color = new Color(1f, 1f, 1f, 0f);
            b.DOFade(1f, 1f).SetUpdate(true);
            yield return a.DOFade(1f, 1f).SetUpdate(true).WaitForCompletion();
            yield return new WaitForSecondsRealtime(1.2f);

            Walk(b, bWalk);
            Walk(a, aWalk);
            b.rectTransform.DOAnchorPosX(-30f, 1.4f).SetEase(Ease.Linear).SetUpdate(true);
            yield return a.rectTransform.DOAnchorPosX(30f, 1.4f).SetEase(Ease.Linear).SetUpdate(true).WaitForCompletion();
            yield return new WaitForSecondsRealtime(0.4f);

            b.rectTransform.DOAnchorPosX(420f, 4f).SetEase(Ease.Linear).SetUpdate(true);
            yield return a.rectTransform.DOAnchorPosX(480f, 4f).SetEase(Ease.Linear).SetUpdate(true).WaitForCompletion();
        }

        private IEnumerator TheEnd()
        {
            var go = new GameObject("The End", typeof(RectTransform));
            go.transform.SetParent(stage, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            text.text = Localization.Get("ending.the_end");
            text.fontSize = 64f;
            text.color = new Color(0.1f, 0.1f, 0.15f, 0f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            OverlayCanvas.Stretch(text.rectTransform);

            yield return text.DOFade(1f, 2f).SetUpdate(true).WaitForCompletion();
            yield return new WaitForSecondsRealtime(3f);
        }

        private void Walk(Image character, Sprite[] frames)
        {
            if (frames == null || frames.Length == 0) return;
            character.gameObject.AddComponent<SpriteFrameAnimator>().Play(frames, 8f);
        }

        private Image CreateCharacter(string name, Sprite sprite, Vector2 position)
        {
            var image = CreateImage(name, Color.white, Vector2.one * characterHeight);
            image.sprite = sprite;
            image.preserveAspect = true;
            image.rectTransform.anchoredPosition = position;
            return image;
        }

        private Image CreateImage(string name, Color color, Vector2 size)
        {
            var image = OverlayCanvas.CreateImage(stage, name, color, false);
            image.rectTransform.sizeDelta = size;
            return image;
        }

        private static void FlyApart(Image image, float delay, float duration, float distance, bool upward = false)
        {
            var rect = image.rectTransform;
            var direction = upward
                ? new Vector2(Random.Range(-0.6f, 0.6f), 1f).normalized
                : (rect.anchoredPosition.sqrMagnitude > 1f ? rect.anchoredPosition.normalized : Random.insideUnitCircle.normalized);
            direction = (direction + Random.insideUnitCircle * 0.4f).normalized;

            DOTween.Sequence()
                .SetDelay(delay)
                .Append(rect.DOAnchorPos(rect.anchoredPosition + direction * distance * Random.Range(0.6f, 1.2f), duration).SetEase(Ease.OutCubic))
                .Join(rect.DOLocalRotate(new Vector3(0f, 0f, Random.Range(-270f, 270f)), duration, RotateMode.FastBeyond360))
                .Join(rect.DOScale(0.2f, duration).SetEase(Ease.InQuad))
                .Join(image.DOFade(0f, duration).SetEase(Ease.InQuad))
                .OnComplete(() => Destroy(image.gameObject))
                .SetUpdate(true)
                .SetLink(image.gameObject);
        }
    }
}
