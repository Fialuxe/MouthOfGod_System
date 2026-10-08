using System;
using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 1 枚のタグの追跡状態。観測を受け取り、平滑化した姿勢と「追跡中か」を管理する。
    /// 見つかった（<see cref="Found"/>）／一定時間見えなくなった（<see cref="Lost"/>）をイベントで通知する。
    /// </summary>
    public sealed class PoseTracker
    {
        readonly float _smoothingSeconds;
        readonly float _lostTimeoutSeconds;
        Pose _target;
        double _lastSeenAt;

        /// <param name="smoothingSeconds">平滑化の時定数（秒）。0 以下で平滑化しない（観測にそのまま追従する）。</param>
        /// <param name="lostTimeoutSeconds">この時間、観測がなければ見失ったとみなす（秒）。</param>
        public PoseTracker(float smoothingSeconds, float lostTimeoutSeconds)
        {
            _smoothingSeconds = smoothingSeconds;
            _lostTimeoutSeconds = lostTimeoutSeconds;
        }

        public bool IsTracked { get; private set; }

        /// <summary>平滑化後の姿勢。見失っても最後の値を保つ。</summary>
        public Pose Current { get; private set; }

        public event Action Found;
        public event Action Lost;

        public void Observe(Pose pose, double now)
        {
            _target = pose;
            _lastSeenAt = now;
            if (IsTracked) return;

            // 見つけた直後は、補間せずその位置に置く（前回の位置から滑ってこないように）。
            Current = pose;
            IsTracked = true;
            Found?.Invoke();
        }

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
