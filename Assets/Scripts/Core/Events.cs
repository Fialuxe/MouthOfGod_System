using UnityEngine;

namespace MouthOfGod.Core
{
    // 機能をまたぐ知らせの中身です。値を運ぶだけで、処理は持ちません。
    // 食べ物は、名前ではなく元の Prefab（IFoodItem.Kind）で運びます。

    /// <summary>食べ物が生まれました。</summary>
    public readonly struct Spawned
    {
        public readonly GameObject Food;
        public readonly Vector3 Position;

        public Spawned(GameObject food, Vector3 position)
        {
            Food = food;
            Position = position;
        }
    }

    /// <summary>物がフィールドに届いたとき、そのフィールドにとって何だったかです。</summary>
    public enum DeliveryKind
    {
        /// <summary>欲しがっていた物です。そのステップが済み、フィールドの一部が戻ります。</summary>
        Requested,

        /// <summary>このフィールドの物ですが、いま欲しがってはいません（2 本目のにんじんなど）。フィールドは戻りません。</summary>
        Own,

        /// <summary>このフィールドの物ではありません（村に豚など）。フィールドは戻りません。</summary>
        Foreign,
    }

    /// <summary>物がフィールドに届きました。何が届いても、届くたびに 1 回出ます。</summary>
    public readonly struct Delivered
    {
        public readonly GameObject Food;
        public readonly FieldId Field;
        public readonly DeliveryKind Kind;

        public Delivered(GameObject food, FieldId field, DeliveryKind kind)
        {
            Food = food;
            Field = field;
            Kind = kind;
        }

        /// <summary>欲しがっていた物でした。</summary>
        public bool WasRequested => Kind == DeliveryKind.Requested;
    }
}
