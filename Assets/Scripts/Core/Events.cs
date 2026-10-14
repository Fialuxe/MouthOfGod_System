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

    /// <summary>食べ物が届け先に運ばれた。Accepted は、届け先が受け取ったか。</summary>
    public readonly struct Delivered
    {
        public readonly FoodId Food;
        public readonly FieldId Field;
        public readonly bool Accepted;

        public Delivered(FoodId food, FieldId field, bool accepted)
        {
            Food = food;
            Field = field;
            Accepted = accepted;
        }
    }

    /// <summary>村が、ある食べ物をあるフィールドへ届けるよう要求した。</summary>
    public readonly struct RequestIssued
    {
        public readonly FoodId Food;
        public readonly FieldId Field;

        public RequestIssued(FoodId food, FieldId field)
        {
            Food = food;
            Field = field;
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
