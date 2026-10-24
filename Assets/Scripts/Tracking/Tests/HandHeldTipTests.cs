using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Tracking.Tests
{
    public class HandHeldTipTests
    {
        const float Tolerance = 1e-4f;

        static Pose Hand(Vector3 position, Quaternion rotation) => new Pose(position, rotation);

        static void AssertVector(Vector3 expected, Vector3 actual)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance);
            Assert.AreEqual(expected.y, actual.y, Tolerance);
            Assert.AreEqual(expected.z, actual.z, Tolerance);
        }

        [Test]
        public void Compute_NoRotation_AddsOffsetToHandPosition()
        {
            var tip = HandHeldTip.Compute(Hand(new Vector3(1f, 2f, 3f), Quaternion.identity), new Vector3(0f, 0f, 0.15f));

            AssertVector(new Vector3(1f, 2f, 3.15f), tip.position);
        }

        [Test]
        public void Compute_RotatedHand_RotatesOffsetWithHand()
        {
            // 手が右（+X）を向くと、手の前（+Z）のオフセットは +X に出る
            var hand = Hand(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));

            var tip = HandHeldTip.Compute(hand, new Vector3(0f, 0f, 0.2f));

            AssertVector(new Vector3(0.2f, 0f, 0f), tip.position);
            Assert.AreEqual(0f, Quaternion.Angle(hand.rotation, tip.rotation), Tolerance);
        }

        [Test]
        public void SolveOffset_IsInverseOfCompute()
        {
            var hand = Hand(new Vector3(0.3f, 1.2f, -0.5f), Quaternion.Euler(20f, 130f, -40f));
            var offset = new Vector3(0.02f, -0.03f, 0.18f);

            var tip = HandHeldTip.Compute(hand, offset);

            AssertVector(offset, HandHeldTip.SolveOffset(hand, tip.position));
        }

        [Test]
        public void Update_FirstTime_SnapsToTargetEvenWithSmoothing()
        {
            var tip = new HandHeldTip(new Vector3(0f, 0f, 0.1f), smoothingSeconds: 1f);

            var pose = tip.Update(Hand(new Vector3(5f, 0f, 0f), Quaternion.identity), 0.01f);

            AssertVector(new Vector3(5f, 0f, 0.1f), pose.position);
        }

        [Test]
        public void Update_WithSmoothing_MovesPartwayThenConverges()
        {
            var tip = new HandHeldTip(Vector3.zero, smoothingSeconds: 0.1f);
            tip.Update(Hand(Vector3.zero, Quaternion.identity), 0.01f);

            var target = Hand(new Vector3(1f, 0f, 0f), Quaternion.identity);
            var first = tip.Update(target, 0.01f);
            Assert.Greater(first.position.x, 0f);
            Assert.Less(first.position.x, 1f);

            Pose last = first;
            for (var i = 0; i < 200; i++) last = tip.Update(target, 0.01f);
            Assert.AreEqual(1f, last.position.x, 1e-2f);
        }

        [Test]
        public void Update_ZeroSmoothing_FollowsHandExactly()
        {
            var tip = new HandHeldTip(Vector3.zero, smoothingSeconds: 0f);
            tip.Update(Hand(Vector3.zero, Quaternion.identity), 0.01f);

            var pose = tip.Update(Hand(new Vector3(2f, 0f, 0f), Quaternion.identity), 0.01f);

            AssertVector(new Vector3(2f, 0f, 0f), pose.position);
        }

        [Test]
        public void Calibrate_PutsTipAtGivenPointAndSnapsOnNextUpdate()
        {
            var tip = new HandHeldTip(new Vector3(0f, 0f, 0.15f), smoothingSeconds: 1f);
            var hand = Hand(new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, 90f, 0f));
            tip.Update(hand, 0.01f);
            var mouth = new Vector3(0.3f, 1.1f, 0f);

            tip.Calibrate(hand, mouth);
            var pose = tip.Update(hand, 0.01f);

            AssertVector(mouth, pose.position);
        }

        [Test]
        public void Calibrate_OffsetFollowsHandAfterwards()
        {
            var tip = new HandHeldTip(Vector3.zero, smoothingSeconds: 0f);
            var hand = Hand(Vector3.zero, Quaternion.identity);
            tip.Calibrate(hand, new Vector3(0f, 0f, 0.2f));

            // 手が 90 度回って移動しても、先は手の前 20 cm のまま
            var moved = Hand(new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));
            var pose = tip.Update(moved, 0.01f);

            AssertVector(new Vector3(1.2f, 0f, 0f), pose.position);
        }
    }
}
