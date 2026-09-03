using System;
using System.Collections;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CaptainPinkTurd.Core.Utilities
{
    public static class HitStop
    {
        public static bool IsWaiting => running;

        private static bool running;
        private static float remainingTime;
        private static float oldTimeScale;

        // Queue all callbacks
        private static Action pendingCallbacks;

        private class HitStopRunner : MonoBehaviour { }

        private static HitStopRunner runner;

        private static void EnsureRunner()
        {
            if (runner) return;

            var go = new GameObject("[HitStopRunner]");
            runner = go.AddComponent<HitStopRunner>();
            Object.DontDestroyOnLoad(go);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ResetStaticVariables()
        {
            running = false;
            remainingTime = 0f;
            pendingCallbacks = null;
        }

        /// <summary>
        /// Drops any hit-stop in flight and hands the clock back to the caller.
        /// </summary>
        /// <remarks>
        /// The runner survives scene changes and counts in unscaled time, so a hit-stop started just before a
        /// level transition keeps running through it and then writes its saved time scale over whatever the
        /// new scene set - which is how a restart could leave the game running at timeScale 0. Pending
        /// callbacks are dropped on purpose: they act on objects in the scene that is being torn down.
        /// </remarks>
        public static void Abort()
        {
            if (runner) runner.StopAllCoroutines();

            running = false;
            remainingTime = 0f;
            pendingCallbacks = null;
            oldTimeScale = 1f;
        }

        public static void Stop(float duration, Action onStopEnd = null)
        {
            EnsureRunner();

            // Always queue callback (even if already running)
            if (onStopEnd != null)
            {
                pendingCallbacks -= onStopEnd; //prevent duplicates
                pendingCallbacks += onStopEnd; 
            }
            
            // Stack duration
            remainingTime = Mathf.Max(remainingTime, duration);

            // If already running, return after extending remainingTime
            if (running) return;
            
            running = true;

            // Never take a frozen clock as the value to restore. A pause popup, a game over screen or a
            // synchronized shake may already have zeroed the time scale, and handing that back when the
            // hit-stop ends leaves the game stopped for good - the player can still turn on the spot,
            // because Update keeps running, but never moves again, because FixedUpdate does not.
            oldTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0.0f;

            runner.StartCoroutine(WaitLoop());
        }

        private static IEnumerator WaitLoop()
        {
            do
            {
                while (remainingTime > 0f)
                {
                    remainingTime -= Time.unscaledDeltaTime;
                    yield return null;
                }

                Time.timeScale = oldTimeScale;

                // Snapshot and clear before invoking so a Stop() call made
                // re-entrantly from within a callback (e.g. OnDeath triggered
                // by this callback's damage) queues into a fresh delegate
                // instead of being wiped out by the reset below.
                var callbacksToRun = pendingCallbacks;
                pendingCallbacks = null;

                try
                {
                    callbacksToRun?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogError($"HitStop callback error: {e}");
                }

                // If a re-entrant Stop() requested another hit-stop window,
                // re-pause and loop again instead of resetting now.
                if (remainingTime > 0f)
                {
                    Time.timeScale = 0f;
                }
            } while (remainingTime > 0f || pendingCallbacks != null);

            running = false;
        }
    }
}