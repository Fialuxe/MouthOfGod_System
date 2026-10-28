using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.CameraWork.Tests
{
    public class ShotPathTests
    {
        GameObject _end;

        [TearDown]
        public void TearDown()
        {
            if (_end != null) Object.DestroyImmediate(_end);
        }

        [Test]
        public void Line_WithoutEnd_GoesAlongDirectionFromForward()
        {
            var shot = new CameraShot { kind = ShotKind.Line, distance = 3f, useSpeed = true, speed = 1.5f };
            var path = new ShotPath(shot, new Vector3(1, 0, 0), Quaternion.identity, Vector3.forward);
            Assert.That(Vector3.Distance(new Vector3(1, 0, 3), path.EndPosition), Is.LessThan(1e-4f));
            Assert.AreEqual(3f, path.Length, 1e-3f);
            Assert.AreEqual(2f, path.Duration, 1e-3f);
        }

        [Test]
        public void Line_WithEnd_GoesToEnd()
        {
            _end = new GameObject("end");
            _end.transform.position = new Vector3(0, 4, 3);
            var shot = new CameraShot { kind = ShotKind.Line, end = _end.transform, seconds = 5f };
            var path = new ShotPath(shot, Vector3.zero, Quaternion.identity, Vector3.forward);
            Assert.That(Vector3.Distance(_end.transform.position, path.EndPosition), Is.LessThan(1e-4f));
            Assert.AreEqual(5f, path.Duration, 1e-5f);
        }

        [Test]
        public void Hold_StaysPut()
        {
            var shot = new CameraShot { kind = ShotKind.Hold, seconds = 1.5f, useSpeed = true };
            var path = new ShotPath(shot, new Vector3(1, 2, 3), Quaternion.identity, Vector3.forward);
            Assert.That(Vector3.Distance(new Vector3(1, 2, 3), path.EndPosition), Is.LessThan(1e-5f));
            Assert.AreEqual(1.5f, path.Duration, 1e-5f);
        }

        [Test]
        public void Orbit_FullTurnLengthIsCircumference()
        {
            var shot = new CameraShot { kind = ShotKind.Orbit, angle = 360f, axis = Vector3.up, radiusScale = 1f };
            var center = new GameObject("c");
            _end = center;
            shot.center = center.transform;
            var path = new ShotPath(shot, new Vector3(2, 0, 0), Quaternion.identity, Vector3.forward);
            Assert.AreEqual(2f * Mathf.PI * 2f, path.Length, 0.05f);
        }

        [Test]
        public void Tangent_OfLine_PointsAlongIt()
        {
            var shot = new CameraShot { kind = ShotKind.Line, distance = 2f };
            var path = new ShotPath(shot, Vector3.zero, Quaternion.identity, Vector3.right);
            Assert.That(Vector3.Distance(Vector3.right, path.Tangent(0.5f, Vector3.up)), Is.LessThan(1e-4f));
        }
    }
}
