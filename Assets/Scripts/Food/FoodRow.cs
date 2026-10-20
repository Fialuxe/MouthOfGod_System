using System;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// <see cref="FoodTable"/> の 1 行。「この食感のとき、この食べ物が生まれる」。
    /// </summary>
    [Serializable]
    public struct FoodRow
    {
        [Tooltip("食感の ID。デバイスが出す食感と同じ文字にする（大文字小文字は区別する）。空の行は使われない")]
        public string texture;

        [Tooltip("この食感で生まれる食べ物の Prefab。Prefab のファイル名が、食べ物の ID（FoodId）になる")]
        public GameObject food;

        public FoodRow(string texture, GameObject food)
        {
            this.texture = texture;
            this.food = food;
        }
    }
}
