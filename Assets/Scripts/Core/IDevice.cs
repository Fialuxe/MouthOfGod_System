using System;

namespace MouthOfGod.Core
{
    /// <summary>触感デバイスの入力。実装は Device（と MockDevice）。物理量は実装の内側で TextureId に変換する。</summary>
    public interface IDevice
    {
        event Action<TextureId> TextureChanged;
        event Action Confirmed;
        event Action Bitten;
    }
}
