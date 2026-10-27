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
        public void AlignNormalToVertical_TiltedTag_MakesNormalVerticalByMinimalRotation()
        {
            // 法線（+z）が真上から 20 度傾いたタグ。
            var tilted = Quaternion.Euler(-70f, 30f, 0f);

            var result = AprilTagGeometry.AlignNormalToVertical(tilted, Vector3.forward);

            var normal = result * Vector3.forward;
            Assert.AreEqual(0f, normal.x, Tolerance);
            Assert.AreEqual(1f, normal.y, Tolerance);
            Assert.AreEqual(0f, normal.z, Tolerance);
            Assert.AreEqual(20f, Quaternion.Angle(tilted, result), 0.01f);
        }

        [Test]
        public void AlignNormalToVertical_AlreadyVertical_KeepsRotation()
        {
            var upright = Quaternion.Euler(-90f, 30f, 0f);

            var result = AprilTagGeometry.AlignNormalToVertical(upright, Vector3.forward);

            Assert.AreEqual(0f, Quaternion.Angle(upright, result), 0.01f);
        }

        [Test]
        public void AlignNormalToVertical_NormalPointingDown_StaysPointingDown()
        {
            var tiltedDown = Quaternion.Euler(70f, 0f, 0f);

            var result = AprilTagGeometry.AlignNormalToVertical(tiltedDown, Vector3.forward);

            var normal = result * Vector3.forward;
            Assert.AreEqual(-1f, normal.y, Tolerance);
        }

        [Test]
        public void IsValidRotation_DefaultQuaternion_IsFalse()
        {
            Assert.IsFalse(AprilTagGeometry.IsValidRotation(default));
            Assert.IsTrue(AprilTagGeometry.IsValidRotation(Quaternion.identity));
        }

        const float OffsetTolerance = 1e-4f;

        [Test]
        public void ApplyOffsets_NoWorldRotation_MatchesTagFrame()
        {
            var tag = new Pose(new Vector3(1f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f));

            var result = AprilTagGeometry.ApplyOffsets(tag, new Vector3(0f, 0f, 1f), new Vector3(0f, 30f, 0f), Vector3.zero);

            // タグの前（+Z）は、ワールドの +X を向く
            Assert.AreEqual(2f, result.position.x, OffsetTolerance);
            Assert.AreEqual(0f, result.position.z, OffsetTolerance);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Euler(0f, 120f, 0f), result.rotation), OffsetTolerance);
        }

        [Test]
        public void ApplyOffsets_WorldYaw_RotatesWholeThingAroundTagPosition()
        {
            var tag = new Pose(new Vector3(1f, 0f, 0f), Quaternion.identity);

            var result = AprilTagGeometry.ApplyOffsets(tag, new Vector3(0f, 0f, 1f), Vector3.zero, new Vector3(0f, 90f, 0f));

            // タグの前 1 m が、ワールドの Y 軸まわり 90 度で +X に回る
            Assert.AreEqual(2f, result.position.x, OffsetTolerance);
            Assert.AreEqual(0f, result.position.z, OffsetTolerance);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), result.rotation), OffsetTolerance);
        }

        [Test]
        public void ApplyOffsets_WorldRotationIsAboutWorldAxisEvenWhenTagIsTilted()
        {
            // タグが X まわりに 90 度倒れていても、ワールドの Y まわりに 90 度回る
            var tag = new Pose(Vector3.zero, Quaternion.Euler(90f, 0f, 0f));

            var result = AprilTagGeometry.ApplyOffsets(tag, Vector3.zero, Vector3.zero, new Vector3(0f, 90f, 0f));

            var expected = Quaternion.Euler(0f, 90f, 0f) * tag.rotation;
            Assert.AreEqual(0f, Quaternion.Angle(expected, result.rotation), OffsetTolerance);
        }

        static Vector3 InRig(Pose rig, Vector3 worldPoint) =>
            Quaternion.Inverse(rig.rotation) * (worldPoint - rig.position);

        [Test]
        public void AlignRigToTarget_AlreadyAligned_LeavesRigAlone()
        {
            var tag = new Pose(new Vector3(1f, 0.5f, 2f), Quaternion.Euler(0f, 40f, 0f));
            var rig = new Pose(new Vector3(0f, 0f, 0f), Quaternion.Euler(0f, 10f, 0f));

            var result = AprilTagGeometry.AlignRigToTarget(rig, tag, tag, alignYaw: true);

            Assert.AreEqual(0f, Vector3.Distance(rig.position, result.position), OffsetTolerance);
            Assert.AreEqual(0f, Quaternion.Angle(rig.rotation, result.rotation), OffsetTolerance);
        }

        [Test]
        public void AlignRigToTarget_PositionOnly_ShiftsRigByTheGap()
        {
            var observed = new Pose(new Vector3(1f, 0f, 0f), Quaternion.identity);
            var desired = new Pose(Vector3.zero, Quaternion.identity);
            var rig = new Pose(new Vector3(2f, 0f, 0f), Quaternion.identity);

            var result = AprilTagGeometry.AlignRigToTarget(rig, observed, desired, alignYaw: false);

            Assert.AreEqual(0f, Vector3.Distance(new Vector3(1f, 0f, 0f), result.position), OffsetTolerance);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, result.rotation), OffsetTolerance);
        }

        [Test]
        public void AlignRigToTarget_Yaw_TurnsRigAroundTheTag()
        {
            var observed = new Pose(new Vector3(1f, 0f, 0f), Quaternion.identity);
            var desired = new Pose(Vector3.zero, Quaternion.Euler(0f, 90f, 0f));
            var rig = new Pose(new Vector3(2f, 0f, 0f), Quaternion.identity);

            var result = AprilTagGeometry.AlignRigToTarget(rig, observed, desired, alignYaw: true);

            Assert.AreEqual(0f, Vector3.Distance(new Vector3(0f, 0f, -1f), result.position), OffsetTolerance);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.Euler(0f, 90f, 0f), result.rotation), OffsetTolerance);
        }

        [Test]
        public void AlignRigToTarget_KeepsTagPositionSeenFromRig()
        {
            var observed = new Pose(new Vector3(0.5f, 0.8f, 1.2f), Quaternion.Euler(0f, 30f, 0f));
            var desired = new Pose(new Vector3(-2f, 0.75f, 0.3f), Quaternion.Euler(0f, 140f, 0f));
            var rig = new Pose(new Vector3(1f, 0f, -1f), Quaternion.Euler(0f, 10f, 0f));

            var result = AprilTagGeometry.AlignRigToTarget(rig, observed, desired, alignYaw: true);

            // リグから見たタグの位置が、動かす前後で変わらない（動かす前は観測、動かした後は基準の位置にタグが見える）
            Assert.AreEqual(0f, Vector3.Distance(InRig(rig, observed.position), InRig(result, desired.position)), OffsetTolerance);
            // 傾きは変えない
            Assert.AreEqual(0f, (result.rotation * Vector3.up - Vector3.up).magnitude, OffsetTolerance);
        }

        [Test]
        public void YawDelta_ForwardVertical_ComparesRightAxis()
        {
            // 前（Z）が鉛直を向く姿勢（机に寝かせたタグ）でも、右（X）で向きを比べられる
            var from = Quaternion.Euler(-90f, 0f, 0f);
            var to = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-90f, 0f, 0f);

            var delta = AprilTagGeometry.YawDelta(from, to);

            Assert.AreEqual(90f, Mathf.Abs(delta), 1e-2f);
        }
    }
}
