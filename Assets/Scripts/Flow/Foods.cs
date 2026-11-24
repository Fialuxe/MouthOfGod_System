using MouthOfGod.Food;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>シーンに生まれた食べ物への入口です。</summary>
    public static class Foods
    {
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
