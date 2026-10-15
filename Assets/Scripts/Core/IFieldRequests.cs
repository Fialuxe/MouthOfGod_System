using System;

namespace MouthOfGod.Core
{
    /// <summary>1 つのフィールドの欲しい物。いつもの実装は Village の FieldStages。Bootstrap が、フィールドの数だけ Game・Tutorial・UI に渡す。</summary>
    /// <remarks>
    /// フィールドを FieldStages とは違う独自の作りにしても、この interface を実装すれば、本編・チュートリアル・吹き出しにそのままつながる。
    /// 分担（docs/requests.md）:
    /// - フィールドは「何が欲しいか」と「届いたら何が変わるか」だけを持つ。いつ出すかは決めない。
    /// - いつ・どのフィールドに出すか（同時にいくつ、待ち時間）は、呼ぶ側（本編は Game、チュートリアルは Tutorial）が決める。
    /// - 1 つのフィールドに、同時に出ている欲しい物は 1 つだけ。フィールドの中の順番は、フィールドのデータで決まっている。
    /// </remarks>
    public interface IFieldRequests
    {
        FieldId Field { get; }

        /// <summary>欲しい物が出ている（吹き出しが出ている）。</summary>
        bool IsOpen { get; }

        /// <summary>次の欲しい物を出せる（出ているものが無く、まだ欲しい物が残っている）。すっかり戻ったフィールドは false。</summary>
        bool CanOpen { get; }

        /// <summary>次の欲しい物を出す（Issued が出る）。出せなければ false。</summary>
        bool Open();

        /// <summary>
        /// 物がこのフィールドに届いた。出ている欲しい物と同じなら解決し（Closed が Resolved = true で出る）true。違う物・出ていないときは false。
        /// 届いたかを決める所（DeliveryZone）が呼ぶ。戻り値は Delivered の Accepted に使える。
        /// </summary>
        bool Resolve(FoodId food);

        /// <summary>出ている欲しい物を、届かないまま終える（待ちきれなかった。Closed が Resolved = false で出る）。出ていなければ何もしない。</summary>
        void Expire();

        /// <summary>はじめ（何も戻っていない）に戻す。出ていた欲しい物も終える（Closed、Resolved = false）。体験の終わりに Bootstrap が呼ぶ。</summary>
        void Restart();

        event Action<RequestIssued> Issued;

        /// <summary>欲しい物が終わった。届いて解決した（Resolved = true）か、Expire やリセットで終わった（false）。</summary>
        event Action<RequestClosed> Closed;
    }
}
