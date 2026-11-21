using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 手の姿勢から、手に持ったものの先の姿勢を求めます。
    /// フォークなどの先に使います。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 先は、手の座標系でのオフセットで表します。
    /// そのため、手の向きに合わせて先も回ります。
    /// 先を口に当てた姿勢から、オフセットを測り直せます（<see cref="Calibrate"/>）。
    /// </para>
    /// <para>
    /// Unity のオブジェクトに依存しません。
    /// <see cref="HandHeldPose"/> から使います。
    /// </para>
    /// </remarks>
    public sealed class HandHeldTip
    {
        readonly float _smoothingSeconds;
        // false のとき、次の Update は補間せずに目標の位置へ置きます。
        bool _hasPose;

        /// <param name="localOffset">手の座標系での、先の位置（m）です。</param>
        /// <param name="smoothingSeconds">平滑化の時定数（秒）です。0 以下なら平滑化せず、手にそのまま追従します。</param>
        public HandHeldTip(Vector3 localOffset, float smoothingSeconds)
        {
            LocalOffset = localOffset;
            _smoothingSeconds = smoothingSeconds;
        }

        /// <summary>
        /// 手の座標系での、先の位置（m）です。
        /// </summary>
        public Vector3 LocalOffset { get; private set; }

        /// <summary>
        /// 平滑化した後の先の姿勢です。
        /// <see cref="Update"/> を呼ぶ前は <see langword="default"/> です。
        /// </summary>
        public Pose Current { get; private set; }

        /// <summary>
        /// 手の姿勢から、オフセットだけ離れた先の姿勢を求めます。
        /// </summary>
        /// <param name="hand">手のワールド姿勢です。</param>
        /// <param name="localOffset">手の座標系での、先の位置（m）です。</param>
        /// <returns>先のワールド姿勢を返します。向きは手と同じです。</returns>
        public static Pose Compute(Pose hand, Vector3 localOffset) =>
            new Pose(hand.position + hand.rotation * localOffset, hand.rotation);

        /// <summary>
        /// 先がワールドの <paramref name="worldPoint"/> にあるとして、オフセットを求めます。
        /// </summary>
        /// <param name="hand">いまの手のワールド姿勢です。</param>
        /// <param name="worldPoint">先があるワールド座標です。</param>
        /// <returns>手の座標系でのオフセット（m）を返します。</returns>
        public static Vector3 SolveOffset(Pose hand, Vector3 worldPoint) =>
            Quaternion.Inverse(hand.rotation) * (worldPoint - hand.position);

        /// <summary>
        /// 手の姿勢を受け取り、平滑化した先の姿勢を返します。
        /// </summary>
        /// <remarks>
        /// 毎フレーム 1 回呼びます。
        /// 最初の 1 回と、測り直した直後は、補間せずに目標の位置へ置きます。
        /// </remarks>
        /// <param name="hand">いまの手のワールド姿勢です。</param>
        /// <param name="deltaTime">前回の呼び出しからの経過時間（秒）です。</param>
        /// <returns>平滑化した先の姿勢を返します。<see cref="Current"/> と同じ値です。</returns>
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

        /// <summary>
        /// 先がいま <paramref name="worldPoint"/> にあるとして、オフセットを測り直します。
        /// </summary>
        /// <param name="hand">いまの手のワールド姿勢です。</param>
        /// <param name="worldPoint">先を当てている点のワールド座標です。たとえば口の位置です。</param>
        public void Calibrate(Pose hand, Vector3 worldPoint)
        {
            LocalOffset = SolveOffset(hand, worldPoint);
            // 次の Update で、前の位置から滑ってこないようにします。
            _hasPose = false;
        }
    }
}
