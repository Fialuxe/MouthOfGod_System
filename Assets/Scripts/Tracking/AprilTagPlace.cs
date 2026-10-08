using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 指定した ID の AprilTag の位置に、対象の Transform（既定は自分自身）を合わせる。
    /// 他の機能が必要とするのは、この Transform（子にするか、位置を読む）と、
    /// 追跡中かどうか（<see cref="IsTracked"/>、<see cref="Found"/>、<see cref="Lost"/>）だけ。
    /// タグを見失っても、Transform は最後の位置に残る。表示の切り替えなどは、イベントを受けた側で行う。
    /// </summary>
    public sealed class AprilTagPlace : MonoBehaviour
    {
        [Header("Tag")]
        [SerializeField] int _tagId;
        [SerializeField] AprilTagDetectionService _source;

        [Header("Target")]
        [Tooltip("動かす対象。空なら、このコンポーネントが付いた GameObject 自身。")]
        [SerializeField] Transform _target;
        [SerializeField] bool _followRotation = true;
        [Tooltip("タグのローカル座標でのオフセット（メートル）。")]
        [SerializeField] Vector3 _localPositionOffset;
        [SerializeField] Vector3 _localEulerOffset;

        [Header("Filtering")]
        [Tooltip("平滑化の時定数（秒）。大きいほどなめらかだが、遅れる。0 で平滑化しない。")]
        [SerializeField, Min(0f)] float _smoothingSeconds = 0.1f;
        [Tooltip("この時間、タグが見えなければ見失ったとみなす（秒）。")]
        [SerializeField, Min(0f)] float _lostTimeoutSeconds = 0.5f;

        PoseTracker _tracker;

        public int TagId => _tagId;
        public bool IsTracked => _tracker != null && _tracker.IsTracked;

        public event Action Found;
        public event Action Lost;

        Transform Target => _target != null ? _target : transform;

        void OnEnable()
        {
            if (_source == null)
            {
                Debug.LogError($"[AprilTagPlace] AprilTagDetectionService が設定されていない（tag {_tagId}）。", this);
                enabled = false;
                return;
            }

            _tracker = new PoseTracker(_smoothingSeconds, _lostTimeoutSeconds);
            _tracker.Found += OnTrackerFound;
            _tracker.Lost += OnTrackerLost;
            _source.ObservationsUpdated += OnObservations;
        }

        void OnDisable()
        {
            if (_source != null) _source.ObservationsUpdated -= OnObservations;
            if (_tracker != null)
            {
                _tracker.Found -= OnTrackerFound;
                _tracker.Lost -= OnTrackerLost;
                _tracker = null;
            }
        }

        void Update()
        {
            _tracker.Tick(Time.realtimeSinceStartupAsDouble, Time.unscaledDeltaTime);
            if (_tracker.IsTracked) Apply(_tracker.Current);
        }

        void OnObservations(IReadOnlyList<AprilTagObservation> observations)
        {
            for (var i = 0; i < observations.Count; i++)
            {
                if (observations[i].Id != _tagId) continue;
                _tracker.Observe(observations[i].WorldPose, observations[i].ReceivedAt);
                return;
            }
        }

        void Apply(Pose tagPose)
        {
            var position = tagPose.position + tagPose.rotation * _localPositionOffset;
            if (_followRotation)
            {
                Target.SetPositionAndRotation(position, tagPose.rotation * Quaternion.Euler(_localEulerOffset));
            }
            else
            {
                Target.position = position;
            }
        }

        void OnTrackerFound()
        {
            Found?.Invoke();
        }

        void OnTrackerLost()
        {
            Lost?.Invoke();
        }

        void OnDrawGizmos()
        {
            var t = Target;
            const float size = 0.05f;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(t.position, t.position + t.right * size);
            Gizmos.color = Color.green;
            Gizmos.DrawLine(t.position, t.position + t.up * size);
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(t.position, t.position + t.forward * size);
        }
    }
}
