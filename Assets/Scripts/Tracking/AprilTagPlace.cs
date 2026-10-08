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
    ///
    /// 2 つの動作がある。
    /// 追従（既定）: 見えている間、観測に合わせて動かす（平滑化あり）。
    /// 測って固定（<see cref="_lockAfterCalibration"/>）: 観測を集めて平均した位置に置き、以後は動かさない。
    /// タグが動かないなら、こちらが最も安定する。取り直すときは <see cref="Recalibrate"/>。
    /// </summary>
    public sealed class AprilTagPlace : MonoBehaviour
    {
        [Header("Tag")]
        [SerializeField] int _tagId;

        [Header("Target")]
        [Tooltip("動かす対象。空なら、このコンポーネントが付いた GameObject 自身。")]
        [SerializeField] Transform _target;
        [SerializeField] bool _followRotation = true;
        [Tooltip("タグのローカル座標でのオフセット（メートル）。")]
        [SerializeField] Vector3 _localPositionOffset;
        [SerializeField] Vector3 _localEulerOffset;

        [Header("Stabilization: tilt")]
        [Tooltip("タグの法線を鉛直に揃え、傾き（ピッチ・ロール）を捨てる。水平な面（机・床）に置いたタグ用。壁のタグではオフにする。")]
        [SerializeField] bool _keepNormalVertical;
        [Tooltip("タグ座標での、タグ面に垂直な軸。Marker の薄い板が、タグに貼り付く向きの軸（既定は Z）。")]
        [SerializeField] Vector3 _tagNormalAxis = Vector3.forward;

        [Header("Stabilization: lock")]
        [Tooltip("オンにすると、観測を集めて平均した位置に置き、以後は動かさない（追従しない）。タグが動かない前提。")]
        [SerializeField] bool _lockAfterCalibration;
        [Tooltip("固定する前に集める観測の数。検出は 1 秒に十数回なので、20 でおよそ 1〜2 秒。")]
        [SerializeField, Min(1)] int _calibrationSamples = 20;
        [Tooltip("位置の中央値からこの距離（メートル）より離れた観測を、外れ値として捨てる。")]
        [SerializeField, Min(0f)] float _calibrationPositionOutlierMeters = 0.02f;
        [Tooltip("平均の向きからこの角度（度）より離れた観測を、外れ値として捨てる。")]
        [SerializeField, Min(0f)] float _calibrationAngleOutlierDegrees = 10f;

        [Header("Stabilization: follow mode")]
        [Tooltip("平滑化の時定数（秒）。大きいほどなめらかだが、遅れる。0 で平滑化しない。固定モードでは使わない。")]
        [SerializeField, Min(0f)] float _smoothingSeconds = 0.1f;
        [Tooltip("この時間、タグが見えなければ見失ったとみなす（秒）。固定モードでは使わない。")]
        [SerializeField, Min(0f)] float _lostTimeoutSeconds = 0.5f;

        static readonly List<AprilTagPlace> ActivePlaces = new List<AprilTagPlace>();

        IAprilTagSource _source;
        PoseTracker _tracker;
        PoseCalibrator _calibrator;
        Pose _lockedPose;
        bool _isLocked;

        public int TagId => _tagId;

        /// <summary>追従モードでは、いまタグが見えているか。固定モードでは、位置が固定済みか。</summary>
        public bool IsTracked => _lockAfterCalibration ? _isLocked : (_tracker != null && _tracker.IsTracked);

        /// <summary>固定モードで、観測を集めている最中か。</summary>
        public bool IsCalibrating => _lockAfterCalibration && !_isLocked && _calibrator != null;

        /// <summary>固定モードの進み具合（0〜1）。</summary>
        public float CalibrationProgress =>
            _calibrator == null ? 0f : Mathf.Clamp01(_calibrator.Count / (float)_calibrator.RequiredSamples);

        public event Action Found;
        public event Action Lost;

        Transform Target => _target != null ? _target : transform;

        // Domain Reload を切っていても、Play のたびに初期化する。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ActivePlaces.Clear();
        }

        /// <summary>シーン内の、固定モードのすべての <see cref="AprilTagPlace"/> を取り直す。</summary>
        public static void RecalibrateAll()
        {
            // Recalibrate() が Lost を通知し、受け手が Place を無効にしても壊れないよう、複製に対して回す。
            foreach (var place in ActivePlaces.ToArray()) place.Recalibrate();
        }

        void OnEnable()
        {
            // 検出サービスは、なければ自動で作られる。手で置いたり、つないだりしなくてよい。
            _source = AprilTagDetectionService.GetOrCreate();
            ActivePlaces.Add(this);

            _tracker = new PoseTracker(_smoothingSeconds, _lostTimeoutSeconds);
            _tracker.Found += RaiseFound;
            _tracker.Lost += RaiseLost;
            _calibrator = new PoseCalibrator(_calibrationSamples, _calibrationPositionOutlierMeters, _calibrationAngleOutlierDegrees);
            _isLocked = false;
            _source.ObservationsUpdated += OnObservations;
        }

        void OnDisable()
        {
            ActivePlaces.Remove(this);
            if (_source != null) _source.ObservationsUpdated -= OnObservations;
            if (_tracker != null)
            {
                _tracker.Found -= RaiseFound;
                _tracker.Lost -= RaiseLost;
                _tracker = null;
            }
            _calibrator = null;
        }

        /// <summary>
        /// 固定した位置を捨てて、観測を集め直す。集め終わるまで、対象は直前の位置に残る。
        /// 追従モードでは何もしない。
        /// </summary>
        [ContextMenu("Recalibrate")]
        public void Recalibrate()
        {
            if (!_lockAfterCalibration || _calibrator == null) return;

            _calibrator.Reset();
            if (_isLocked)
            {
                _isLocked = false;
                Lost?.Invoke();
            }
        }

        void Update()
        {
            if (_lockAfterCalibration)
            {
                if (_isLocked) Apply(_lockedPose);
                return;
            }

            _tracker.Tick(Time.realtimeSinceStartupAsDouble, Time.unscaledDeltaTime);
            if (_tracker.IsTracked) Apply(_tracker.Current);
        }

        void OnObservations(IReadOnlyList<AprilTagObservation> observations)
        {
            for (var i = 0; i < observations.Count; i++)
            {
                if (observations[i].Id != _tagId) continue;

                var pose = observations[i].WorldPose;
                if (_keepNormalVertical)
                {
                    pose = new Pose(pose.position, AprilTagGeometry.AlignNormalToVertical(pose.rotation, _tagNormalAxis));
                }

                if (_lockAfterCalibration) Calibrate(pose);
                else _tracker.Observe(pose, observations[i].ReceivedAt);
                return;
            }
        }

        void Calibrate(Pose pose)
        {
            if (_isLocked) return;

            _calibrator.Add(pose);
            if (!_calibrator.IsComplete || !_calibrator.TryGetResult(out var result)) return;

            _lockedPose = result;
            _isLocked = true;
            Found?.Invoke();
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

        void RaiseFound()
        {
            Found?.Invoke();
        }

        void RaiseLost()
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
