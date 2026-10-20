using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// <see cref="FoodSpawner"/> が、生んだ食べ物に付ける。どの食べ物かを <see cref="IFoodItem"/> として渡す。
    /// 届け先の判定などは、Food を参照せずに、<see cref="IFoodItem"/> として読む。
    /// </summary>
    sealed class FoodItemTag : MonoBehaviour, IFoodItem
    {
        public FoodId Food { get; set; }
    }
}
