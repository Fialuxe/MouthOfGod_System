using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.CameraWork.Tests
{
    public class CameraPathMathTests
    {
        static void AssertNear(Vector3 expected, Vector3 actual) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-4f), $"{expected} vs {actual}");

        [Test]
        public void Ease_EndsAreFixedForEveryKind()
        {
            foreach (CameraEase ease in new[] { CameraEase.Linear, CameraEase.EaseIn, CameraEase.EaseOut, CameraEase.EaseInOut })
            {
                Assert.AreEqual(0f, CameraPathMath.Ease(ease, 0f, 3f), 1e-5f, ease.ToString());
                Assert.AreEqual(1f, CameraPathMath.Ease(ease, 1f, 3f), 1e-5f, ease.ToString());
            }
        }

        [Test]
        public void Ease_InIsSlowAtStart_OutIsFastAtStart()
        {
            Assert.Less(CameraPathMath.Ease(CameraEase.EaseIn, 0.25f, 2f), 0.25f);
            Assert.Greater(CameraPathMath.Ease(CameraEase.EaseOut, 0.25f, 2f), 0.25f);
            Assert.AreEqual(0.5f, CameraPathMath.Ease(CameraEase.EaseInOut, 0.5f, 2f), 1e-5f);
        }

        [Test]
        public void Ease_StrengthOneIsLinear()
        {
            Assert.AreEqual(0.3f, CameraPathMath.Ease(CameraEase.EaseIn, 0.3f, 1f), 1e-5f);
        }

        [Test]
        public void Ease_ClampsOutOfRange()
        {
            Assert.AreEqual(1f, CameraPathMath.Ease(CameraEase.Linear, 2f, 1f), 1e-5f);
            Assert.AreEqual(0f, CameraPathMath.Ease(CameraEase.Linear, -1f, 1f), 1e-5f);
        }

        [Test]
        public void Line_Midpoint() =>
            AssertNear(new Vector3(1, 1, 1), CameraPathMath.Line(Vector3.zero, new Vector3(2, 2, 2), 0.5f));

        [Test]
        public void Bezier_PassesThroughEndsAndLeansToControl()
        {
            var pts = new[] { Vector3.zero, new Vector3(1, 2, 0), new Vector3(2, 0, 0) };
            AssertNear(Vector3.zero, CameraPathMath.Bezier(pts, 0f));
            AssertNear(new Vector3(2, 0, 0), CameraPathMath.Bezier(pts, 1f));
            AssertNear(new Vector3(1, 1, 0), CameraPathMath.Bezier(pts, 0.5f));
        }

        [Test]
        public void Bezier_TwoPointsIsALine() =>
            AssertNear(new Vector3(0.5f, 0, 0), CameraPathMath.Bezier(new[] { Vector3.zero, Vector3.right }, 0.5f));

        [Test]
        public void Orbit_QuarterTurnAroundY()
        {
            Vector3 end = CameraPathMath.Orbit(new Vector3(1, 0, 0), Vector3.zero, Vector3.up, 90f, 1f, 0f, 1f);
            AssertNear(new Vector3(0, 0, -1), end);
        }

        [Test]
        public void Orbit_FullTurnReturns_AndHeightChangeRaises()
        {
            var start = new Vector3(2, 0, 0);
            AssertNear(start, CameraPathMath.Orbit(start, Vector3.zero, Vector3.up, 360f, 1f, 0f, 1f));
            AssertNear(new Vector3(2, 3, 0), CameraPathMath.Orbit(start, Vector3.zero, Vector3.up, 360f, 1f, 3f, 1f));
        }

        [Test]
        public void Orbit_RadiusScaleShrinksTowardCenter()
        {
            Vector3 end = CameraPathMath.Orbit(new Vector3(2, 0, 0), Vector3.zero, Vector3.up, 0f, 0.5f, 0f, 1f);
            AssertNear(new Vector3(1, 0, 0), end);
        }

        [Test]
        public void Length_OfStraightLine() =>
            Assert.AreEqual(5f, CameraPathMath.Length(t => CameraPathMath.Line(Vector3.zero, new Vector3(3, 4, 0), t)), 1e-3f);

        [Test]
        public void Duration_FromSpeedOrSeconds()
        {
            Assert.AreEqual(2.5f, CameraPathMath.Duration(true, 9f, 2f, 5f), 1e-5f);
            Assert.AreEqual(9f, CameraPathMath.Duration(false, 9f, 2f, 5f), 1e-5f);
            Assert.Greater(CameraPathMath.Duration(false, 0f, 1f, 0f), 0f);
        }
    }
}
