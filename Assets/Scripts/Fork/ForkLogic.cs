using System;
using MouthOfGod.Core;

namespace MouthOfGod.Fork
{
    /// <summary>
    /// フォークの状態（刺したか、口の中か）を決める部分。Unity に依存しないので、EditMode のテストで確かめられる。
    /// 位置や当たり判定は <see cref="ForkTip"/> が調べて、ここに渡す。
    /// </summary>
    internal sealed class ForkLogic
    {
        readonly float _enterRadius;
        readonly float _exitRadius;

        /// <param name="enterRadius">先と口の距離がこれ以下になったら、口に入ったとする（m）。</param>
        /// <param name="exitRadius">口に入ったあと、これより離れたら出たとする（m）。enterRadius より小さければ、enterRadius にそろえる。境目で入る・出るをくり返さないため、少し大きくする。</param>
        public ForkLogic(float enterRadius, float exitRadius)
        {
            _enterRadius = enterRadius;
            _exitRadius = Math.Max(enterRadius, exitRadius);
        }

        public bool IsLoaded { get; private set; }
        public bool InMouth { get; private set; }

        public event Action Pierced;
        public event Action MouthEntered;
        public event Action MouthLeft;
        public event Action Emptied;

        /// <summary>雲を刺す。かけらが付いていなければ付けて true。もう付いていれば何もせず false（雲は取らない）。</summary>
        public bool Pierce()
        {
            if (IsLoaded) return false;
            IsLoaded = true;
            Pierced?.Invoke();
            return true;
        }

        /// <summary>
        /// 先と口の距離を渡す（毎フレーム）。<paramref name="forced"/> が true なら、距離に関係なく口の中とする（キーボードで試すとき）。
        /// 距離が分からないとき（カメラがないなど）は、<see cref="float.PositiveInfinity"/> を渡す。
        /// </summary>
        public void UpdateMouth(float distance, bool forced)
        {
            bool inMouth = forced || (InMouth ? distance <= _exitRadius : distance <= _enterRadius);
            if (inMouth == InMouth) return;
            InMouth = inMouth;
            if (inMouth) MouthEntered?.Invoke();
            else MouthLeft?.Invoke();
        }

        /// <summary>
        /// デバイスで決定した。かけらが付いていれば、食べたことにしてなくす。噛む前の決定（食感が空）は数えない。
        /// 口の中かどうかは見ない（食べ物は、決定すれば口の位置に生まれるので、それに合わせる）。
        /// </summary>
        public void Confirm(TextureId texture)
        {
            if (texture.IsEmpty || !IsLoaded) return;
            IsLoaded = false;
            Emptied?.Invoke();
        }
    }
}
