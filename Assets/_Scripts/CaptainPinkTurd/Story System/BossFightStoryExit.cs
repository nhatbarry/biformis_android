using System.Collections;
using CaptainPinkTurd.AudioSystem;
using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Game.Player;
using CaptainPinkTurd.Scene.Manager;
using CaptainPinkTurd.Story.Presentation;
using UnityEngine;

namespace CaptainPinkTurd.Story
{
    /// <summary>
    /// Level six ends when the boss dies: the arena breaks like glass (GlassShatter), the boss and the player stay
    /// standing in the dark, and the story moves on to the ending's cutscene, which opens on that same picture.
    /// </summary>
    public class BossFightStoryExit : MonoBehaviour
    {
        [SerializeField] private PlagueDoctorBoss boss;
        [SerializeField] private LevelManager level;

        [Header("Boss music")]
        [SerializeField] private AudioClip phaseOneMusic;
        [SerializeField, Min(0f)] private float phaseOneMusicStartTime = 69f;
        [SerializeField] private AudioClip phaseTwoMusic;

        [Header("Glass shatter (set by Biformis > Ending > Build Ending)")]
        [SerializeField] private Sprite glow;
        [Tooltip("The defeated boss breathing: the pose it takes once the glass breaks")]
        [SerializeField] private Sprite[] bossPose;
        [SerializeField] private float[] bossPoseSeconds;
        [SerializeField] private Sprite redIdle;
        [SerializeField] private Sprite blueIdle;
        [SerializeField] private AudioClip breakSfx;

        private bool completed;

        public GlassShatter Shatter { get; private set; }

        private void OnEnable()
        {
            completed = false;
            boss.OnDeath.Subscribe(OnBossDeath);
            boss.OnPhaseTwoStarted.Subscribe(PlayPhaseTwoMusic);
        }

        private void OnDisable()
        {
            boss.OnDeath.Unsubscribe(OnBossDeath);
            boss.OnPhaseTwoStarted.Unsubscribe(PlayPhaseTwoMusic);
        }

        private IEnumerator Start()
        {
            yield return null; // Let the level manager finish its startup before taking over music.
            var entrance = FindAnyObjectByType<BossEntranceSequence>();
            while (entrance && !entrance.Finished) yield return null;
            if (!completed && isActiveAndEnabled && boss && boss.Phase == PlagueDoctorBoss.EPhase.Hooded &&
                phaseOneMusic && MusicManager.HasInstance)
                MusicManager.Instance.Play(phaseOneMusic, loop: true, startTime: phaseOneMusicStartTime);
        }

        private void PlayPhaseTwoMusic()
        {
            if (!completed && phaseTwoMusic && MusicManager.HasInstance)
                MusicManager.Instance.Play(phaseTwoMusic, loop: true);
        }

        private void OnBossDeath(SDamageData damage)
        {
            if (completed) return;
            completed = true;
            if (MusicManager.HasInstance) MusicManager.Instance.StopCurrentTrack();
            StartCoroutine(BreakThenLeave());
        }

        private IEnumerator BreakThenLeave()
        {
            //hides the HUD and holds the player still, as for the boss's transformation
            var arena = FindAnyObjectByType<BossArenaController>();
            if (arena) arena.BeginPhaseTwoPresentation();
            var player = FindAnyObjectByType<PlayerUnit>();

            Shatter = GlassShatter.Play(new GlassShatter.Settings
            {
                camera = Camera.main,
                boss = boss.transform,
                player = player ? player.transform : null,
                glow = glow,
                bossPose = bossPose,
                bossPoseSeconds = bossPoseSeconds,
                redIdle = redIdle,
                blueIdle = blueIdle,
                breakSfx = breakSfx,
            });
            Shatter.transform.SetParent(transform, false);
            //realtime: the killing blow's hit-stop may still hold the clock
            float end = Time.realtimeSinceStartup + 10f;
            while (!Shatter.Finished && Time.realtimeSinceStartup < end) yield return null;
            level.NextLevel();
        }
    }
}
