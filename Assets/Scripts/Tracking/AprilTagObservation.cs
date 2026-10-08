using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// ある 1 フレームで検出された AprilTag 1 枚分の結果。ワールド座標。
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

        /// <summary>タグ中心のワールド座標での位置と向き。向きの軸は AprilTag ライブラリの規約に従う。</summary>
        public Pose WorldPose { get; }

        /// <summary>結果を受け取った時刻（<see cref="Time.realtimeSinceStartupAsDouble"/>）。見失い判定に使う。</summary>
        public double ReceivedAt { get; }
    }
}
