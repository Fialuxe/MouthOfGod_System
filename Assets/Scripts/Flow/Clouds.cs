using MouthOfGod.Fork;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 体験者の前に浮かぶ雲への入口です。
    /// </summary>
    /// <remarks>
    /// 並び方や補充は、雲の部品の CloudRing が自分で行います。
    /// シーンに雲の部品が無いときは、何もしません。
    /// </remarks>
    public static class Clouds
    {
        static CloudGlow _glow;

        /// <summary>いちばん手前の雲を光らせます。刺してほしいときに使います。</summary>
        public static void Glow() => Part?.Glow();

        /// <summary>光っている雲を、体験者に少し寄せます。なかなか刺さないときのヒントです。</summary>
        public static void Approach() => Part?.Approach();

        /// <summary>光と寄せをやめ、元に戻します。</summary>
        public static void Calm() => Part?.Calm();

        // シーンが変わって部品が消えていたら、探し直します。
        static CloudGlow Part
        {
            get
            {
                if (_glow == null) _glow = Object.FindFirstObjectByType<CloudGlow>(FindObjectsInactive.Include);
                return _glow;
            }
        }
    }
}
