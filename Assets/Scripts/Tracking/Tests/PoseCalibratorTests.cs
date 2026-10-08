using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Tracking.Tests
{
    public class PoseCalibratorTests
    {
        const float Tolerance = 1e-4f;

        static Pose At(float x) => new Pose(new Vector3(x, 0f, 0f), Quaternion.identity);

        [Test]
        public void TryGetResult_NoSamples_ReturnsFalse()
        {
            var calibrator = new PoseCalibrator(3, 0.02f, 10f);

            Assert.IsFalse(calibrator.TryGetResult(out _));
        }

        [Test]
        public void IsComplete_AfterRequiredSamples_IsTrueAndExtraSamplesAreIgnored()
        {
            var calibrator = new PoseCalibrator(requiredSamples: 2, 0.02f, 10f);

            calibrator.Add(At(0f));
            Assert.IsFalse(calibrator.IsComplete);
            calibrator.Add(At(0.01f));
            calibrator.Add(At(5f)); // 集め終わったあとの観測は無視される。

            Assert.IsTrue(calibrator.IsComplete);
            Assert.AreEqual(2, calibrator.Count);
            Assert.IsTrue(calibrator.TryGetResult(out var result));
            Assert.AreEqual(0.005f, result.position.x, Tolerance);
        }

        [Test]
        public void TryGetResult_AveragesPositions()
        {
            var calibrator = new PoseCalibrator(3, 0.1f, 10f);
            calibrator.Add(At(0f));
            calibrator.Add(At(0.01f));
            calibrator.Add(At(0.02f));

            Assert.IsTrue(calibrator.TryGetResult(out var result));
            Assert.AreEqual(0.01f, result.position.x, Tolerance);
        }

        [Test]
        public void TryGetResult_RejectsPositionOutlier()
        {
            var calibrator = new PoseCalibrator(5, maxPositionOutlierMeters: 0.02f, 10f);
            for (var i = 0; i < 4; i++) calibrator.Add(At(0f));
            calibrator.Add(At(1f));

            Assert.IsTrue(calibrator.TryGetResult(out var result));
            Assert.AreEqual(0f, result.position.x, Tolerance);
        }

        [Test]
        public void TryGetResult_RejectsRotationOutlier()
        {
            var calibrator = new PoseCalibrator(5, 0.1f, maxAngleOutlierDegrees: 10f);
            for (var i = 0; i < 4; i++) calibrator.Add(new Pose(Vector3.zero, Quaternion.identity));
            calibrator.Add(new Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f)));

            Assert.IsTrue(calibrator.TryGetResult(out var result));
            Assert.Less(Quaternion.Angle(result.rotation, Quaternion.identity), 1f);
        }

        [Test]
        public void TryGetResult_QuaternionsWithOppositeSigns_AreTreatedAsTheSameRotation()
        {
            var q = Quaternion.Euler(10f, 20f, 30f);
            var negated = new Quaternion(-q.x, -q.y, -q.z, -q.w);
            var calibrator = new PoseCalibrator(2, 0.1f, 10f);
            calibrator.Add(new Pose(Vector3.zero, q));
            calibrator.Add(new Pose(Vector3.zero, negated));

            Assert.IsTrue(calibrator.TryGetResult(out var result));
            Assert.Less(Quaternion.Angle(result.rotation, q), 0.01f);
        }

        [Test]
        public void Reset_ClearsSamples()
        {
            var calibrator = new PoseCalibrator(2, 0.1f, 10f);
            calibrator.Add(At(0f));
            calibrator.Add(At(0f));

            calibrator.Reset();

            Assert.AreEqual(0, calibrator.Count);
            Assert.IsFalse(calibrator.IsComplete);
        }
    }
}
