using System;

namespace MouthOfGod.Core
{
    /// <summary>触感デバイスの入力。実装は Device（と MockDevice）。物理量は実装の内側で TextureId に変換する。</summary>
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
