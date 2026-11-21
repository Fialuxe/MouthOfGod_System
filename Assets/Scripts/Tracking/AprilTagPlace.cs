using System;
using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 指定した ID の AprilTag の位置に、対象の Transform を合わせます。
    /// 対象は、既定では自分自身です。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 他の機能は、この Transform を子にするか、位置を読んで使います。
    /// 追跡中かどうかは、<see cref="IsTracked"/>、<see cref="Found"/>、<see cref="Lost"/> で知ります。
    /// </para>
    /// <para>
    /// タグを見失っても、Transform は最後の位置に残ります。
    /// 表示の切り替えなどは、イベントを受けた側で行います。
    /// </para>
    /// <para>
    /// 動作は 2 つあります。
    /// 既定の追従では、見えている間、観測に合わせてなめらかに動かします。
    /// 測って固定（<see cref="_lockAfterCalibration"/>）では、観測を集めて平均した位置に置きます。
    /// 以後は動かしません。
    /// タグが動かないなら、固定が最も安定します。
    /// 取り直すときは <see cref="Recalibrate"/> を呼びます。
    /// </para>
    /// <para>
    /// シーンに <see cref="AprilTagDetectionService"/> が無いときは、タグに合わせず今の位置のまま使います。
    /// </para>
    /// </remarks>
    public sealed class AprilTagPlace : MonoBehaviour
    {
        [Header("Tag")]
        [Tooltip("合わせる AprilTag の ID。tagStandard41h12 の番号。")]
        [SerializeField] int _tagId;

        [Header("Target")]
        [Tooltip("動かす対象。空なら、このコンポーネントが付いた GameObject 自身。")]
        [SerializeField] Transform _target;
        [Tooltip("オンなら、位置に加えて向きもタグに合わせる。オフなら位置だけ合わせる。Align Rig Instead では、オフにするとリグの向きを回さない。")]
        [SerializeField] bool _followRotation = true;
        [Tooltip("タグのローカル座標でのオフセット（メートル）。")]
        [SerializeField] Vector3 _localPositionOffset;
        [Tooltip("タグの向きに足す回転（度）。タグの座標系で指定する。対象の向きとタグの向きの差を、ここで合わせる。")]
        [SerializeField] Vector3 _localEulerOffset;
        [Tooltip("ワールド軸での回転オフセット（度）。タグの位置を軸に、全体を回す。タグの座標系ではなく、ワールドの軸で指定する（Y が鉛直軸まわりの向き）。位置とローカルのオフセットは、回したあとの向きで足す。")]
        [SerializeField] Vector3 _worldEulerOffset;

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

        [Header("Alignment")]
        [Tooltip("オンにすると、対象を動かす代わりに、OVRCameraRig（ユーザー側）を動かして、観測したタグを、対象のいまのワールド姿勢に重ねる。Terrain のように回せないものが子にあるときに使う。固定モード（Lock After Calibration）と Keep Normal Vertical を、自動で有効にする。リグは、位置と鉛直軸まわりの向きだけ動かす（傾きは変えない）。対象の向きは Y が上、前（Z）が水平になるようにし、タグの向きとの差は Local Euler Offset で合わせる。固定が終わったときに 1 回だけ合わせ、取り直すとまた合わせる。")]
        [SerializeField] bool _alignRigInstead;

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

        /// <summary>
        /// タグに合っているかを返します。
        /// </summary>
        /// <value>
        /// 追従モードでは、いまタグが見えていれば <see langword="true"/> です。
        /// 固定モードでは、位置を固定し終えていれば <see langword="true"/> です。
        /// </value>
        public bool IsTracked => LockMode ? _isLocked : (_tracker != null && _tracker.IsTracked);

        /// <summary>
        /// 固定モードで、観測を集めている最中かを返します。
        /// </summary>
        public bool IsCalibrating => LockMode && !_isLocked && _calibrator != null;

        /// <summary>
        /// 固定モードで、観測をどこまで集めたかを返します。
        /// </summary>
        /// <value>0〜1 の値です。1 で集め終わりです。無効のときは 0 です。</value>
        public float CalibrationProgress =>
            _calibrator == null ? 0f : Mathf.Clamp01(_calibrator.Count / (float)_calibrator.RequiredSamples);

        /// <summary>
        /// タグに合ったときに発火します。
        /// </summary>
        /// <remarks>
        /// 追従モードでは、タグが見え始めたときです。
        /// 固定モードでは、位置を固定し終えたときです。
        /// リグを合わせるときは、リグを動かした後に発火します。
        /// </remarks>
        public event Action Found;

        /// <summary>
        /// タグに合わなくなったときに発火します。
        /// </summary>
        /// <remarks>
        /// 追従モードでは、タグが一定時間見えなくなったときです。
        /// 固定モードでは、固定済みの位置を <see cref="Recalibrate"/> で捨てたときです。
        /// </remarks>
        public event Action Lost;

        Transform Target => _target != null ? _target : transform;

        // リグを合わせるモードは、固定モードを前提にします。
        // 追従すると、リグが動くたびに観測が変わって、振動するためです。
        bool LockMode => _lockAfterCalibration || _alignRigInstead;

        // Domain Reload を切っていても、Play のたびに初期化します。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            ActivePlaces.Clear();
        }

        /// <summary>
        /// シーン内の、固定モードの <see cref="AprilTagPlace"/> をすべて取り直します。
        /// </summary>
        /// <remarks>
        /// 有効な Place だけが対象です。追従モードの Place は何もしません。
        /// </remarks>
        public static void RecalibrateAll()
        {
            // Recalibrate() が Lost を通知し、受け手が Place を無効にすることがあります。
            // それでも壊れないよう、複製に対して回します。
            foreach (var place in ActivePlaces.ToArray()) place.Recalibrate();
        }

        void OnEnable()
        {
            _source = AprilTagDetectionService.Find();
            if (_source == null)
            {
                Debug.LogWarning($"[AprilTagPlace] {name}: シーンに AprilTagDetectionService がありません。タグに合わせずに、今の位置のまま使います。", this);
                return;
            }
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
        /// 固定した位置を捨てて、観測を集め直します。
        /// 追従モードでは何もしません。
        /// </summary>
        /// <remarks>
        /// 集め終わるまで、対象は直前の位置に残ります。
        /// 固定済みだったときは、<see cref="Lost"/> を発火します。
        /// </remarks>
        [ContextMenu("Recalibrate")]
        public void Recalibrate()
        {
            if (!LockMode || _calibrator == null) return;

            _calibrator.Reset();
            if (_isLocked)
            {
                _isLocked = false;
                Lost?.Invoke();
            }
        }

        void Update()
        {
            if (LockMode)
            {
                if (_isLocked && !_alignRigInstead) Apply(_lockedPose);
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
                if (_keepNormalVertical || _alignRigInstead)
                {
                    pose = new Pose(pose.position, AprilTagGeometry.AlignNormalToVertical(pose.rotation, _tagNormalAxis));
                }

                if (LockMode) Calibrate(pose);
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
            if (_alignRigInstead) AlignRig(result);
            Found?.Invoke();
        }

        void Apply(Pose tagPose)
        {
            var pose = AprilTagGeometry.ApplyOffsets(tagPose, _localPositionOffset, _localEulerOffset, _worldEulerOffset);
            if (_followRotation) Target.SetPositionAndRotation(pose.position, pose.rotation);
            else Target.position = pose.position;
        }

        // 観測したタグが、対象のいまの姿勢に重なるように、リグを動かします。対象は動かしません。
        void AlignRig(Pose tagPose)
        {
            var rig = FindFirstObjectByType<OVRCameraRig>();
            if (rig == null)
            {
                Debug.LogWarning("[AprilTag] OVRCameraRig が見つからないので、リグを合わせられません。", this);
                return;
            }

            var observed = AprilTagGeometry.ApplyOffsets(tagPose, _localPositionOffset, _localEulerOffset, _worldEulerOffset);
            var desired = new Pose(Target.position, Target.rotation);
            var current = new Pose(rig.transform.position, rig.transform.rotation);
            var aligned = AprilTagGeometry.AlignRigToTarget(current, observed, desired, _followRotation);
            rig.transform.SetPositionAndRotation(aligned.position, aligned.rotation);
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
