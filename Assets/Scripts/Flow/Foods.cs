using MouthOfGod.Core;
using MouthOfGod.Food;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>シーンに生まれた食べ物への入口です。</summary>
    public static class Foods
    {
        /// <summary>
        /// この場面の間だけ、指定した食感のときは、どこに立っていても指定した食べ物を生むようにします。
        /// </summary>
        /// <param name="texture">食感の ID です。</param>
        /// <param name="food">生ませる食べ物の Prefab です。</param>
        /// <remarks>
        /// チュートリアルで使います。
        /// 場面が終わったら <see cref="ClearOverrides"/> で戻します。
        /// 有効なリグが無いときは、何もしません。
        /// </remarks>
        public static void Override(string texture, GameObject food) => Spawner?.Override(new TextureId(texture), food);

        /// <summary><see cref="Override"/> で決めた対応を、すべて戻します。</summary>
        public static void ClearOverrides() => Spawner?.ClearOverrides();

        static FoodSpawner Spawner => PlayerRig.Current != null ? PlayerRig.Current.FoodSpawner as FoodSpawner : null;

        /// <summary>生まれた食べ物を、すべて消します。</summary>
        /// <remarks>前の場面の食べ物が、あとから届かないようにするために使います。</remarks>
        public static void Clear()
        {
            int count = 0;
            foreach (FoodItem item in Object.FindObjectsByType<FoodItem>(FindObjectsSortMode.None))
            {
                Object.Destroy(item.gameObject);
                count++;
            }
            if (count > 0) Debug.Log($"[Flow] 残っていた食べ物を {count} 個消しました。");
        }
    }
}
