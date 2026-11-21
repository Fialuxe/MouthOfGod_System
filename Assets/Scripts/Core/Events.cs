using UnityEngine;

namespace MouthOfGod.Core
{
    // 機能をまたぐ知らせの中身です。値を運ぶだけで、処理は持ちません。
    // 食べ物は、名前ではなく元の Prefab（IFoodItem.Kind）で運びます。

    /// <summary>食べ物が生まれたことを知らせる中身です。</summary>
    public readonly struct Spawned
    {
        /// <summary>生まれた食べ物の元の Prefab です。</summary>
        public readonly GameObject Food;

        /// <summary>生まれた位置です。ワールド座標です。</summary>
        public readonly Vector3 Position;

        public Spawned(GameObject food, Vector3 position)
        {
            Food = food;
            Position = position;
        }
    }

    /// <summary>届いた物が、そのフィールドにとって何だったかを表します。</summary>
    public enum DeliveryKind
    {
        /// <summary>欲しがっていた物です。そのステップが済み、フィールドの一部が戻ります。</summary>
        Requested,

        /// <summary>
        /// このフィールドの物ですが、いまは欲しがっていません。
        /// 2 本目のにんじんなどです。フィールドは戻りません。
        /// </summary>
        Own,

        /// <summary>
        /// このフィールドの物ではありません。
        /// 村に豚が届いたときなどです。フィールドは戻りません。
        /// </summary>
        Foreign,
    }

    /// <summary>物がフィールドに届いたことを知らせる中身です。</summary>
    /// <remarks>何が届いても、届くたびに 1 回出ます。</remarks>
    public readonly struct Delivered
    {
        /// <summary>届いた食べ物の元の Prefab です。</summary>
        public readonly GameObject Food;

        public readonly FieldId Field;
        public readonly DeliveryKind Kind;

        public Delivered(GameObject food, FieldId field, DeliveryKind kind)
        {
            Food = food;
            Field = field;
            Kind = kind;
        }

        /// <summary>欲しがっていた物が届いたとき <see langword="true"/> です。</summary>
        public bool WasRequested => Kind == DeliveryKind.Requested;
    }
}
