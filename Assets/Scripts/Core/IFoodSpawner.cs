using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 決定した食感から食べ物を生む所。実装は Food の FoodSpawner。使う側（Tutorial・UI）は <see cref="FoodSpawners.WhenReady"/> で受け取る。
    /// </summary>
    /// <remarks>
    /// - チュートリアルは <see cref="Spawned"/> で「何が生まれたか」を待つ。影を出す UI は <see cref="Preview"/> で「いまの食感なら何が生まれるか」を引く。
    /// - 食感と食べ物の対応（表）は、実装の内側にある。使う側は表を知らない。
    /// - イベントはメインスレッドで出す。
    /// - この形を変えるときは、使う側すべてに効くので、別の PR にする。
    /// </remarks>
    public interface IFoodSpawner
    {
        /// <summary>食べ物を生んだ直後に、1 回出る。</summary>
        event Action<Spawned> Spawned;

        /// <summary>その食感なら生まれる食べ物の Prefab を引くだけ。生まない。引けなければ null。いま立っているフィールドの表で引く。</summary>
        GameObject Preview(TextureId texture);
    }
}
