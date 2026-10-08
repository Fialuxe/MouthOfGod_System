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

        /// <summary>回転が (0,0,0,0)（未取得）でないか。</summary>
        public static bool IsValidRotation(Quaternion rotation)
        {
            return rotation.x != 0f || rotation.y != 0f || rotation.z != 0f || rotation.w != 0f;
        }
    }
}
