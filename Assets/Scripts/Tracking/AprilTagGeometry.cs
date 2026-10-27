using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// カメラの内部パラメータの換算と、座標変換。Unity のオブジェクトに依存しない純粋な計算。
    /// </summary>
    public static class AprilTagGeometry
    {
        /// <summary>現在の画像（ピクセル）に換算した内部パラメータ。原点は左下、y は上向き（Unity のテクスチャと同じ）。</summary>
        public readonly struct ImageIntrinsics
        {
            public ImageIntrinsics(Vector2 focalLength, Vector2 principalPoint)
            {
                FocalLength = focalLength;
                PrincipalPoint = principalPoint;
            }

            public Vector2 FocalLength { get; }
            public Vector2 PrincipalPoint { get; }
        }

        /// <summary>
        /// センサー基準の内部パラメータを、現在の画像解像度の基準に換算する。
        /// 画像は、センサーを中央でクロップして縮小したもの（Meta の <c>PassthroughCameraAccess</c> と同じ扱い）。
        /// </summary>
        public static ImageIntrinsics ToImageIntrinsics(
            Vector2 sensorFocalLength, Vector2 sensorPrincipalPoint, Vector2Int sensorResolution, Vector2Int imageResolution)
        {
            var sensor = (Vector2)sensorResolution;
            var image = (Vector2)imageResolution;

            var scale = image / sensor;
            scale /= Mathf.Max(scale.x, scale.y);
            var cropSize = sensor * scale;
            var cropOrigin = (sensor - cropSize) * 0.5f;

            var pixelsPerSensorPixel = new Vector2(image.x / cropSize.x, image.y / cropSize.y);
            return new ImageIntrinsics(
                Vector2.Scale(sensorFocalLength, pixelsPerSensorPixel),
                Vector2.Scale(sensorPrincipalPoint - cropOrigin, pixelsPerSensorPixel));
        }

        /// <summary>AprilTag ライブラリに渡す、垂直方向の画角（ラジアン）。ライブラリは画素が正方形で主点が画像中央にあると仮定する。</summary>
        public static float VerticalFovRadians(float focalLengthY, int imageHeight)
        {
            return 2f * Mathf.Atan(imageHeight / (2f * focalLengthY));
        }

        /// <summary>カメラのローカル座標で得たタグの姿勢を、ワールド座標に直す。</summary>
        public static Pose CameraLocalToWorld(Pose cameraWorldPose, Vector3 localPosition, Quaternion localRotation)
        {
            return new Pose(
                cameraWorldPose.position + cameraWorldPose.rotation * localPosition,
                cameraWorldPose.rotation * localRotation);
        }

        /// <summary>
        /// タグの法線（タグ座標の <paramref name="localNormalAxis"/> 軸）が鉛直になるように、傾きだけを取り除く。
        /// 法線まわりの回転（水平なタグなら向き）は変えない。法線が上向きなら上、下向きなら下に揃える。
        /// 水平な面に置いたタグ向け。壁など、法線が水平なタグには使わない。
        /// </summary>
        public static Quaternion AlignNormalToVertical(Quaternion rotation, Vector3 localNormalAxis)
        {
            var normal = rotation * localNormalAxis.normalized;
            var target = normal.y >= 0f ? Vector3.up : Vector3.down;
            return Quaternion.FromToRotation(normal, target) * rotation;
        }

        /// <summary>
        /// タグの姿勢に、オフセットを足した、対象の姿勢を求める。
        /// 基準の向きは「ワールド軸の回転 × タグの回転」。ワールドの回転を 0 にすれば、タグの向きそのまま。
        /// 位置のオフセットとローカルのオイラー角は、この基準の座標系で足す。そのため、ワールドの回転は、タグの位置を軸に全体を回す。
        /// </summary>
        public static Pose ApplyOffsets(Pose tag, Vector3 localPosition, Vector3 localEuler, Vector3 worldEuler)
        {
            var frame = Quaternion.Euler(worldEuler) * tag.rotation;
            return new Pose(tag.position + frame * localPosition, frame * Quaternion.Euler(localEuler));
        }

        /// <summary>
        /// 観測したタグの姿勢を、シーンの基準の姿勢に重ねるために、リグ（ユーザー側）を動かした後の姿勢を求める。
        /// 動かすのは、位置と、鉛直軸まわりの向き（<paramref name="alignYaw"/> のとき）だけ。傾きは変えない（VR の水平線が傾かないように）。
        /// 「タグから見たリグの位置」を、動かす前後で変えない。つまり、シーン空間でのユーザーの姿勢 = 基準の姿勢 × 観測したタグの姿勢の逆 × ワールドでのユーザーの姿勢。
        /// </summary>
        /// <param name="rig">いまのリグの姿勢（ワールド）。</param>
        /// <param name="observed">観測したタグの姿勢（ワールド）。オフセットを足したあと。</param>
        /// <param name="desired">タグがあるべき、シーンの基準の姿勢（ワールド）。</param>
        public static Pose AlignRigToTarget(Pose rig, Pose observed, Pose desired, bool alignYaw)
        {
            var yaw = alignYaw
                ? Quaternion.AngleAxis(YawDelta(observed.rotation, desired.rotation), Vector3.up)
                : Quaternion.identity;
            return new Pose(
                desired.position + yaw * (rig.position - observed.position),
                yaw * rig.rotation);
        }

        /// <summary>from から to へ、鉛直軸まわりに回す角度（度）。前（Z）が鉛直に近いときは、右（X）で比べる。</summary>
        public static float YawDelta(Quaternion from, Quaternion to)
        {
            var a = Vector3.ProjectOnPlane(from * Vector3.forward, Vector3.up);
            var b = Vector3.ProjectOnPlane(to * Vector3.forward, Vector3.up);
            if (a.sqrMagnitude < 1e-4f || b.sqrMagnitude < 1e-4f)
            {
                a = Vector3.ProjectOnPlane(from * Vector3.right, Vector3.up);
                b = Vector3.ProjectOnPlane(to * Vector3.right, Vector3.up);
            }
            return Vector3.SignedAngle(a, b, Vector3.up);
        }

        /// <summary>回転が (0,0,0,0)（未取得）でないか。</summary>
        public static bool IsValidRotation(Quaternion rotation)
        {
            return rotation.x != 0f || rotation.y != 0f || rotation.z != 0f || rotation.w != 0f;
        }
    }
}
