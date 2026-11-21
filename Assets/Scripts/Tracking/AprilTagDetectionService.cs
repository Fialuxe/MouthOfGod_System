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
    /// パススルーカメラの映像から AprilTag を検出します。
    /// 検出したタグのワールド姿勢を、<see cref="IAprilTagSource"/> として配ります。
    /// </summary>
    /// <remarks>
    /// <para>
    /// シーンに 1 つだけ置きます。
    /// Main シーンには、AprilTag Detection のプレハブを置いてあります。
    /// 無いときは、<see cref="AprilTagPlace"/> がタグに合わせずに動きます。
    /// </para>
    /// <para>
    /// カメラの読み出しと検出は重い処理です。
    /// そのため、タグごとには行わず、ここで 1 回だけ行います。
    /// 各 <see cref="AprilTagPlace"/> は、その結果を共有します。
    /// </para>
    /// <para>
    /// 設定は、Inspector でつないだ <see cref="AprilTagSettings"/> を使います。
    /// ふだんは <c>Assets/Data/AprilTagSettings.asset</c> をつなぎます。
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(PassthroughCameraAccess))]
    [DefaultExecutionOrder(-50)]
    public sealed class AprilTagDetectionService : MonoBehaviour, IAprilTagSource
    {
        /// <summary>
        /// シーンに置いてある検出サービスを返します。
        /// </summary>
        /// <returns>検出サービス。シーンに無ければ <see langword="null"/> を返します。</returns>
        public static AprilTagDetectionService Find()
            => _instance != null ? _instance : FindAnyObjectByType<AprilTagDetectionService>();

        static AprilTagDetectionService _instance;

        // Domain Reload を切っていても、Play のたびに初期化します。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
        }

        /// <inheritdoc/>
        public event Action<IReadOnlyList<AprilTagObservation>> ObservationsUpdated;

        // 検出のたびに中身を入れ替えて、イベントに渡します。GC を避けるため使い回します。
        readonly List<AprilTagObservation> _observations = new List<AprilTagObservation>();
        // タグ ID ごとの、最後に検出した結果です。見失っても消しません。
        readonly Dictionary<int, AprilTagObservation> _latest = new Dictionary<int, AprilTagObservation>();

        [Tooltip("検出の設定（Assets/Data/AprilTagSettings.asset）。空のときは、コードの既定値で動き、Console にエラーを出します。")]
        [SerializeField] AprilTagSettings _settings;
        PassthroughCameraAccess _camera;
        TagDetector _detector;
        Vector2Int _detectorResolution;
        DateTime _lastTimestamp;
        double _nextDetectAt;
        bool _warnedUnsupported;

        // 以下は LogStats 用の集計です。ログを出すたびに 0 に戻します。
        readonly Stopwatch _stopwatch = new Stopwatch();
        double _statsStartedAt;
        int _statsFrames;
        int _statsFramesWithTags;
        double _statsDetectMsTotal;
        double _statsDetectMsMax;
        readonly HashSet<int> _statsIds = new HashSet<int>();

        /// <inheritdoc/>
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

            if (_settings == null)
            {
                Debug.LogError("[AprilTagDetectionService] 検出の設定（Settings）が空なので、既定の値で動きます。", this);
                _settings = ScriptableObject.CreateInstance<AprilTagSettings>();
            }
            _camera = GetComponent<PassthroughCameraAccess>();
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
            // 同じフレームを 2 回検出しないよう、新しいフレームが来るまで待ちます。
            if (_camera.Timestamp == _lastTimestamp) return;
            _lastTimestamp = _camera.Timestamp;
            _nextDetectAt = now + _settings.MinDetectIntervalSeconds;

            Detect(now);
            LogStatsIfDue(now);
        }

        void Detect(double now)
        {
            var colors = _camera.GetColors();
            if (!colors.IsCreated) return;

            // カメラの姿勢がまだ取れていないフレームは、ワールド座標に直せないので飛ばします。
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

        // 検出器は解像度ごとに作るので、解像度が変わったときだけ作り直します。
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
