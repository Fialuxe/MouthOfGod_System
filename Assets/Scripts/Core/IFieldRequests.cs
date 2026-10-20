using System;

namespace MouthOfGod.Core
{
    /// <summary>1 つのフィールドの欲しい物。いつもの実装は Village の FieldStages。フィールドは自分で <see cref="Fields"/> に登録し、Game・Tutorial・UI はそこから受け取る。</summary>
    /// <remarks>
    /// フィールドを FieldStages とは違う独自の作りにしても、この interface を実装すれば、本編・チュートリアル・吹き出しにそのままつながる。
    /// 分担（docs/requests.md）:
    /// - フィールドは「何が欲しいか」と「届いたら何が変わるか」だけを持つ。いつ出すかは決めない。
    /// - いつ・どのフィールドに出すか（同時にいくつ、待ち時間）は、呼ぶ側（本編は Game、チュートリアルは Tutorial）が決める。
    /// - 1 つのフィールドに、同時に複数の欲しい物が出ていることがある（上限はフィールドのデータで決まる）。どれかは番号（Request）で見分ける。フィールドの中の順番は、フィールドのデータで決まっている。
    /// - 同じ番号の Issued と Closed は、1 回ずつ、この順に出る。状態を変えてから知らせるので、イベントの中から Open などをすぐ呼んでよい。メインスレッドから使う。
    /// 約束の全体は docs/requests.md「API 仕様」。形を変えるときは、使う側すべてに効くので別の PR にする。
    /// </remarks>
    public interface IFieldRequests
    {
        FieldId Field { get; }

        /// <summary>欲しい物が 1 つ以上出ている（吹き出しが出ている）。</summary>
        bool IsOpen { get; }

        /// <summary>次の欲しい物を出せる（出ている数が上限より少なく、まだ出していない欲しい物が残っている）。すっかり戻ったフィールドは false。</summary>
        bool CanOpen { get; }

        /// <summary>次の欲しい物を出す（Issued が出る）。出せなければ false。</summary>
        bool Open();

        /// <summary>
        /// その物を欲しがる、まだ済んでいないステップを出す（並び順によらない）。チュートリアルのように、出す物が台本で決まっているときに使う。
        /// 上限まで出ている・その物を欲しがるステップが無いなら false。
        /// </summary>
        bool Open(FoodId food);

        /// <summary>
        /// 物がこのフィールドに届いた。何が届いても受け取り、何だったかを返す（Received も 1 回出る）。
        /// 出ている欲しい物のどれかと同じ物（Requested）なら、同じ物を欲しがる一番古いものが済んで、見た目が変わってから Closed（Resolved = true）が出る。
        /// それ以外（Own・Foreign）は、フィールドは戻らない。反応（豊作・いびつな世界など）は、Received を受けた側が足す。
        /// 物が落ちた場所を決める所（DeliveryZone）が呼ぶ。
        /// </summary>
        DeliveryKind Receive(FoodId food);

        /// <summary>出ている欲しい物を、すべて届かないまま終える（Closed が Resolved = false で、出した順に出る）。出ていなければ何もしない。</summary>
        void Expire();

        /// <summary>その番号の欲しい物だけを、届かないまま終える（待ちきれなかった。Closed が Resolved = false で出る）。出ていなければ何もしない。</summary>
        void Expire(int request);

        /// <summary>はじめ（何も戻っていない）に戻す。出ていた欲しい物も終える（Closed、Resolved = false）。体験の終わり（次の人へ）に呼ぶ。チュートリアルと本編の間では戻さない。</summary>
        void Restart();

        event Action<RequestIssued> Issued;

        /// <summary>欲しい物が終わった。届いて解決した（Resolved = true）か、Expire やリセットで終わった（false）。</summary>
        event Action<RequestClosed> Closed;

        /// <summary>物が届いた（Receive のたびに 1 回）。採点はこれを数える。Requested のときは Closed の後に出る。</summary>
        event Action<Delivered> Received;
    }
}
