using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// カメラの内部パラメータの換算と、AprilTag まわりの座標変換を行います。
    /// </summary>
    /// <remarks>
    /// Unity のオブジェクトに依存しない、純粋な計算だけを置きます。
    /// </remarks>
    public static class AprilTagGeometry
    {
        /// <summary>
        /// 現在の画像のピクセルに換算した、カメラの内部パラメータです。
        /// </summary>
        /// <remarks>
        /// 原点は左下で、y は上向きです。
        /// Unity のテクスチャと同じ向きです。
        /// </remarks>
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
        /// センサー基準の内部パラメータを、現在の画像の解像度の基準に換算します。
        /// </summary>
        /// <remarks>
        /// 画像は、センサーを中央でクロップして縮小したものとして扱います。
        /// Meta の <c>PassthroughCameraAccess</c> と同じ扱いです。
        /// </remarks>
        /// <param name="sensorFocalLength">センサーのピクセルでの焦点距離です。</param>
        /// <param name="sensorPrincipalPoint">センサーのピクセルでの主点です。</param>
        /// <param name="sensorResolution">センサーの解像度です。</param>
        /// <param name="imageResolution">現在の画像の解像度です。</param>
        /// <returns>画像のピクセルに換算した内部パラメータを返します。</returns>
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

        /// <summary>
        /// AprilTag ライブラリに渡す、垂直方向の画角を求めます。
        /// </summary>
        /// <remarks>
        /// ライブラリは、画素が正方形だと仮定します。
        /// 主点も画像の中央にあると仮定します。
        /// </remarks>
        /// <param name="focalLengthY">画像のピクセルでの、y 方向の焦点距離です。</param>
        /// <param name="imageHeight">画像の高さ（ピクセル）です。</param>
        /// <returns>垂直方向の画角（ラジアン）を返します。</returns>
        public static float VerticalFovRadians(float focalLengthY, int imageHeight)
        {
            return 2f * Mathf.Atan(imageHeight / (2f * focalLengthY));
        }

        /// <summary>
        /// カメラのローカル座標で得たタグの姿勢を、ワールド座標に直します。
        /// </summary>
        /// <param name="cameraWorldPose">検出したフレームでの、カメラのワールド姿勢です。</param>
        /// <param name="localPosition">カメラから見たタグの位置です。</param>
        /// <param name="localRotation">カメラから見たタグの向きです。</param>
        /// <returns>タグのワールド姿勢を返します。</returns>
        public static Pose CameraLocalToWorld(Pose cameraWorldPose, Vector3 localPosition, Quaternion localRotation)
        {
            return new Pose(
                cameraWorldPose.position + cameraWorldPose.rotation * localPosition,
                cameraWorldPose.rotation * localRotation);
        }

        /// <summary>
        /// タグの法線が鉛直になるように、傾きだけを取り除きます。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 法線まわりの回転は変えません。
        /// 水平なタグなら、向きはそのまま残ります。
        /// 法線が上向きなら真上に、下向きなら真下に揃えます。
        /// </para>
        /// <para>
        /// 机や床など、水平な面に置いたタグ向けです。
        /// 壁のように法線が水平なタグには使いません。
        /// </para>
        /// </remarks>
        /// <param name="rotation">タグのワールドでの向きです。</param>
        /// <param name="localNormalAxis">タグ座標での法線の軸です。長さは 1 でなくてもかまいません。</param>
        /// <returns>傾きを取り除いた向きを返します。</returns>
        public static Quaternion AlignNormalToVertical(Quaternion rotation, Vector3 localNormalAxis)
        {
            var normal = rotation * localNormalAxis.normalized;
            var target = normal.y >= 0f ? Vector3.up : Vector3.down;
            return Quaternion.FromToRotation(normal, target) * rotation;
        }

        /// <summary>
        /// タグの姿勢にオフセットを足して、対象の姿勢を求めます。
        /// </summary>
        /// <remarks>
        /// <para>
        /// 基準の向きは「ワールド軸の回転 × タグの回転」です。
        /// ワールドの回転を 0 にすれば、タグの向きのままになります。
        /// </para>
        /// <para>
        /// 位置のオフセットとローカルのオイラー角は、この基準の座標系で足します。
        /// そのため、ワールドの回転は、タグの位置を軸に全体を回します。
        /// </para>
        /// </remarks>
        /// <param name="tag">タグのワールド姿勢です。</param>
        /// <param name="localPosition">基準の座標系での位置のオフセット（m）です。</param>
        /// <param name="localEuler">基準の座標系での回転のオフセット（度）です。</param>
        /// <param name="worldEuler">ワールド軸での回転のオフセット（度）です。</param>
        /// <returns>対象のワールド姿勢を返します。</returns>
        public static Pose ApplyOffsets(Pose tag, Vector3 localPosition, Vector3 localEuler, Vector3 worldEuler)
        {
            var frame = Quaternion.Euler(worldEuler) * tag.rotation;
            return new Pose(tag.position + frame * localPosition, frame * Quaternion.Euler(localEuler));
        }

        /// <summary>
        /// 観測したタグをシーンの基準の姿勢に重ねるため、リグを動かした後の姿勢を求めます。
        /// </summary>
        /// <remarks>
        /// <para>
        /// リグはユーザー側の OVRCameraRig です。
        /// 動かすのは、位置と鉛直軸まわりの向きだけです。
        /// VR の水平線が傾かないよう、傾きは変えません。
        /// </para>
        /// <para>
        /// 動かす前後で、タグから見たリグの位置を変えません。
        /// 式で書くと、次のとおりです。
        /// シーンでのユーザーの姿勢 = 基準の姿勢 × 観測したタグの姿勢の逆 × ワールドでのユーザーの姿勢。
        /// </para>
        /// </remarks>
        /// <param name="rig">いまのリグのワールド姿勢です。</param>
        /// <param name="observed">観測したタグのワールド姿勢です。オフセットを足した後の値を渡します。</param>
        /// <param name="desired">タグがあるべき、シーンの基準のワールド姿勢です。</param>
        /// <param name="alignYaw"><see langword="false"/> なら向きは変えず、位置だけ合わせます。</param>
        /// <returns>動かした後のリグのワールド姿勢を返します。</returns>
        public static Pose AlignRigToTarget(Pose rig, Pose observed, Pose desired, bool alignYaw)
        {
            var yaw = alignYaw
                ? Quaternion.AngleAxis(YawDelta(observed.rotation, desired.rotation), Vector3.up)
                : Quaternion.identity;
            return new Pose(
                desired.position + yaw * (rig.position - observed.position),
                yaw * rig.rotation);
        }

        /// <summary>
        /// <paramref name="from"/> から <paramref name="to"/> へ、鉛直軸まわりに回す角度を求めます。
        /// </summary>
        /// <remarks>
        /// 前（Z）が鉛直に近いと、水平面に投影した向きが定まりません。
        /// そのときは、右（X）で比べます。
        /// </remarks>
        /// <param name="from">回す前の向きです。</param>
        /// <param name="to">回した後の向きです。</param>
        /// <returns>上から見て時計回りを正とした角度（度）を返します。範囲は -180〜180 です。</returns>
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

        /// <summary>
        /// 回転が、まだ取れていないことを表す (0,0,0,0) でないかを調べます。
        /// </summary>
        /// <param name="rotation">調べる回転です。</param>
        /// <returns>成分のどれかが 0 でなければ <see langword="true"/> を返します。</returns>
        public static bool IsValidRotation(Quaternion rotation)
        {
            return rotation.x != 0f || rotation.y != 0f || rotation.z != 0f || rotation.w != 0f;
        }
    }
}
