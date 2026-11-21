using System;
using MouthOfGod.Core;

namespace MouthOfGod.Fork
{
    /// <summary>
    /// フォークが刺したか、口の中にあるかを決めます。
    /// </summary>
    /// <remarks>
    /// Unity に依存しないので、EditMode のテストで確かめられます。
    /// 位置や当たり判定は <see cref="ForkTip"/> が調べて、ここに渡します。
    /// イベントは、状態を更新してから出します。
    /// </remarks>
    internal sealed class ForkLogic
    {
        readonly float _enterRadius;
        readonly float _exitRadius;

        /// <param name="enterRadius">先と口の距離がこれ以下になったら、口に入ったとします（m）。</param>
        /// <param name="exitRadius">
        /// 口に入ったあと、これより離れたら出たとします（m）。
        /// 境目で入る・出るをくり返さないよう、<paramref name="enterRadius"/> より少し大きくします。
        /// 小さい値を渡すと、<paramref name="enterRadius"/> にそろえます。
        /// </param>
        public ForkLogic(float enterRadius, float exitRadius)
        {
            _enterRadius = enterRadius;
            _exitRadius = Math.Max(enterRadius, exitRadius);
        }

        /// <inheritdoc cref="IFork.IsLoaded"/>
        public bool IsLoaded { get; private set; }

        /// <inheritdoc cref="IFork.InMouth"/>
        public bool InMouth { get; private set; }

        /// <inheritdoc cref="IFork.Pierced"/>
        public event Action Pierced;

        /// <inheritdoc cref="IFork.MouthEntered"/>
        public event Action MouthEntered;

        /// <inheritdoc cref="IFork.MouthLeft"/>
        public event Action MouthLeft;

        /// <inheritdoc cref="IFork.Emptied"/>
        public event Action Emptied;

        /// <summary>
        /// 雲を刺して、先にかけらを付けます。
        /// </summary>
        /// <returns>
        /// かけらを付けたら <see langword="true"/> です。
        /// もう付いていれば何もせず <see langword="false"/> を返します。
        /// このとき呼び出し側は、雲を取りません。
        /// </returns>
        public bool Pierce()
        {
            if (IsLoaded) return false;
            IsLoaded = true;
            Pierced?.Invoke();
            return true;
        }

        /// <summary>
        /// 先と口の距離から、口に入ったか出たかを決めます。毎フレーム呼びます。
        /// </summary>
        /// <param name="distance">
        /// 先と口の距離（m）です。
        /// 口の位置が分からないときは、<see cref="float.PositiveInfinity"/> を渡します。
        /// </param>
        /// <param name="forced">
        /// <see langword="true"/> なら、距離に関係なく口の中とします。
        /// キーボードなどで試すときに使います。
        /// </param>
        public void UpdateMouth(float distance, bool forced)
        {
            bool inMouth = forced || (InMouth ? distance <= _exitRadius : distance <= _enterRadius);
            if (inMouth == InMouth) return;
            InMouth = inMouth;
            if (inMouth) MouthEntered?.Invoke();
            else MouthLeft?.Invoke();
        }

        /// <summary>
        /// デバイスで決定したときに呼びます。かけらが付いていれば、食べたことにしてなくします。
        /// </summary>
        /// <param name="texture">決定したときの食感です。空なら噛む前の決定なので、何もしません。</param>
        /// <remarks>
        /// 先が口の中かどうかは見ません。
        /// 食べ物は、決定すれば口の位置に生まれるので、それに合わせています。
        /// </remarks>
        public void Confirm(TextureId texture)
        {
            if (texture.IsEmpty || !IsLoaded) return;
            IsLoaded = false;
            Emptied?.Invoke();
        }
    }
}
