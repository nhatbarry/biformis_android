using CaptainPinkTurd.Core.Struct;
using CaptainPinkTurd.Game.Enemy;
using CaptainPinkTurd.Scene.Manager;
using UnityEngine;

namespace CaptainPinkTurd.Story
{
    /// <summary>Level six advances directly to its next cutscene when the boss dies.</summary>
    public class BossFightStoryExit : MonoBehaviour
    {
        [SerializeField] private PlagueDoctorBoss boss;
        [SerializeField] private LevelManager level;
        private bool completed;

        private void OnEnable()
        {
            completed = false;
            boss.OnDeath.Subscribe(OnBossDeath);
        }
        private void OnDisable() => boss.OnDeath.Unsubscribe(OnBossDeath);
        private void OnBossDeath(SDamageData damage)
        {
            if (completed) return;
            completed = true;
            level.NextLevel();
        }
    }
}
