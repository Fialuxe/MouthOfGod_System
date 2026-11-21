using System;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// 食べ物の Prefab に付けます。どのフィールドで、どの食感のときに生まれるかを、食べ物が自分で持ちます。
    /// FoodSpawner は、自分の一覧にある食べ物から、今のフィールドと食感に合う物を選んで生みます。
    /// 食感 a〜f は、小さい物から大きい物の順に割り当てる決まりです。チュートリアルは、場面の間だけ食感と食べ物の対応を決め直します（FoodSpawner.Override）。
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

        [Tooltip("村人の吹き出しに出す、この食べ物の絵。空なら、吹き出しには名前の文字を出す")]
        [SerializeField] Texture2D icon;

        /// <summary>吹き出しに出す絵です。空なら null です。</summary>
        public Texture2D Icon => icon;

        [NonSerialized] GameObject _source;

        /// <summary>この食べ物の元の Prefab です。Prefab そのものなら自分を、生まれた物なら生まれた元の Prefab を返します。</summary>
        public GameObject Kind => _source != null ? _source : gameObject;

        /// <summary>生まれた物に、元の Prefab を覚えさせます。FoodSpawner が生んだときに呼びます。</summary>
        internal void SetSource(GameObject prefab) => _source = prefab;

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
