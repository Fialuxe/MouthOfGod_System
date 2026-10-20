using System;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 体験者が持つフォーク。雲を刺したか（先に雲のかけらが付いているか）と、先が口の中にあるかを知らせる。実装は Fork の ForkTip。
    /// </summary>
    /// <remarks>
    /// - 使う側（Tutorial・UI など）は <see cref="Forks.WhenReady"/> で受け取り、位置の取り方（ハンドトラッキング・AprilTag・キーボード）を知らない。
    /// - 約束: 刺したら <see cref="Pierced"/>、先が口に入ったら <see cref="MouthEntered"/>、出たら <see cref="MouthLeft"/>、
    ///   かけらがなくなったら <see cref="Emptied"/>。状態（<see cref="IsLoaded"/>・<see cref="InMouth"/>）を更新してから出す。
    /// - イベントはメインスレッドで出す。
    /// - この形を変えるときは、使う側すべてに効くので、別の PR にする。
    /// </remarks>
    public interface IFork
    {
        /// <summary>先に雲のかけらが付いている。</summary>
        bool IsLoaded { get; }

        /// <summary>先が口の中にある。</summary>
        bool InMouth { get; }

        /// <summary>雲を刺した（かけらが付いた）。かけらが付いている間は、もう一度は出ない。</summary>
        event Action Pierced;

        /// <summary>先が口に入った。</summary>
        event Action MouthEntered;

        /// <summary>先が口から出た。</summary>
        event Action MouthLeft;

        /// <summary>かけらがなくなった（決定して、食べ物が生まれたとき）。</summary>
        event Action Emptied;
    }
}
