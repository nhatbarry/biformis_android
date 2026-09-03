using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using ZLinq;

namespace CaptainPinkTurd.EffectSystem.ShakeEffect
{
    public static class ShakeUtils
    {
        /// <summary>
        /// Applies synchronized shake animation to multiple transforms using the specified shake profile.
        /// All transforms will shake with the same random pattern for visual consistency.
        /// </summary>
        /// <param name="shakeObjects">List of transforms to shake</param>
        /// <param name="shakeProfile">Shake profile containing animation parameters</param>
        /// <param name="synchronizedRandomSeed">Random seed for synchronized shake pattern</param>
        public static void SynchronizedShakeWithProfile(List<Transform> shakeObjects, GameObjectShakeProfile shakeProfile, int synchronizedRandomSeed = 12345)
        {
            UnityEngine.Random.State originalState = UnityEngine.Random.state;
                
            // Same rule as HitStop: never save a clock that is already stopped, or the restore below hands
            // back a frozen game. The shake is driven with SetUpdate(Normal, true) so it runs regardless.
            float oldTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0;

            foreach (var (shakeObj, index) in shakeObjects.AsValueEnumerable().Select((shakeObj, i) => (shakeObj, i)))
            {
                UnityEngine.Random.InitState(synchronizedRandomSeed);
                bool isLast = index == shakeObjects.Count - 1;
        
                shakeObj.DOShakePosition(shakeProfile.defaultShakeDuration,
                        shakeProfile.shakeStrength,
                        shakeProfile.vibration,
                        shakeProfile.randomness,
                        shakeProfile.snapping,
                        shakeProfile.fadeOut)
                    .SetUpdate(UpdateType.Normal, true)
                    // OnKill rather than OnComplete: a tween killed early - the object was destroyed, the
                    // scene unloaded - would never reach OnComplete, and the game would stay frozen forever.
                    .OnKill(() =>
                    {
                        if (!isLast) return;

                        Time.timeScale = oldTimeScale;
                    });
            }
            UnityEngine.Random.state = originalState;
        }
    }
}