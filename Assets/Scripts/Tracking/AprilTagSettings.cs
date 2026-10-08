using UnityEngine;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// AprilTag 検出の設定。<c>Assets/Resources/AprilTagSettings.asset</c> があれば、それを使う。なければコードの既定値で動く。
    /// シーンやオブジェクトに値を入れる必要はなく、変更は、このアセット（ファイル）の差分として残る。
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "MouthOfGod/Tracking/AprilTag Settings")]
    public sealed class AprilTagSettings : ScriptableObject
    {
        public const string ResourceName = "AprilTagSettings";

        [Tooltip("タグの一辺の長さ（メートル）。tagStandard41h12 では、黒い枠の**内側の縁**で囲まれた正方形（5 マス分）の一辺。1 マスの一辺 × 5。")]
        [SerializeField, Min(0.001f)] float _tagSizeMeters = 0.075f;

        [Tooltip("検出時に画像を縮小する倍率。大きいほど速いが、遠くのタグを検出しにくくなる。")]
        [SerializeField, Min(1)] int _decimation = 2;

        [Tooltip("検出の最小間隔（秒）。0 でカメラの新しいフレームごとに検出する。")]
        [SerializeField, Min(0f)] float _minDetectIntervalSeconds = 0.05f;

        [Tooltip("検出回数・処理時間・検出した ID を、一定間隔でログに出す。")]
        [SerializeField] bool _logStats = true;

        [SerializeField, Min(0.5f)] float _statsIntervalSeconds = 2f;

        public float TagSizeMeters => _tagSizeMeters;
        public int Decimation => _decimation;
        public float MinDetectIntervalSeconds => _minDetectIntervalSeconds;
        public bool LogStats => _logStats;
        public float StatsIntervalSeconds => _statsIntervalSeconds;

        /// <summary>アセットがあればそれを、なければ既定値のインスタンスを返す。</summary>
        public static AprilTagSettings Load()
        {
            var asset = Resources.Load<AprilTagSettings>(ResourceName);
            return asset != null ? asset : CreateInstance<AprilTagSettings>();
        }
    }
}
