using System;

namespace MouthOfGod.Core
{
    /// <summary>触感デバイスの入力。実装は Device（と MockDevice）。物理量は実装の内側で TextureId に変換する。</summary>
    /// <remarks>
    /// 前提（この形に、全員のコードが乗っている）:
    /// - 使う側（Food・Tutorial・UI など）は Devices.WhenReady で受け取り、相手が Mock か実機かを知らない。
    /// - 実機は、IDevice を実装して Devices.Register するだけで差し替わる。実機が先に登録されていれば、MockDevice は自動で作られない。
    /// - 約束: 噛み始めに Bitten、食感が変わったら TextureChanged、ボタンで Confirmed（そのときの食感）。
    ///   内容量の制御や物理量の変換は、実装の内側に閉じる（使う側に ％ やパターン番号を見せない）。
    /// この形（メンバー・イベントの意味・受け取り方）を変えたい場合は、使う側すべてに影響するので、別の PR でお願いします。
    /// </remarks>
    public interface IDevice
    {
        /// <summary>いまの食感。まだ噛んでいなければ TextureId.None。</summary>
        TextureId Current { get; }

        event Action<TextureId> TextureChanged;

        /// <summary>決定した。決定したときの食感（Current）を運ぶ。噛む前の決定なら TextureId.None。</summary>
        event Action<TextureId> Confirmed;

        event Action Bitten;
    }
}
