using System;
using System.Collections;
using UnityEngine;

namespace CaptainPinkTurd.Core.Utils
{
    public static class CoroutineUtils
    {
        /// <summary>
        /// Waits until a condition is met and then executes the callback.
        /// </summary>
        /// <param name="condition">The function that should return true when the condition is met.</param>
        /// <param name="onAwaitComplete">The action to call upon completion.</param>
        /// /// <param name="cancelCondition">Optional cancellation condition.</param>
        /// <returns>An IEnumerator to be used in a coroutine.</returns>
        public static IEnumerator WaitForCondition(Func<bool> condition,
            Action onAwaitComplete, Func<bool> cancelCondition = null)
        {
            // Loop until either the condition is met or canceled
            while (true)
            {
                if (cancelCondition?.Invoke() == true)
                {
                    //Debug.Log("Cancel condition met for WaitForCondition");
                    yield break;
                }

                if (condition?.Invoke() == true)
                    break;

                yield return null;
            }

            onAwaitComplete?.Invoke();
        }

        /// <summary>
        ///  Waits until waitTime is over and execute Action.
        /// </summary>
        /// <param name="waitTime">The amount of time (in seconds) to wait.</param>
        /// <param name="onAwaitComplete">The action to call upon completion.</param>
        /// <param name="cancelCondition">Optional cancellation condition. If true, stops the wait early.</param>
        /// <param name="ignoreTimeScale"></param>
        /// <returns>An IEnumerator to be used in a coroutine.</returns>
        public static IEnumerator WaitForSeconds(float waitTime, Action onAwaitComplete, 
            Func<bool> cancelCondition = null, bool ignoreTimeScale = false)
        {
            float elapsedTime = 0f;
            
            while (elapsedTime < waitTime)
            {
                if (cancelCondition?.Invoke() == true)
                {
                    yield break;
                }
        
                elapsedTime += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }
            
            onAwaitComplete?.Invoke();
        }

        /// <summary>
        /// Waits until the next frame and then executes the callback.
        /// </summary>
        /// <param name="onAwaitComplete">The action to call on the next n frame.</param>
        /// <param name="framesToWait">The number of frames needed to wait for</param>
        /// <param name="cancelCondition">Optional cancellation condition. If true before next frame, stops early.</param>
        /// <returns>An IEnumerator to be used in a coroutine.</returns>
        public static IEnumerator WaitForNextFrames(Action onAwaitComplete, int framesToWait = 1, Func<bool> cancelCondition = null)
        {
            // Check cancellation immediately (same behavior pattern as your other methods)
            if (cancelCondition?.Invoke() == true)
                yield break;

            for (int i = 0; i < framesToWait; i++)
            {
                yield return null;
            }
        
            // Check again in case something changed during the frame
            if (cancelCondition?.Invoke() == true)
                yield break;
        
            onAwaitComplete?.Invoke();
        }
    }
}