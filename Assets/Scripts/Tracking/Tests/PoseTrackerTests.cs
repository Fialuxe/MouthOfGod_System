using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Tracking.Tests
{
    public class PoseTrackerTests
    {
        const float Tolerance = 1e-4f;

        static Pose At(float x) => new Pose(new Vector3(x, 0f, 0f), Quaternion.identity);

        [Test]
        public void Observe_FirstTime_SnapsToPoseAndRaisesFound()
        {
            var tracker = new PoseTracker(smoothingSeconds: 1f, lostTimeoutSeconds: 0.5f);
            var found = 0;
            tracker.Found += () => found++;

            tracker.Observe(At(5f), now: 0d);

            Assert.IsTrue(tracker.IsTracked);
            Assert.AreEqual(5f, tracker.Current.position.x, Tolerance);
            Assert.AreEqual(1, found);
        }

        [Test]
        public void Observe_WhileTracked_DoesNotRaiseFoundAgain()
        {
            var tracker = new PoseTracker(1f, 0.5f);
            var found = 0;
            tracker.Found += () => found++;

            tracker.Observe(At(0f), 0d);
            tracker.Observe(At(1f), 0.1d);

            Assert.AreEqual(1, found);
        }

        [Test]
        public void Tick_WithSmoothing_MovesPartiallyTowardTarget()
        {
            var tracker = new PoseTracker(smoothingSeconds: 1f, lostTimeoutSeconds: 10f);
            tracker.Observe(At(0f), 0d);
            tracker.Observe(At(1f), 0.5d);

            tracker.Tick(now: 1d, deltaTime: 1f);

            // 時定数と同じ時間が経つと、目標までの 1 - e^-1 (約 63%) 進む。
            Assert.AreEqual(1f - Mathf.Exp(-1f), tracker.Current.position.x, Tolerance);
        }

        [Test]
        public void Tick_WithoutSmoothing_FollowsTargetImmediately()
        {
            var tracker = new PoseTracker(smoothingSeconds: 0f, lostTimeoutSeconds: 10f);
            tracker.Observe(At(0f), 0d);
            tracker.Observe(At(3f), 0.1d);

            tracker.Tick(0.2d, 0.016f);

            Assert.AreEqual(3f, tracker.Current.position.x, Tolerance);
        }

        [Test]
        public void Tick_AfterTimeout_RaisesLostOnceAndKeepsLastPose()
        {
            var tracker = new PoseTracker(0f, lostTimeoutSeconds: 0.5f);
            var lost = 0;
            tracker.Lost += () => lost++;
            tracker.Observe(At(2f), 0d);

            tracker.Tick(0.4d, 0.016f);
            Assert.IsTrue(tracker.IsTracked);

            tracker.Tick(0.6d, 0.016f);
            tracker.Tick(0.7d, 0.016f);

            Assert.IsFalse(tracker.IsTracked);
            Assert.AreEqual(1, lost);
            Assert.AreEqual(2f, tracker.Current.position.x, Tolerance);
        }

        [Test]
        public void Observe_AfterLost_SnapsAndRaisesFoundAgain()
        {
            var tracker = new PoseTracker(1f, 0.5f);
            var found = 0;
            tracker.Found += () => found++;
            tracker.Observe(At(0f), 0d);
            tracker.Tick(1d, 0.016f);

            tracker.Observe(At(9f), 2d);

            Assert.IsTrue(tracker.IsTracked);
            Assert.AreEqual(9f, tracker.Current.position.x, Tolerance);
            Assert.AreEqual(2, found);
        }
    }
}
