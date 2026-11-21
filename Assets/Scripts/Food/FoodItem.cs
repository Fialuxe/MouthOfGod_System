using System;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// 食べ物の Prefab に付けます。どのフィールドで、どの食感のときに生まれるかを、食べ物が自分で持ちます。
    /// FoodSpawner は、Resources/Prefabs/Food にある食べ物から、今のフィールドと食感に合う物を選んで生みます。
    /// </summary>
    public sealed class FoodItem : MonoBehaviour, IFoodItem
    {
        /// <summary>生まれる条件 1 つです。field が空なら、どのフィールドでも（どこにも立っていなくても）生まれます。</summary>
        [Serializable]
        public struct Birth
        {
            public string field;
            public string texture;
        }

        [Tooltip("この食べ物が生まれる条件です。フィールドが合う条件が、空のフィールドの条件より先に選ばれます")]
        [SerializeField] Birth[] births = new Birth[0];

        /// <summary>食べ物の名前です。Prefab のファイル名を使います。</summary>
        public FoodId Food => new FoodId(name.Replace("(Clone)", "").Trim());

        /// <summary>field の前で texture のときに生まれるか。anyField なら、フィールドが空の条件だけを見ます。</summary>
        public bool IsBornFrom(FieldId field, TextureId texture, bool anyField)
        {
            foreach (Birth birth in births)
            {
                if (birth.texture != texture.Value) continue;
                if (anyField ? string.IsNullOrEmpty(birth.field) : !field.IsEmpty && birth.field == field.Value) return true;
            }
            return false;
        }
    }
}
