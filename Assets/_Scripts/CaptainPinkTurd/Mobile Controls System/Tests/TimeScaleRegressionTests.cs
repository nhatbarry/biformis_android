using System.Collections;
using CaptainPinkTurd.Core.Utilities;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CaptainPinkTurd.MobileControls.Tests
{
    /// <summary>
    /// Guards the way a level could come back from a restart with the clock stopped: the player could still
    /// turn on the spot, because Update keeps running, but never moved again, because FixedUpdate does not.
    /// </summary>
    public class TimeScaleRegressionTests
    {
        [SetUp]
        public void SetUp() => HitStop.Abort();

        [TearDown]
        public void TearDown()
        {
            HitStop.Abort();
            Time.timeScale = 1f;
        }

        /// <summary>
        /// A pause popup and the game over screen both park the time scale at zero. Anything that gets hit
        /// while one of those is up used to save that zero and hand it back when the hit-stop finished.
        /// </summary>
        [UnityTest]
        public IEnumerator HitStopNeverRestoresAFrozenClock()
        {
            Time.timeScale = 0f;

            HitStop.Stop(0.05f);
            yield return new WaitForSecondsRealtime(0.3f);

            Assert.Greater(Time.timeScale, 0f,
                "a hit-stop that began while the game was already frozen must not hand that freeze back");
        }

        /// <summary>
        /// The hit-stop runner is DontDestroyOnLoad and counts unscaled, so one started just before a level
        /// transition outlives it. SceneController aborts it for exactly this reason.
        /// </summary>
        [UnityTest]
        public IEnumerator AnAbortedHitStopNeverWritesItsSavedClockLater()
        {
            HitStop.Stop(0.1f);
            Assert.AreEqual(0f, Time.timeScale, "a hit-stop should freeze the game while it runs");

            // What SceneController does when a transition starts.
            HitStop.Abort();
            Time.timeScale = 1f;

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.AreEqual(1f, Time.timeScale,
                "the aborted hit-stop must not stomp the time scale the new scene set");
        }

        [UnityTest]
        public IEnumerator AHitStopStillRestoresANormalClock()
        {
            Time.timeScale = 1f;

            HitStop.Stop(0.05f);
            Assert.AreEqual(0f, Time.timeScale, "the hit-stop should freeze the game");

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.AreEqual(1f, Time.timeScale, "and give the clock back when it ends");
        }
    }
}
