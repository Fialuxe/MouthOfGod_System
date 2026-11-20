using UnityEngine;

namespace MouthOfGod.UI
{
    /// <summary>
    /// 時間切れのあとに出す、届けた数の文（<see cref="GameResultText"/>）の設定（#355）。<c>Assets/Resources/GameResultSettings.asset</c> があれば、それを使う。なければコードの既定値で動く。
    /// Project でアセットを選び、Inspector で値を直す。Play を止めても値は残る。Play 中に直した値は、次に文を出すときに効く。
    /// 変更は、このアセット（ファイル）の差分として残るので、誰が直しても PR で見える。値はどれも仮のもの。
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "MouthOfGod/UI/Game Result Settings")]
    public sealed class GameResultSettings : ScriptableObject
    {
        public const string ResourceName = "GameResultSettings";

        /// <summary>いつもの文。{0} に届けた数が入る。</summary>
        public const string DefaultFormat = "Time's up!\nDelivered: {0}";

        [Tooltip("出す文。{0} に届けた数が入る（仮の値）。書式が壊れていたら、いつもの文で出す")]
        [SerializeField, TextArea(2, 4)] string _format = DefaultFormat;

        [Tooltip("カメラから、水平にどれだけ前に出すか（m。仮の値）")]
        [SerializeField, Min(0f)] float _distance = 2f;

        [Tooltip("カメラ（目）の高さから、どれだけ上に出すか（m。0 で目の高さ。仮の値）")]
        [SerializeField] float _height;

        [Tooltip("文字の大きさ（TextMesh の characterSize。fontSize 64 のとき、0.02 で 1 行がおよそ 0.14 m）")]
        [SerializeField, Min(0.001f)] float _characterSize = 0.02f;

        [Tooltip("文字の色")]
        [SerializeField] Color _color = Color.white;

        public string Format => _format;
        public float Distance => _distance;
        public float Height => _height;
        public float CharacterSize => _characterSize;
        public Color Color => _color;

        /// <summary>アセットがあればそれを、なければ既定値のインスタンスを返す。</summary>
        public static GameResultSettings Load()
        {
            var asset = Resources.Load<GameResultSettings>(ResourceName);
            if (asset != null) return asset;
            var defaults = CreateInstance<GameResultSettings>();
            defaults.hideFlags = HideFlags.DontSave; // アセットが無いときの既定値。保存しない
            return defaults;
        }
    }
}
