using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 食べ物が持つ、「どの食べ物か」の情報です。実装は Food の FoodItem で、食べ物の Prefab に付いています。
    /// </summary>
    /// <remarks>
    /// 同じ食べ物かどうかは、名前ではなく <see cref="Kind"/>（元の Prefab）が同じ物かで比べます。
    /// 生まれた食べ物も、Prefab そのものも、同じ Prefab を返します。
    /// </remarks>
    public interface IFoodItem
    {
        /// <summary>この食べ物の元の Prefab です。</summary>
        GameObject Kind { get; }

        /// <summary>吹き出しに出す絵です。無ければ null です。</summary>
        Texture2D Icon { get; }
    }
}
