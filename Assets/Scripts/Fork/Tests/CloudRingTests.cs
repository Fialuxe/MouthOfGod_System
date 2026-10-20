using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Fork.Tests
{
    public class CloudRingTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void Offset_OneCloud_IsStraightAhead()
        {
            Vector3 offset = CloudRing.Offset(0, 1, 0.5f, -0.3f, 120f);
            Assert.AreEqual(0f, offset.x, Tolerance);
            Assert.AreEqual(-0.3f, offset.y, Tolerance);
            Assert.AreEqual(0.5f, offset.z, Tolerance);
        }

        [Test]
        public void Offset_SpreadsEvenly_AndTheMiddleIsAhead()
        {
            Vector3 left = CloudRing.Offset(0, 3, 1f, 0f, 90f);
            Vector3 middle = CloudRing.Offset(1, 3, 1f, 0f, 90f);
            Vector3 right = CloudRing.Offset(2, 3, 1f, 0f, 90f);

            Assert.AreEqual(-Mathf.Sin(45f * Mathf.Deg2Rad), left.x, Tolerance);
            Assert.AreEqual(0f, middle.x, Tolerance);
            Assert.AreEqual(1f, middle.z, Tolerance);
            Assert.AreEqual(-left.x, right.x, Tolerance);
        }

        [Test]
        public void Offset_FullCircle_DoesNotOverlapTheFirstAndLast()
        {
            Vector3 first = CloudRing.Offset(0, 4, 1f, 0f, 360f);
            Vector3 last = CloudRing.Offset(3, 4, 1f, 0f, 360f);
            Assert.Greater(Vector3.Distance(first, last), 1f);
        }

        [Test]
        public void Offset_KeepsTheRadius()
        {
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = CloudRing.Offset(i, 5, 0.45f, 0f, 120f);
                Assert.AreEqual(0.45f, new Vector2(offset.x, offset.z).magnitude, Tolerance);
            }
        }
    }
}
