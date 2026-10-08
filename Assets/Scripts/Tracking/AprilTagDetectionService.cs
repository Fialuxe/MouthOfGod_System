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
    /// シーンに 1 つだけ存在する（<see cref="AprilTagPlace"/> が必要なときに自動で作るので、手で置かなくてよい）。
    /// カメラの読み出しと検出は重いため、タグごとではなくここで 1 回だけ行い、各 <see cref="AprilTagPlace"/> が結果を共有する。
    /// 設定は <see cref="AprilTagSettings"/>（<c>Resources/AprilTagSettings.asset</c>）から読む。
    /// </summary>
    [RequireComponent(typeof(PassthroughCameraAccess))]
    [DefaultExecutionOrder(-50)]
    public sealed class AprilTagDetectionService : MonoBehaviour, IAprilTagSource
    {
        /// <summary>シーンにあればそれを、なければ作って返す。<see cref="AprilTagPlace"/> が使うので、手で置く必要はない。</summary>
        public static AprilTagDetectionService GetOrCreate()
        {
            if (_instance != null) return _instance;

            var existing = FindAnyObjectByType<AprilTagDetectionService>();
            if (existing != null) return existing;

            var go = new GameObject("AprilTag Detection");
            return go.AddComponent<AprilTagDetectionService>();
        }

        static AprilTagDetectionService _instance;

        // Domain Reload を切っていても、Play のたびに初期化する。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }

        public event Action<IReadOnlyList<AprilTagObservation>> ObservationsUpdated;

        readonly List<AprilTagObservation> _observations = new List<AprilTagObservation>();
        readonly Dictionary<int, AprilTagObservation> _latest = new Dictionary<int, AprilTagObservation>();

        AprilTagSettings _settings;
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
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning("[AprilTag] AprilTagDetectionService が複数ある。1 つだけ使う。", this);
            }
            else
            {
                _instance = this;
            }

            _settings = AprilTagSettings.Load();
            _camera = GetComponent<PassthroughCameraAccess>();
            if (GetComponent<AprilTagRecalibrateInput>() == null) gameObject.AddComponent<AprilTagRecalibrateInput>();
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
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
            _nextDetectAt = now + _settings.MinDetectIntervalSeconds;

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
            _detector.ProcessImage(colors.AsReadOnlySpan(), fov, _settings.TagSizeMeters);
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
            _detector = new TagDetector(resolution.x, resolution.y, _settings.Decimation);
            _detectorResolution = resolution;
        }

        void DisposeDetector()
        {
            _detector?.Dispose();
            _detector = null;
        }

        void LogStatsIfDue(double now)
        {
            if (!_settings.LogStats) return;
            if (_statsStartedAt == 0d) _statsStartedAt = now;
            var span = now - _statsStartedAt;
            if (span < _settings.StatsIntervalSeconds) return;

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
