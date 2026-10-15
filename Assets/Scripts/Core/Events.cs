using UnityEngine;

namespace MouthOfGod.Core
{
    // 機能をまたぐイベントのデータ。値を運ぶだけで、ロジックは持たない（docs/architecture.md §3）。
    // デバイスの入力（食感が変わった・決定した・噛んだ）は、ここではなく IDevice のイベント。

    /// <summary>食べ物が生まれた。</summary>
    public readonly struct Spawned
    {
        public readonly FoodId Food;
        public readonly Vector3 Position;

        public Spawned(FoodId food, Vector3 position)
        {
            Food = food;
            Position = position;
        }
    }

    /// <summary>物がフィールドに届いたとき、そのフィールドにとって何だったか。採点や反応は、これを見て決める。</summary>
    public enum DeliveryKind
    {
        /// <summary>吹き出しで欲しがっていた物。そのステップが済み、フィールドの一部が戻る。</summary>
        Requested,

        /// <summary>このフィールドの物だが、いま欲しがってはいない（2 本目のにんじん、まだ頼んでいない物）。フィールドは戻らない。</summary>
        Own,

        /// <summary>このフィールドの物ではない（村に豚）。フィールドは戻らない。</summary>
        Foreign,
    }

    /// <summary>物がフィールドに届いた。何が届いても、届いたたびに 1 回出る。Kind で、欲しがっていた物か・このフィールドの物か・よその物かが分かる。</summary>
    public readonly struct Delivered
    {
        public readonly FoodId Food;
        public readonly FieldId Field;
        public readonly DeliveryKind Kind;

        public Delivered(FoodId food, FieldId field, DeliveryKind kind)
        {
            Food = food;
            Field = field;
            Kind = kind;
        }

        /// <summary>欲しがっていた物だったか（Kind == Requested）。</summary>
        public bool WasRequested => Kind == DeliveryKind.Requested;
    }

    /// <summary>フィールドの困っている人が、欲しい物を出した（吹き出しを出す合図）。1 つのフィールドに、同時に出ているのは 1 つだけ。</summary>
    public readonly struct RequestIssued
    {
        public readonly FoodId Food;
        public readonly FieldId Field;

        /// <summary>吹き出しを出す場所（困っている人の頭の上など）。ミニチュアと一緒に動くので、位置ではなく Transform で渡す。無ければ null。</summary>
        public readonly Transform Anchor;

        public RequestIssued(FoodId food, FieldId field, Transform anchor = null)
        {
            Food = food;
            Field = field;
            Anchor = anchor;
        }
    }

    /// <summary>出ていた欲しい物が終わった（吹き出しを消す合図）。Resolved は、届いて解決したか（false なら、待ちきれなかった・やり直した）。</summary>
    public readonly struct RequestClosed
    {
        public readonly FoodId Food;
        public readonly FieldId Field;
        public readonly bool Resolved;

        public RequestClosed(FoodId food, FieldId field, bool resolved)
        {
            Food = food;
            Field = field;
            Resolved = resolved;
        }
    }

    /// <summary>Tutorial から Village や UI への指示（「この場所を光らせる」など）。</summary>
    public readonly struct CueRequested
    {
        public readonly CueId Cue;

        public CueRequested(CueId cue)
        {
            Cue = cue;
        }
    }
}
