using System;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 触感デバイスからの入力です。実装は Device と MockDevice です。
    /// 物理量は、実装の内側で <see cref="TextureId"/> に変換します。
    /// </summary>
    /// <remarks>
    /// <para>
    /// この形に、全員のコードが乗っています。
    /// 使う側は、体験者のリグの <see cref="PlayerRig.Device"/> から受け取ります。
    /// 相手が Mock か実機かは知りません。
    /// 実機に替えるときは、IDevice を実装した部品をリグの子に置きます。
    /// それをリグの Device に入れるだけで差し替わります。
    /// </para>
    /// <para>
    /// 約束は次のとおりです。
    /// 噛み始めに <see cref="Bitten"/> を出します。
    /// 食感が変わったら <see cref="TextureChanged"/> を出します。
    /// ボタンで <see cref="Confirmed"/> を出し、そのときの食感を運びます。
    /// 内容量の制御や物理量の変換は、実装の内側に閉じます。
    /// 使う側に、％ やパターン番号を見せません。
    /// </para>
    /// <para>
    /// イベントはメインスレッドで出します。
    /// シリアル通信の受信スレッドから、直接は出しません。
    /// </para>
    /// <para>
    /// メンバー・イベントの意味・受け取り方を変えると、使う側すべてに効きます。
    /// 変えたいときは、別の PR でお願いします。
    /// </para>
    /// </remarks>
    public interface IDevice
    {
        /// <summary>いまの食感です。</summary>
        /// <value>
        /// まだ噛んでいなければ <see cref="TextureId.None"/> です。
        /// 噛むのをやめても、最後の食感が残ります。
        /// </value>
        TextureId Current { get; }

        /// <summary>食感が変わったときに、新しい食感を運んで出ます。</summary>
        /// <remarks>
        /// <see cref="Current"/> を更新してから出します。
        /// 噛み始めは、<see cref="Bitten"/> の直後に最初の食感で出します。前と同じ食感でも出します。
        /// 噛むのをやめても、<see cref="TextureId.None"/> では出しません。
        /// </remarks>
        event Action<TextureId> TextureChanged;

        /// <summary>決定したときに出ます。そのときの <see cref="Current"/> を運びます。</summary>
        /// <remarks>噛む前に決定したときは <see cref="TextureId.None"/> を運びます。</remarks>
        event Action<TextureId> Confirmed;

        /// <summary>噛み始めるたびに 1 回出ます。噛んでいる間は出ません。</summary>
        event Action Bitten;
    }
}
