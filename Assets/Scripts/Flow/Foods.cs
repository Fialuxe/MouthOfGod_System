using MouthOfGod.Core;
using MouthOfGod.Food;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>シーンに生まれた食べ物への入口です。</summary>
    public static class Foods
    {
        /// <summary>
        /// この場面の間だけ、texture のときは、どこに立っていても food を生むようにします。チュートリアルで使います。
        /// 場面が終わったら <see cref="ClearOverrides"/> で戻します。
        /// </summary>
        public static void Override(string texture, GameObject food) => Spawner?.Override(new TextureId(texture), food);

        /// <summary><see cref="Override"/> で決めた対応を、すべて戻します。</summary>
        public static void ClearOverrides() => Spawner?.ClearOverrides();

        static FoodSpawner Spawner => Object.FindFirstObjectByType<FoodSpawner>();

        /// <summary>生まれた食べ物を、すべて消します。前の場面の食べ物が、あとから届かないようにします。</summary>
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
