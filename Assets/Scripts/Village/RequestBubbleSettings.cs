using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 村人の吹き出し（<see cref="VillagerBubble"/>）の見た目の設定です。
    /// </summary>
    /// <remarks>
    /// <para>
    /// アセットは Assets/Data/RequestBubbleSettings.asset の 1 つだけです。
    /// 各村人の <see cref="VillagerBubble"/> がつなぎます。
    /// </para>
    /// <para>
    /// Project でアセットを選び、Inspector で値を直します。
    /// Play 中に直しても、すぐ吹き出しに効きます。Play を止めても値は残ります。
    /// 変更はアセットのファイルの差分として残るので、誰が直しても PR で見えます。
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "RequestBubbleSettings", menuName = "MouthOfGod/Village/Request Bubble Settings")]
    public sealed class RequestBubbleSettings : ScriptableObject
    {
        [Tooltip("吹き出し全体の大きさです（ワールドでの倍率）。1 で、アイコンの一辺が約 1.2 m になります。ミニチュアに合わせて直します。")]
        [SerializeField, Min(0.001f)] float _size = 0.07f;

        [Tooltip("村人の頭の目印（Anchor）から、吹き出しの中心までの高さ（m）です。")]
        [SerializeField] float _height = 0.15f;

        [Tooltip("背景としっぽの不透明度です（0〜1）。下げると、吹き出しの後ろの物が透けて見えます。アイコンと文字は透けません。")]
        [SerializeField, Range(0f, 1f)] float _backgroundOpacity = 0.75f;

        [Tooltip("届いたとき・届かずに終わったとき、色を変えて残す秒数です。0 ですぐ消えます。")]
        [SerializeField, Min(0f)] float _closedHoldSeconds = 0.6f;

        [Tooltip("角の丸みの半径です。吹き出しの中の大きさで、アイコンの一辺が 1.2 です。吹き出しの短い辺の半分より大きくしても、半分で止まります。")]
        [SerializeField, Min(0f)] float _cornerRadius = 0.25f;

        [Tooltip("アイコンと吹き出しの縁のあいだです。吹き出しの中の大きさで、アイコンの一辺が 1.2 です。")]
        [SerializeField, Min(0f)] float _iconPadding = 0.08f;

        [Tooltip("欲しい物が出ているときの背景の色です。不透明度は Background Opacity で決まり、ここの A は使いません。")]
        [SerializeField] Color _openColor = new Color(1f, 0.95f, 0.7f);

        [Tooltip("届いたときの背景の色です。不透明度は Background Opacity で決まります。")]
        [SerializeField] Color _resolvedColor = new Color(0.6f, 0.95f, 0.6f);

        [Tooltip("届かずに終わったときの背景の色です。不透明度は Background Opacity で決まります。")]
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
    }
}
