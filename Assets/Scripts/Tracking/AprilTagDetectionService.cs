using System;
using System.Collections.Generic;
using System.Diagnostics;
using AprilTag;
using Meta.XR;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// パススルーカメラの RGB フレームから AprilTag を検出し、ワールド座標の姿勢を <see cref="IAprilTagSource"/> として公開する。
    /// シーンに 1 つだけ置く。カメラの読み出しと検出は重いため、タグごとではなくここで 1 回だけ行い、
    /// 各 <see cref="AprilTagPlace"/> が結果を共有する。
    /// </summary>
    [RequireComponent(typeof(PassthroughCameraAccess))]
    [DefaultExecutionOrder(-50)]
    public sealed class AprilTagDetectionService : MonoBehaviour, IAprilTagSource
    {
        [Header("Detection")]
        [Tooltip("タグの黒い外枠を含む一辺の長さ（メートル）。実物の寸法と一致させる。")]
        [SerializeField] float _tagSizeMeters = 0.1f;

        [Tooltip("検出時に画像を縮小する倍率。大きいほど速いが、遠くのタグを検出しにくくなる。")]
        [SerializeField, Min(1)] int _decimation = 2;

        [Tooltip("検出の最小間隔（秒）。0 でカメラの新しいフレームごとに検出する。")]
        [SerializeField, Min(0f)] float _minDetectIntervalSeconds = 0.05f;

        [Header("Diagnostics")]
        [Tooltip("検出回数・処理時間・検出した ID を、一定間隔でログに出す（Issue #25 の検証用）。")]
        [SerializeField] bool _logStats = true;
        [SerializeField, Min(0.5f)] float _statsIntervalSeconds = 2f;

        public event Action<IReadOnlyList<AprilTagObservation>> ObservationsUpdated;

        readonly List<AprilTagObservation> _observations = new List<AprilTagObservation>();
        readonly Dictionary<int, AprilTagObservation> _latest = new Dictionary<int, AprilTagObservation>();

        PassthroughCameraAccess _camera;
        TagDetector _detector;
        Vector2Int _detectorResolution;
        DateTime _lastTimestamp;
        double _nextDetectAt;
        bool _warnedUnsupported;

        // 診断用
        readonly Stopwatch _stopwatch = new Stopwatch();
        double _statsStartedAt;
        int _statsFrames;
        int _statsFramesWithTags;
        double _statsDetectMsTotal;
        double _statsDetectMsMax;
        readonly HashSet<int> _statsIds = new HashSet<int>();

        public bool TryGetLatest(int tagId, out AprilTagObservation observation)
        {
            return _latest.TryGetValue(tagId, out observation);
        }

        void Awake()
        {
            _camera = GetComponent<PassthroughCameraAccess>();
        }

        void OnDestroy()
        {
            DisposeDetector();
        }

        void Update()
        {
            if (!PassthroughCameraAccess.IsSupported)
            {
                if (!_warnedUnsupported)
                {
                    _warnedUnsupported = true;
                    Debug.LogWarning("[AprilTag] この環境はパススルーカメラの取得に対応していない（Link v85 以上と Developer Runtime Features が必要）。", this);
                }
                return;
            }
            if (!_camera.IsPlaying) return;

            var now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextDetectAt) return;
            if (_camera.Timestamp == _lastTimestamp) return; // 新しいフレームが来ていない
            _lastTimestamp = _camera.Timestamp;
            _nextDetectAt = now + _minDetectIntervalSeconds;

            Detect(now);
            LogStatsIfDue(now);
        }

        void Detect(double now)
        {
            var colors = _camera.GetColors();
            if (!colors.IsCreated) return;

            var cameraPose = _camera.GetCameraPose();
            if (!AprilTagGeometry.IsValidRotation(cameraPose.rotation)) return;

            var resolution = _camera.CurrentResolution;
            EnsureDetector(resolution);

            var intrinsics = _camera.Intrinsics;
            var imageIntrinsics = AprilTagGeometry.ToImageIntrinsics(
                intrinsics.FocalLength, intrinsics.PrincipalPoint, intrinsics.SensorResolution, resolution);
            var fov = AprilTagGeometry.VerticalFovRadians(imageIntrinsics.FocalLength.y, resolution.y);

            _stopwatch.Restart();
            _detector.ProcessImage(colors.AsReadOnlySpan(), fov, _tagSizeMeters);
            _stopwatch.Stop();

            _observations.Clear();
            foreach (var tag in _detector.DetectedTags)
            {
                var worldPose = AprilTagGeometry.CameraLocalToWorld(cameraPose, tag.Position, tag.Rotation);
                var observation = new AprilTagObservation(tag.ID, worldPose, now);
                _observations.Add(observation);
                _latest[tag.ID] = observation;
                _statsIds.Add(tag.ID);
            }

            var elapsedMs = _stopwatch.Elapsed.TotalMilliseconds;
            _statsFrames++;
            _statsDetectMsTotal += elapsedMs;
            _statsDetectMsMax = Math.Max(_statsDetectMsMax, elapsedMs);
            if (_observations.Count > 0)
            {
                _statsFramesWithTags++;
                ObservationsUpdated?.Invoke(_observations);
            }
        }

        void EnsureDetector(Vector2Int resolution)
        {
            if (_detector != null && _detectorResolution == resolution) return;
            DisposeDetector();
            _detector = new TagDetector(resolution.x, resolution.y, _decimation);
            _detectorResolution = resolution;
        }

        void DisposeDetector()
        {
            _detector?.Dispose();
            _detector = null;
        }

        void LogStatsIfDue(double now)
        {
            if (!_logStats) return;
            if (_statsStartedAt == 0d) _statsStartedAt = now;
            var span = now - _statsStartedAt;
            if (span < _statsIntervalSeconds) return;

            if (_statsFrames > 0)
            {
                Debug.Log(
                    $"[AprilTag] {_statsFrames / span:F1} detections/s, " +
                    $"with tags {_statsFramesWithTags}/{_statsFrames}, " +
                    $"detect {_statsDetectMsTotal / _statsFrames:F1} ms avg / {_statsDetectMsMax:F1} ms max, " +
                    $"ids [{string.Join(",", _statsIds)}], res {_camera.CurrentResolution.x}x{_camera.CurrentResolution.y}", this);
            }

            _statsStartedAt = now;
            _statsFrames = 0;
            _statsFramesWithTags = 0;
            _statsDetectMsTotal = 0d;
            _statsDetectMsMax = 0d;
            _statsIds.Clear();
        }
    }
}
