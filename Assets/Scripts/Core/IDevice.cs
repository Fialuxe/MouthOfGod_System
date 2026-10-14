using System;

namespace MouthOfGod.Core
{
    /// <summary>触感デバイスの入力。実装は Device（と MockDevice）。物理量は実装の内側で TextureId に変換する。</summary>
    /// <remarks>
    /// 前提（この形に、全員のコードが乗っている）:
    /// - 使う側（Food・Tutorial・UI など）は Devices.WhenReady で受け取り、相手が Mock か実機かを知らない。
    /// - 実機は、IDevice を実装して Devices.Register するだけで差し替わる。実機が先に登録されていれば、MockDevice は自動で作られない。
    ///   そのため実機は、通信がつながる前でも Awake か OnEnable で Register する（Start や接続後だと、Mock が先に登録される）。
    /// - 約束: 噛み始めに Bitten、食感が変わったら TextureChanged、ボタンで Confirmed（そのときの食感）。
    ///   内容量の制御や物理量の変換は、実装の内側に閉じる（使う側に ％ やパターン番号を見せない）。
    /// - イベントはメインスレッドで出す（シリアル通信の受信スレッドから直接出さない）。
    /// この形（メンバー・イベントの意味・受け取り方）を変えたい場合は、使う側すべてに影響するので、別の PR でお願いします。
    /// </remarks>
    public interface IDevice
    {
        /// <summary>いまの食感。まだ噛んでいなければ TextureId.None。噛むのをやめても、最後の食感が残る。</summary>
        TextureId Current { get; }

        /// <summary>
        /// 食感が変わった（新しい食感を運ぶ）。Current を更新してから出す。
        /// 噛み始めは Bitten の直後に、最初の食感で出す（前と同じ食感でも出す）。噛むのをやめても None では出さない。
        /// </summary>
        event Action<TextureId> TextureChanged;

        /// <summary>決定した。決定したときの食感（Current）を運ぶ。噛む前の決定なら TextureId.None。</summary>
        event Action<TextureId> Confirmed;

        /// <summary>噛み始めた。噛み始めるたびに 1 回出す（噛んでいる間は出さない）。</summary>
        event Action Bitten;
    }
}
