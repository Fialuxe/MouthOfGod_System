using System;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// どのフィールドで、どの食感のときに生まれるかを、食べ物が自分で持ちます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 食べ物の Prefab に付けます。
    /// <see cref="FoodSpawner"/> は、自分の一覧からいまのフィールドと食感に合う物を選んで生みます。
    /// </para>
    /// <para>
    /// 食感 a〜f は、小さい物から大きい物の順に割り当てる決まりです。
    /// チュートリアルは、場面の間だけ対応を決め直します。
    /// 決め直しには <see cref="FoodSpawner.Override"/> を使います。
    /// </para>
    /// </remarks>
    public sealed class FoodItem : MonoBehaviour, IFoodItem
    {
        /// <summary>生まれる条件の 1 つです。</summary>
        [Serializable]
        public struct Birth
        {
            /// <summary>
            /// フィールドの ID です。空なら、どのフィールドでも生まれます。
            /// どこにも立っていないときも生まれます。
            /// </summary>
            public string field;

            /// <summary>食感の ID です。</summary>
            public string texture;
        }

        [Tooltip("この食べ物が生まれる条件です。フィールドが合う条件が、空のフィールドの条件より先に選ばれます。フィールドを空にすると、どのフィールドでも生まれます")]
        [SerializeField] Birth[] births = new Birth[0];

        [Tooltip("村人の吹き出しに出す、この食べ物の絵。空なら、吹き出しには名前の文字を出す")]
        [SerializeField] Texture2D icon;

        /// <inheritdoc/>
        public Texture2D Icon => icon;

        [NonSerialized] GameObject _source;

        /// <inheritdoc/>
        /// <remarks>
        /// Prefab そのものなら、自分を返します。
        /// 生まれた物なら、生まれた元の Prefab を返します。
        /// </remarks>
        public GameObject Kind => _source != null ? _source : gameObject;

        /// <summary>
        /// 生まれた物に、元の Prefab を覚えさせます。<see cref="FoodSpawner"/> が生んだときに呼びます。
        /// </summary>
        /// <param name="prefab">生まれた元の Prefab です。</param>
        internal void SetSource(GameObject prefab) => _source = prefab;

        /// <summary>
        /// そのフィールドの前で、その食感のときに生まれるかを返します。
        /// </summary>
        /// <param name="field">体験者の前のフィールドです。</param>
        /// <param name="texture">食感です。</param>
        /// <param name="anyField">
        /// <see langword="true"/> なら、フィールドが空の条件だけを見ます。
        /// <see langword="false"/> なら、<paramref name="field"/> と同じフィールドの条件だけを見ます。
        /// このとき <paramref name="field"/> が空なら、いつも <see langword="false"/> です。
        /// </param>
        /// <returns>合う条件が 1 つでもあれば <see langword="true"/> です。</returns>
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
