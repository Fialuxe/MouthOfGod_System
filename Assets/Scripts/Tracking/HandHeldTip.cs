using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 手の姿勢から、手に持ったもの（フォークなど）の先の姿勢を出す。先は「手の座標系でのオフセット」で表し、手の向きに合わせて回る。
    /// 先を口に当てた姿勢からオフセットを測り直せる（<see cref="Calibrate"/>）。Unity のオブジェクトに依存しない（<see cref="HandHeldPose"/> が使う）。
    /// </summary>
    public sealed class HandHeldTip
    {
        readonly float _smoothingSeconds;
        bool _hasPose;

        /// <param name="localOffset">手の座標系での、先の位置（m）。</param>
        /// <param name="smoothingSeconds">平滑化の時定数（秒）。0 以下で平滑化しない（手にそのまま追従する）。</param>
        public HandHeldTip(Vector3 localOffset, float smoothingSeconds)
        {
            LocalOffset = localOffset;
            _smoothingSeconds = smoothingSeconds;
        }

        /// <summary>手の座標系での、先の位置（m）。</summary>
        public Vector3 LocalOffset { get; private set; }

        /// <summary>平滑化後の先の姿勢。<see cref="Update"/> を呼ぶ前は単位姿勢。</summary>
        public Pose Current { get; private set; }

        /// <summary>手の姿勢から、オフセットだけ離れた先の姿勢を求める。向きは手と同じ。</summary>
        public static Pose Compute(Pose hand, Vector3 localOffset) =>
            new Pose(hand.position + hand.rotation * localOffset, hand.rotation);

        /// <summary>先が、いまの手から見てワールドの <paramref name="worldPoint"/> にあるときの、オフセットを求める。</summary>
        public static Vector3 SolveOffset(Pose hand, Vector3 worldPoint) =>
            Quaternion.Inverse(hand.rotation) * (worldPoint - hand.position);

        /// <summary>手の姿勢を受け取り、平滑化した先の姿勢を返す。最初の 1 回と、測り直した直後は、補間せずその位置に置く。</summary>
        public Pose Update(Pose hand, float deltaTime)
        {
            var target = Compute(hand, LocalOffset);
            if (!_hasPose || _smoothingSeconds <= 0f)
            {
                Current = target;
                _hasPose = true;
                return Current;
            }

            var alpha = 1f - Mathf.Exp(-deltaTime / _smoothingSeconds);
            Current = new Pose(
                Vector3.Lerp(Current.position, target.position, alpha),
                Quaternion.Slerp(Current.rotation, target.rotation, alpha));
            return Current;
        }

        /// <summary>先がいま <paramref name="worldPoint"/>（たとえば口の位置）にあるとして、オフセットを測り直す。</summary>
        public void Calibrate(Pose hand, Vector3 worldPoint)
        {
            LocalOffset = SolveOffset(hand, worldPoint);
            _hasPose = false; // 次の Update で、前の位置から滑ってこないようにする
        }
    }
}
