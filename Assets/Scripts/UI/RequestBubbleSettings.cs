using UnityEngine;

namespace MouthOfGod.UI
{
    /// <summary>
    /// 村人の吹き出しの見た目の設定。<c>Assets/Resources/RequestBubbleSettings.asset</c> があれば、それを使う。なければコードの既定値で動く。
    /// Project でアセットを選び、Inspector で値を直す。Play 中に直しても、すぐ吹き出しに効き、Play を止めても値は残る。
    /// 変更は、このアセット（ファイル）の差分として残るので、誰が直しても PR で見える。
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "MouthOfGod/UI/Request Bubble Settings")]
    public sealed class RequestBubbleSettings : ScriptableObject
    {
        public const string ResourceName = "RequestBubbleSettings";

        [Tooltip("吹き出し全体の大きさ（ワールドでの倍率）。1 で、アイコンの一辺が約 1.2 m。ミニチュアに合わせて直す")]
        [SerializeField, Min(0.001f)] float _size = 0.07f;

        [Tooltip("村人の頭の目印（Anchor）から、吹き出しの中心までの高さ（m）")]
        [SerializeField] float _height = 0.15f;

        [Tooltip("背景としっぽの不透明度。下げると、吹き出しの後ろの物が透けて見える。アイコンと文字は透けない")]
        [SerializeField, Range(0f, 1f)] float _backgroundOpacity = 0.75f;

        [Tooltip("届いたとき・届かずに終わったとき、色を変えて残す秒数。0 ですぐ消える")]
        [SerializeField, Min(0f)] float _closedHoldSeconds = 0.6f;

        [Tooltip("角の丸み（吹き出しの中の大きさ。アイコンの一辺が 1.2）")]
        [SerializeField, Min(0f)] float _cornerRadius = 0.25f;

        [Tooltip("アイコンと吹き出しの縁のあいだ（吹き出しの中の大きさ）")]
        [SerializeField, Min(0f)] float _iconPadding = 0.08f;

        [Tooltip("欲しい物が出ているときの背景の色（不透明度は Background Opacity で決まる）")]
        [SerializeField] Color _openColor = new Color(1f, 0.95f, 0.7f);

        [Tooltip("届いたときの背景の色")]
        [SerializeField] Color _resolvedColor = new Color(0.6f, 0.95f, 0.6f);

        [Tooltip("届かずに終わったときの背景の色")]
        [SerializeField] Color _unresolvedColor = new Color(0.8f, 0.8f, 0.8f);

        public float Size => _size;
        public float Height => _height;
        public float BackgroundOpacity => _backgroundOpacity;
        public float ClosedHoldSeconds => _closedHoldSeconds;
        public float CornerRadius => _cornerRadius;
        public float IconPadding => _iconPadding;
        public Color OpenColor => _openColor;
        public Color ResolvedColor => _resolvedColor;
        public Color UnresolvedColor => _unresolvedColor;

        /// <summary>アセットがあればそれを、なければ既定値のインスタンスを返す。</summary>
        public static RequestBubbleSettings Load()
        {
            var asset = Resources.Load<RequestBubbleSettings>(ResourceName);
            return asset != null ? asset : CreateInstance<RequestBubbleSettings>();
        }
    }
}
