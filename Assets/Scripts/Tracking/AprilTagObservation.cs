using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 1 フレームで検出した AprilTag 1 枚分の結果を保持します。
    /// 姿勢はワールド座標で持ちます。
    /// </summary>
    public readonly struct AprilTagObservation
    {
        public AprilTagObservation(int id, Pose worldPose, double receivedAt)
        {
            Id = id;
            WorldPose = worldPose;
            ReceivedAt = receivedAt;
        }

        public int Id { get; }

        /// <summary>
        /// タグ中心のワールド座標での位置と向きです。
        /// </summary>
        /// <remarks>
        /// 向きの軸は、AprilTag ライブラリの決まりに従います。
        /// Unity のオブジェクトの軸とは一致しないことがあります。
        /// </remarks>
        public Pose WorldPose { get; }

        /// <summary>
        /// 結果を受け取った時刻です。
        /// <see cref="Time.realtimeSinceStartupAsDouble"/> の値で、見失いの判定に使います。
        /// </summary>
        public double ReceivedAt { get; }
    }
}
