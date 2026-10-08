using NUnit.Framework;
using UnityEngine;

namespace MouthOfGod.Tracking.Tests
{
    public class AprilTagGeometryTests
    {
        const float Tolerance = 1e-4f;

        [Test]
        public void ToImageIntrinsics_SameResolution_KeepsSensorValues()
        {
            var result = AprilTagGeometry.ToImageIntrinsics(
                new Vector2(900f, 910f), new Vector2(640f, 480f),
                new Vector2Int(1280, 960), new Vector2Int(1280, 960));

            Assert.AreEqual(900f, result.FocalLength.x, Tolerance);
            Assert.AreEqual(910f, result.FocalLength.y, Tolerance);
            Assert.AreEqual(640f, result.PrincipalPoint.x, Tolerance);
            Assert.AreEqual(480f, result.PrincipalPoint.y, Tolerance);
        }

        [Test]
        public void ToImageIntrinsics_HalfResolutionSameAspect_HalvesEverything()
        {
            var result = AprilTagGeometry.ToImageIntrinsics(
                new Vector2(900f, 900f), new Vector2(640f, 480f),
                new Vector2Int(1280, 960), new Vector2Int(640, 480));

            Assert.AreEqual(450f, result.FocalLength.x, Tolerance);
            Assert.AreEqual(450f, result.FocalLength.y, Tolerance);
            Assert.AreEqual(320f, result.PrincipalPoint.x, Tolerance);
            Assert.AreEqual(240f, result.PrincipalPoint.y, Tolerance);
        }

        [Test]
        public void ToImageIntrinsics_WiderImage_CropsSensorVerticallyAndKeepsPixelScale()
        {
            // 1280x960 のセンサーを、16:9 の 1280x720 に切り出す。縦だけ 240 ピクセル分（上下 120 ずつ）削れ、拡大縮小はない。
            var result = AprilTagGeometry.ToImageIntrinsics(
                new Vector2(900f, 900f), new Vector2(640f, 480f),
                new Vector2Int(1280, 960), new Vector2Int(1280, 720));

            Assert.AreEqual(900f, result.FocalLength.x, Tolerance);
            Assert.AreEqual(900f, result.FocalLength.y, Tolerance);
            Assert.AreEqual(640f, result.PrincipalPoint.x, Tolerance);
            Assert.AreEqual(360f, result.PrincipalPoint.y, Tolerance);
        }

        [Test]
        public void VerticalFovRadians_FocalLengthEqualToHalfHeight_Is90Degrees()
        {
            var fov = AprilTagGeometry.VerticalFovRadians(480f, 960);

            Assert.AreEqual(Mathf.PI / 2f, fov, Tolerance);
        }

        [Test]
        public void CameraLocalToWorld_AppliesCameraPositionAndRotation()
        {
            var camera = new Pose(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 90f, 0f));

            var world = AprilTagGeometry.CameraLocalToWorld(camera, new Vector3(0f, 0f, 1f), Quaternion.identity);

            // Y 軸まわりに 90 度回したカメラの正面（+z）は、ワールドの +x を向く。
            Assert.AreEqual(2f, world.position.x, Tolerance);
            Assert.AreEqual(2f, world.position.y, Tolerance);
            Assert.AreEqual(3f, world.position.z, Tolerance);
            Assert.AreEqual(0f, Quaternion.Angle(camera.rotation, world.rotation), Tolerance);
        }

        [Test]
        public void IsValidRotation_DefaultQuaternion_IsFalse()
        {
            Assert.IsFalse(AprilTagGeometry.IsValidRotation(default));
            Assert.IsTrue(AprilTagGeometry.IsValidRotation(Quaternion.identity));
        }
    }
}
