using System;
using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 1 枚のタグの追跡状態を管理します。
    /// 観測を受け取り、平滑化した姿勢と、追跡中かどうかを保ちます。
    /// </summary>
    /// <remarks>
    /// 時刻は呼び出し側が渡します。
    /// <see cref="Observe"/> と <see cref="Tick"/> には、同じ時計の値を渡してください。
    /// </remarks>
    public sealed class PoseTracker
    {
        readonly float _smoothingSeconds;
        readonly float _lostTimeoutSeconds;
        Pose _target;
        double _lastSeenAt;

        /// <param name="smoothingSeconds">平滑化の時定数（秒）です。0 以下なら平滑化せず、観測にそのまま追従します。</param>
        /// <param name="lostTimeoutSeconds">この時間（秒）観測がなければ、見失ったとみなします。</param>
        public PoseTracker(float smoothingSeconds, float lostTimeoutSeconds)
        {
            _smoothingSeconds = smoothingSeconds;
            _lostTimeoutSeconds = lostTimeoutSeconds;
        }

        public bool IsTracked { get; private set; }

        /// <summary>
        /// 平滑化した後の姿勢です。見失っても最後の値を保ちます。
        /// </summary>
        public Pose Current { get; private set; }

        /// <summary>
        /// 見失っていた状態から、観測を受け取ったときに発火します。
        /// </summary>
        public event Action Found;

        /// <summary>
        /// 一定時間観測がなく、見失ったときに <see cref="Tick"/> の中で発火します。
        /// </summary>
        public event Action Lost;

        /// <summary>
        /// 観測を 1 つ受け取ります。
        /// </summary>
        /// <param name="pose">観測した姿勢です。</param>
        /// <param name="now">観測した時刻（秒）です。</param>
        public void Observe(Pose pose, double now)
        {
            _target = pose;
            _lastSeenAt = now;
            if (IsTracked) return;

            // 見つけた直後は、補間せずにその位置へ置きます。前回の位置から滑ってこないようにするためです。
            Current = pose;
            IsTracked = true;
            Found?.Invoke();
        }

        /// <summary>
        /// 時間を進めて、姿勢を観測へ近づけます。見失いの判定もここで行います。
        /// </summary>
        /// <remarks>
        /// 毎フレーム 1 回呼びます。
        /// </remarks>
        /// <param name="now">いまの時刻（秒）です。</param>
        /// <param name="deltaTime">前回の呼び出しからの経過時間（秒）です。</param>
        public void Tick(double now, float deltaTime)
        {
            if (!IsTracked) return;

            if (now - _lastSeenAt > _lostTimeoutSeconds)
            {
                IsTracked = false;
                Lost?.Invoke();
                return;
            }

            var alpha = _smoothingSeconds <= 0f ? 1f : 1f - Mathf.Exp(-deltaTime / _smoothingSeconds);
            Current = new Pose(
                Vector3.Lerp(Current.position, _target.position, alpha),
                Quaternion.Slerp(Current.rotation, _target.rotation, alpha));
        }
    }
}
