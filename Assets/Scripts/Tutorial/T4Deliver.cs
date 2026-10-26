using System;
using System.Collections;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// チュートリアル 4「届ける」（シナリオブック S6）。届け先は、体験者が立っている場所で決まることを覚えてもらう。
    /// 欲しがっているフィールド（既定は海）の前まで歩き、そこで欲しい物（既定は魚）を生み出すと終わる。
    /// </summary>
    /// <remarks>
    /// 流れ:
    /// 1. 準備。目当てのフィールドの欲しい物が出ていなければ、Open(食べ物) で出す（チュートリアル 3 を外しても、これだけで動くように）。
    ///    フィールドがシーンに無ければ、警告を出して、このチュートリアルを終える（止まらない）。
    /// 2. フィールドを光らせ、足あとの印を出し、巫女をついて来させて「海の前まで歩き、そこで魚を生み出す」と言う。
    ///    その後は、どこかに物が届くのを待つ。目当てのフィールドに、欲しがっていた物が届いたら成功。
    ///    - 目当ての前に立ったら、足あとの印を消し、「ここで生み出す」と言う。
    ///    - よそのフィールドに届いたら、「ここではない」と言う。足あとの印をもう一度出す。
    ///    - 目当てのフィールドに、違う物が届いたら、指示をもう一度言う。
    ///    - どちらの場合も、届いたフィールドは自分でがっかりする（DeliveryFx）。届いた物は霞のように消える（DeliveryZone）。
    ///    - しばらく何も届かなければ、指示をもう一度言う。
    ///    - あきらめる時間を過ぎたら、成功しないまま 4 へ進む（ブースを止めない）。
    /// 3. 成功したら、「見事じゃ」と言う。フィールドは自分で喜ぶ（ステップが済み、DeliveryFx が粒と音を出す）。
    /// 4. 光と足あとの印を消し、巫女をとどまらせて、締めのセリフを言う。成功したときだけ、<c>delivered-by-standing</c> を覚える。
    ///
    /// 出す合図（フィールド名は Inspector の Field。接尾辞を空にした合図は出さない）:
    /// - <c>&lt;field&gt;.glow</c> / <c>&lt;field&gt;.glow-off</c>: 目当てのフィールドを光らせる / 光を消す。
    /// - <c>&lt;field&gt;.footprint</c> / <c>&lt;field&gt;.footprint-off</c>: 目当てのフィールドの前の床に、足あとの印を出す / 消す。
    /// - <c>&lt;field&gt;.cheer</c> / <c>&lt;届いたフィールド&gt;.shake-head</c>: 既定では出さない。届いたときの反応（喜ぶ・がっかりする）は、
    ///   フィールドの DeliveryFx が、チュートリアルでも本編でも同じように出すため。演出を足したいときだけ、接尾辞を入れる。
    /// - <c>miko.follow</c> / <c>miko.stay</c>: 巫女が体験者についてくる / その場にとどまる。
    ///
    /// セリフ（<c>miko.say.&lt;id&gt;</c>。中身はシーンの Miko の LineBook に書く）:
    /// - <c>t4-walk</c>: "Walk to the sea, and create the fish there."
    /// - <c>t4-here</c>: "Now, create it here."
    /// - <c>t4-not-here</c>: "Not here — go to the place that glows."
    /// - <c>t4-splendid</c>: "Splendid!"
    /// - <c>t4-true-god</c>: "Find what each place needs, go there, and create it. Now you are a true god."
    /// </remarks>
    public sealed class T4Deliver : TutorialScript
    {
        // 警告の出し先。テストで差し替える。
        internal static Action<string> ReportWarning = Debug.LogWarning;

        const string MemoryDelivered = "delivered-by-standing";

        [Header("届け先")]
        [Tooltip("届けてもらうフィールドの ID（village / mountain / sea / grassland）。合図の名前の頭にも使う")]
        [SerializeField] string field = "sea";

        [Tooltip("届けてもらう物の ID。このフィールドが欲しがっていなければ、始めに Open で欲しがらせる")]
        [SerializeField] string food = "fish";

        [Header("セリフ（巫女の LineBook の Id。空なら言わない）")]
        [Tooltip("指示。「Walk to the sea, and create the fish there.」。しばらく届かないときも、くり返す")]
        [SerializeField] string walkLine = "t4-walk";

        [Tooltip("目当てのフィールドの前に立ったとき。「Now, create it here.」")]
        [SerializeField] string hereLine = "t4-here";

        [Tooltip("よそのフィールドに届いたとき。「Not here — go to the place that glows.」")]
        [SerializeField] string notHereLine = "t4-not-here";

        [Tooltip("目当てのフィールドに、違う物が届いたとき。既定は指示のくり返し")]
        [SerializeField] string wrongFoodLine = "t4-walk";

        [Tooltip("正しく届いたとき。「Splendid!」")]
        [SerializeField] string splendidLine = "t4-splendid";

        [Tooltip("締め。「Find what each place needs, go there, and create it. Now you are a true god.」")]
        [SerializeField] string trueGodLine = "t4-true-god";

        [Header("合図（フィールドの ID のあとに付ける接尾辞。空なら出さない）")]
        [Tooltip("目当てのフィールドを光らせる（<field>.glow）")]
        [SerializeField] string glowSuffix = "glow";

        [Tooltip("光を消す（<field>.glow-off）。最後に出す")]
        [SerializeField] string glowOffSuffix = "glow-off";

        [Tooltip("目当てのフィールドの前に、足あとの印を出す（<field>.footprint）")]
        [SerializeField] string footprintSuffix = "footprint";

        [Tooltip("足あとの印を消す（<field>.footprint-off）。前に立ったときと、最後に出す")]
        [SerializeField] string footprintOffSuffix = "footprint-off";

        [Tooltip("正しく届いたフィールドに出す合図（<field>.cheer）。空なら出さない。届いたときの反応は、フィールドの DeliveryFx が出すので、ふつうは空のまま")]
        [SerializeField] string cheerSuffix = "";

        [Tooltip("違う場所・違う物が届いたフィールドに出す合図（<届いたフィールド>.shake-head）。空なら出さない。ふつうは空のまま（DeliveryFx が反応する）")]
        [SerializeField] string shakeHeadSuffix = "";

        [Header("巫女の合図（空なら出さない）")]
        [Tooltip("指示を出すときに、巫女を体験者についてこさせる")]
        [SerializeField] string mikoFollowCue = "miko.follow";

        [Tooltip("締めのセリフの前に、巫女をその場にとどまらせる")]
        [SerializeField] string mikoStayCue = "miko.stay";

        [Header("時間（秒）")]
        [Tooltip("この秒数のあいだ何も届かなければ、指示をもう一度言う。0 なら言わない")]
        [SerializeField, Min(0f)] float hintSeconds = 15f;

        [Tooltip("この秒数たっても正しく届かなければ、あきらめて締めへ進む。0 ならいつまでも待つ")]
        [SerializeField, Min(0f)] float giveUpSeconds = 60f;

        [Tooltip("「Splendid!」のあと、締めのセリフまでの間")]
        [SerializeField, Min(0f)] float pauseAfterSplendid = 2f;

        [Tooltip("締めのセリフのあと、次の場面へ進むまでの間")]
        [SerializeField, Min(0f)] float pauseAfterTrueGod = 5f;

        bool _openWarned; // 欲しがらせられなかった警告を、もう出したか
        bool _restarted; // 欲しがらせられず、フィールドをはじめに戻したか

        public override PhaseId Phase => new PhaseId("deliver");

        public override IEnumerator Play()
        {
            var target = new FieldId(field);
            var wanted = new FoodId(food);
            _openWarned = false;
            _restarted = false;

            // 1. 準備
            IFieldRequests requests = Fields.Find(target);
            if (requests == null)
            {
                ReportWarning($"[T4Deliver] フィールド「{field}」がシーンにないので、チュートリアル 4 を飛ばします（Field の ID と、フィールドが Fields に登録されているかを確かめる）。");
                yield break;
            }
            EnsureOpen(requests, wanted);

            // 2. 目当ての場所へ歩いて、生み出す
            Cue(FieldCue(field, glowSuffix));
            Cue(FieldCue(field, footprintSuffix));
            Cue(mikoFollowCue);
            Say(walkLine);

            float startedAt = Time.time;
            bool arrived = false;
            bool succeeded = false;
            while (true)
            {
                float remaining = giveUpSeconds > 0f ? giveUpSeconds - (Time.time - startedAt) : 0f;
                if (giveUpSeconds > 0f && remaining <= 0f) break;

                EnsureOpen(requests, wanted); // ほかの所が欲しい物を終えていても、また出す

                DeliveryWait delivery = WaitForDelivery("", WaitSeconds(remaining));
                if (arrived)
                {
                    yield return delivery;
                }
                else
                {
                    // 目当ての前に立ったら、ひと言そえる（もう立っていれば、すぐ終わる）
                    AnyWait any = WaitForAny(delivery, WaitForStanding(field));
                    yield return any;
                    if (any.Index == 1)
                    {
                        arrived = true;
                        Cue(FieldCue(field, footprintOffSuffix));
                        Say(hereLine);
                        continue;
                    }
                }

                if (delivery.TimedOut)
                {
                    bool gaveUp = giveUpSeconds > 0f && Time.time - startedAt >= giveUpSeconds;
                    if (gaveUp) break;
                    Say(walkLine); // しばらく届かないので、指示をくり返す
                    continue;
                }
                if (!delivery.Result.HasValue) continue; // 取り消された（ふつうは起きない）

                Delivered delivered = delivery.Result.Value;
                switch (Judge(delivered, target))
                {
                    case Outcome.Success:
                        succeeded = true;
                        break;
                    case Outcome.WrongFood:
                        Cue(FieldCue(delivered.Field.ToString(), shakeHeadSuffix));
                        Say(wrongFoodLine);
                        continue;
                    default: // 違う場所
                        Cue(FieldCue(delivered.Field.ToString(), shakeHeadSuffix));
                        Say(notHereLine);
                        Cue(FieldCue(field, footprintSuffix)); // 足あとの印をもう一度出し、着いたらまたひと言そえる
                        arrived = false;
                        continue;
                }
                break;
            }

            // 3. 喜ぶ
            if (succeeded)
            {
                Cue(FieldCue(field, cheerSuffix));
                Say(splendidLine);
                if (pauseAfterSplendid > 0f) yield return new WaitForSeconds(pauseAfterSplendid);
            }

            // 4. 締め
            Cue(FieldCue(field, footprintOffSuffix));
            Cue(FieldCue(field, glowOffSuffix));
            Cue(mikoStayCue);
            Say(trueGodLine);
            if (succeeded) Memory.Set(MemoryDelivered);
            if (pauseAfterTrueGod > 0f) yield return new WaitForSeconds(pauseAfterTrueGod);
        }

        /// <summary>届いた物の判定。</summary>
        internal enum Outcome
        {
            /// <summary>目当てのフィールドに、欲しがっていた物が届いた。</summary>
            Success,
            /// <summary>目当てのフィールドに、違う物が届いた。</summary>
            WrongFood,
            /// <summary>よそのフィールドに届いた。</summary>
            WrongPlace,
        }

        /// <summary>届いた物が、目当てのフィールドに正しく届いたか。</summary>
        internal static Outcome Judge(Delivered delivered, FieldId target)
        {
            if (delivered.Field != target) return Outcome.WrongPlace;
            return delivered.WasRequested ? Outcome.Success : Outcome.WrongFood;
        }

        /// <summary>フィールドの合図の名前（<c>sea.glow</c>）。フィールドか接尾辞が空なら、空（合図を出さない）。</summary>
        internal static string FieldCue(string fieldId, string suffix)
        {
            if (string.IsNullOrEmpty(fieldId) || string.IsNullOrEmpty(suffix)) return "";
            return fieldId + "." + suffix;
        }

        // 1 回の待ちの長さ。指示をくり返す時間と、あきらめるまでの残りの、短いほう。0 なら時間切れなし。
        float WaitSeconds(float remaining)
        {
            if (giveUpSeconds <= 0f) return hintSeconds;
            if (hintSeconds <= 0f) return remaining;
            return Mathf.Min(hintSeconds, remaining);
        }

        // 目当ての物を欲しがっていなければ、欲しがらせる。出せないときは警告を 1 回だけ出す（体験は止めない）。
        void EnsureOpen(IFieldRequests requests, FoodId wanted)
        {
            if (requests.IsOpen || requests.Open(wanted)) return;
            // もう届けてしまって、欲しがらせる枠が無い（T3 で、海の前に立ったまま魚を生んだときなど）。1 回だけ、はじめに戻してやり直す。
            if (!_restarted)
            {
                _restarted = true;
                requests.Restart();
                if (requests.Open(wanted)) return;
            }
            if (_openWarned) return;
            _openWarned = true;
            ReportWarning($"[T4Deliver] フィールド「{field}」に「{food}」を欲しがらせられませんでした（そのフィールドのデータに、この物のステップがあるかを確かめる）。");
        }
    }
}
