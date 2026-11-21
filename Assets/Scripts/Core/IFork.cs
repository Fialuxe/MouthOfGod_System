using System;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 体験者が持つフォークです。実装は Fork の ForkTip です。
    /// 雲を刺したかと、先が口の中にあるかを知らせます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 使う側は、体験者のリグの <see cref="PlayerRig.Fork"/> から受け取ります。
    /// 位置の取り方は知りません。ハンドトラッキング・AprilTag・キーボードのどれでも同じです。
    /// </para>
    /// <para>
    /// 約束は次のとおりです。
    /// 刺したら <see cref="Pierced"/>、先が口に入ったら <see cref="MouthEntered"/> を出します。
    /// 口から出たら <see cref="MouthLeft"/>、かけらがなくなったら <see cref="Emptied"/> を出します。
    /// どれも、<see cref="IsLoaded"/> と <see cref="InMouth"/> を更新してから出します。
    /// </para>
    /// <para>
    /// イベントはメインスレッドで出します。
    /// この形を変えると使う側すべてに効くので、別の PR にします。
    /// </para>
    /// </remarks>
    public interface IFork
    {
        /// <summary>先に雲のかけらが付いているとき <see langword="true"/> です。</summary>
        bool IsLoaded { get; }

        /// <summary>先が口の中にあるとき <see langword="true"/> です。</summary>
        bool InMouth { get; }

        /// <summary>雲を刺して、かけらが付いたときに出ます。</summary>
        /// <remarks>かけらが付いている間は、もう一度は出ません。</remarks>
        event Action Pierced;

        /// <summary>先が口に入ったときに出ます。</summary>
        event Action MouthEntered;

        /// <summary>先が口から出たときに出ます。</summary>
        event Action MouthLeft;

        /// <summary>かけらがなくなったときに出ます。決定して、食べ物が生まれたときです。</summary>
        event Action Emptied;
    }
}
