using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Fork.Tests
{
    public class CloudCuesTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void Frontmost_PicksTheCloudStraightAhead()
        {
            var positions = new[] { new Vector3(-0.4f, -0.3f, 0.2f), new Vector3(0f, -0.3f, 0.45f), new Vector3(0.4f, -0.3f, 0.2f) };
            Assert.AreEqual(1, CloudCues.Frontmost(Vector3.zero, Vector3.forward, positions));
        }

        [Test]
        public void Frontmost_FollowsTheHeadDirection_AndIgnoresPitch()
        {
            var positions = new[] { new Vector3(-1f, 0f, 0f), new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 0f) };
            Assert.AreEqual(2, CloudCues.Frontmost(Vector3.zero, new Vector3(1f, -0.8f, 0f), positions));
        }

        [Test]
        public void Frontmost_Empty_IsMinusOne()
        {
            Assert.AreEqual(-1, CloudCues.Frontmost(Vector3.zero, Vector3.forward, new Vector3[0]));
            Assert.AreEqual(-1, CloudCues.Frontmost(Vector3.zero, Vector3.forward, null));
        }

        [Test]
        public void Frontmost_LookingStraightDown_PicksTheFirst()
        {
            var positions = new[] { new Vector3(1f, 0f, 0f), new Vector3(0f, 0f, 1f) };
            Assert.AreEqual(0, CloudCues.Frontmost(Vector3.zero, Vector3.down, positions));
        }

        [Test]
        public void ApproachOffset_MovesTowardTheHeadByMeters()
        {
            Vector3 offset = CloudCues.ApproachOffset(new Vector3(0f, 0f, 1f), Vector3.zero, 0.2f, 0.25f, 1f);
            Assert.AreEqual(-0.2f, offset.z, Tolerance);
            Assert.AreEqual(0f, offset.x, Tolerance);
        }

        [Test]
        public void ApproachOffset_StopsAtTheMinimumDistance()
        {
            Vector3 offset = CloudCues.ApproachOffset(new Vector3(0f, 0f, 0.3f), Vector3.zero, 0.2f, 0.25f, 1f);
            Assert.AreEqual(-0.05f, offset.z, Tolerance);
        }

        [Test]
        public void ApproachOffset_AlreadyTooClose_DoesNotMove()
        {
            Assert.AreEqual(Vector3.zero, CloudCues.ApproachOffset(new Vector3(0f, 0f, 0.1f), Vector3.zero, 0.2f, 0.25f, 1f));
            Assert.AreEqual(Vector3.zero, CloudCues.ApproachOffset(Vector3.zero, Vector3.zero, 0.2f, 0.25f, 1f));
        }

        [Test]
        public void ApproachOffset_ZeroAmount_IsZero_AndAmountIsClamped()
        {
            Assert.AreEqual(0f, CloudCues.ApproachOffset(new Vector3(0f, 0f, 1f), Vector3.zero, 0.2f, 0.25f, 0f).magnitude, Tolerance);
            Assert.AreEqual(0.2f, CloudCues.ApproachOffset(new Vector3(0f, 0f, 1f), Vector3.zero, 0.2f, 0.25f, 5f).magnitude, Tolerance);
        }

        [Test]
        public void Pulse_GoesFromZeroToOneAndBack()
        {
            Assert.AreEqual(0f, CloudCues.Pulse(0f, 1f), Tolerance);
            Assert.AreEqual(1f, CloudCues.Pulse(0.5f, 1f), Tolerance);
            Assert.AreEqual(0f, CloudCues.Pulse(1f, 1f), Tolerance);
            Assert.AreEqual(0f, CloudCues.Pulse(3f, 0f), Tolerance);
        }
    }
}
