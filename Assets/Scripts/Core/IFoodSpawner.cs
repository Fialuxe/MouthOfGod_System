using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 決定した食感から、食べ物を生む所です。実装は Food の FoodSpawner です。
    /// 使う側は、体験者のリグの <see cref="PlayerRig.FoodSpawner"/> から受け取ります。
    /// </summary>
    /// <remarks>
    /// <para>
    /// チュートリアルは、<see cref="Spawned"/> で何が生まれたかを待ちます。
    /// 影を出す UI は、<see cref="Preview"/> でいまの食感なら何が生まれるかを引きます。
    /// </para>
    /// <para>
    /// 食感と食べ物の対応表は、実装の内側にあります。使う側は表を知りません。
    /// イベントはメインスレッドで出します。
    /// この形を変えると使う側すべてに効くので、別の PR にします。
    /// </para>
    /// </remarks>
    public interface IFoodSpawner
    {
        /// <summary>食べ物を生んだ直後に 1 回出ます。</summary>
        event Action<Spawned> Spawned;

        /// <summary>その食感なら生まれる食べ物を引きます。食べ物は生みません。</summary>
        /// <param name="texture">調べる食感です。</param>
        /// <returns>生まれる食べ物の Prefab です。引けなければ <see langword="null"/> です。</returns>
        /// <remarks>いま立っているフィールドの表で引きます。</remarks>
        GameObject Preview(TextureId texture);
    }
}
