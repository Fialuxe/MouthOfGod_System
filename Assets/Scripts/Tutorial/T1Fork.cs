using System;
using System.Collections;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// チュートリアル 1「フォークの使い方」（シナリオブック S3）。雲を刺す → 口に含む → 食感が切り替わる → ボタンで決定する、の順に進める。
    /// </summary>
    /// <remarks>
    /// 流れ:
    /// 0. （Stand Field が空でなければ）足あとの印を出し、その場所に立つのを待つ。時間がたったら、立たなくても進む（ブースではキーを打てないため）。
    /// 1. 雲を光らせ、刺すのを待つ。Hint 秒たっても刺せなければ、光る雲を近くへ寄せる。
    /// 2. 口に含むのを待つ。Hint 秒たったら、巫女がくり返す。Alert 秒たったら、スタッフに知らせる印を出す。
    /// 3. 食感が Texture Changes 回切り替わるのを待つ。
    /// 4. ボタンを光らせ、決定するのを待つ。Hint 秒たったら、ボタンを強く光らせる。
    /// 5. 雲とボタンの光を消し、"Good." と言う。どの段もあきらめずに終わったときだけ、<c>fork-learned</c> を覚える。
    /// どの待ちにも Give Up 秒（0 なら待ち続ける）があり、過ぎたら次の段へ進む。体験が止まらないようにするため。
    ///
    /// 出す合図（名前は Inspector で変えられる。空にすると出さない）:
    /// - <c>&lt;Stand Field&gt;.footprint</c>: 立つ場所の足あとの印を出す
    /// - <c>cloud.glow</c>: 雲を 1 つ光らせる（CloudCues が受ける）
    /// - <c>cloud.approach</c>: 光る雲を体験者の近くへ寄せる（CloudCues が受ける）
    /// - <c>cloud.calm</c>: 雲の光を消し、寄せた雲を戻す（CloudCues が受ける）
    /// - <c>staff.alert</c>: スタッフに、体験者が困っていると知らせる
    /// - <c>device.button.glow</c> / <c>device.button.glow-strong</c> / <c>device.button.glow-off</c>: ボタンの位置を光らせる・強く光らせる・消す
    /// - <c>miko.say.&lt;id&gt;</c>: 巫女のセリフ（下の表。文はシーンの LineBook に書く）
    ///
    /// セリフ（id: 英語。日本語は参考訳）:
    /// - <c>t1-pierce</c>: "Take the fork in your hand. Pierce that glowing cloud."（手のフォークで、あの光る雲を刺してみよ。）
    /// - <c>t1-mouth</c>: "Now bring it to your mouth."（では、それを口に含むのじゃ。）
    /// - <c>t1-mouth-again</c>: "Bring it to your mouth."（口に含むのじゃ。）
    /// - <c>t1-changing</c>: "Keep it in your mouth. Feel it? The texture keeps changing."（含んだままでおれ。味わいが移ろうであろう？）
    /// - <c>t1-button</c>: "When it feels right, press the button."（これだと思うたら、ボタンを押すのじゃ。）
    /// - <c>t1-good</c>: "Good."（うむ、よいぞ。）
    /// </remarks>
    public sealed class T1Fork : TutorialScript
    {
        const string LearnedKey = "fork-learned"; // docs/progress-keys.md

        public override PhaseId Phase => new PhaseId("fork");

        [Header("0. 立つ場所")]
        [Tooltip("最初に立ってもらうフィールドの名前。空なら、この段を飛ばす")]
        [SerializeField] string standField = "village";

        [Tooltip("立つ場所の印の合図の後ろ半分。Stand Field のあとに付けて出す（village.footprint）。空なら出さない")]
        [SerializeField] string standCueSuffix = ".footprint";

        [Tooltip("立つ場所を案内するセリフの id。空なら言わない")]
        [SerializeField] string standLine = "";

        [Tooltip("この秒数たっても立たなければ、そのまま進む。0 なら立つまで待ち続ける")]
        [SerializeField, Min(0f)] float standGiveUpSeconds = 15f;

        [Header("1. 雲を刺す")]
        [Tooltip("雲を 1 つ光らせる合図。空なら出さない")]
        [SerializeField] string pierceGlowCue = "cloud.glow";

        [Tooltip("刺すように言うセリフの id。Take the fork in your hand. Pierce that glowing cloud.")]
        [SerializeField] string pierceLine = "t1-pierce";

        [Tooltip("この秒数たっても刺せなければ、ヒントの合図を出す。0 ならヒントなし")]
        [SerializeField, Min(0f)] float pierceHintSeconds = 10f;

        [Tooltip("刺せないときのヒントの合図（光る雲を近くへ寄せる）。空なら出さない")]
        [SerializeField] string pierceHintCue = "cloud.approach";

        [Tooltip("この秒数たっても刺せなければ、次へ進む。0 なら待ち続ける")]
        [SerializeField, Min(0f)] float pierceGiveUpSeconds = 45f;

        [Header("2. 口に含む")]
        [Tooltip("口に含むように言うセリフの id。Now bring it to your mouth.")]
        [SerializeField] string mouthLine = "t1-mouth";

        [Tooltip("この秒数たっても含まなければ、もう一度言う。0 ならくり返さない")]
        [SerializeField, Min(0f)] float mouthHintSeconds = 10f;

        [Tooltip("くり返すセリフの id。Bring it to your mouth.")]
        [SerializeField] string mouthHintLine = "t1-mouth-again";

        [Tooltip("この秒数たっても含まなければ、スタッフに知らせる合図を出す（待ち始めからの秒数）。0 なら出さない")]
        [SerializeField, Min(0f)] float mouthAlertSeconds = 20f;

        [Tooltip("スタッフに知らせる合図。空なら出さない")]
        [SerializeField] string mouthAlertCue = "staff.alert";

        [Tooltip("この秒数たっても含まなければ、次へ進む。0 なら待ち続ける")]
        [SerializeField, Min(0f)] float mouthGiveUpSeconds = 45f;

        [Header("3. 食感が切り替わる")]
        [Tooltip("含んだままでいるように言うセリフの id。Keep it in your mouth. Feel it? The texture keeps changing.")]
        [SerializeField] string changingLine = "t1-changing";

        [Tooltip("食感が何回切り替わったら進むか")]
        [SerializeField, Min(1)] int textureChanges = 3;

        [Tooltip("この秒数たっても切り替わりきらなければ、次へ進む。0 なら待ち続ける")]
        [SerializeField, Min(0f)] float changingGiveUpSeconds = 30f;

        [Header("4. ボタンで決定する")]
        [Tooltip("ボタンの位置を光らせる合図。空なら出さない")]
        [SerializeField] string buttonGlowCue = "device.button.glow";

        [Tooltip("ボタンを押すように言うセリフの id。When it feels right, press the button.")]
        [SerializeField] string buttonLine = "t1-button";

        [Tooltip("この秒数たっても押さなければ、ヒントの合図を出す。0 ならヒントなし")]
        [SerializeField, Min(0f)] float buttonHintSeconds = 10f;

        [Tooltip("押さないときのヒントの合図（ボタンを強く光らせる）。空なら出さない")]
        [SerializeField] string buttonHintCue = "device.button.glow-strong";

        [Tooltip("この秒数たっても押さなければ、次へ進む。0 なら待ち続ける")]
        [SerializeField, Min(0f)] float buttonGiveUpSeconds = 45f;

        [Header("5. 終わり")]
        [Tooltip("雲の光を消し、寄せた雲を戻す合図。空なら出さない")]
        [SerializeField] string calmCue = "cloud.calm";

        [Tooltip("ボタンの光を消す合図。空なら出さない")]
        [SerializeField] string buttonOffCue = "device.button.glow-off";

        [Tooltip("ほめるセリフの id。Good.")]
        [SerializeField] string goodLine = "t1-good";

        [Tooltip("ほめたあと、次のチュートリアルへ進むまでの秒数（セリフを聞き終わるまで）")]
        [SerializeField, Min(0f)] float endHoldSeconds = 2f;

        [Header("間")]
        [Tooltip("段と段のあいだの秒数。できた直後に次のセリフを重ねないため")]
        [SerializeField, Min(0f)] float beatPauseSeconds = 0.5f;

        bool _gaveUp;

        public override IEnumerator Play()
        {
            _gaveUp = false;

            // 0. 立つ場所
            if (!string.IsNullOrEmpty(standField))
            {
                if (!string.IsNullOrEmpty(standCueSuffix)) Cue(standField + standCueSuffix);
                Say(standLine);
                StandWait stand = WaitForStanding(standField, standGiveUpSeconds);
                yield return stand;
                if (stand.TimedOut) Debug.LogWarning("[T1Fork] " + standField + " の前に立たないまま、進みます。");
                yield return Pause();
            }

            // 1. 雲を刺す
            Cue(pierceGlowCue);
            Say(pierceLine);
            IEnumerator step = WaitWithHints("刺す", t => WaitForPierce(t), pierceGiveUpSeconds,
                new Hint(pierceHintSeconds, () => Cue(pierceHintCue)));
            while (step.MoveNext()) yield return step.Current;
            yield return Pause();

            // 2. 口に含む
            Say(mouthLine);
            step = WaitWithHints("口に含む", t => WaitForMouth(t), mouthGiveUpSeconds,
                new Hint(mouthHintSeconds, () => Say(mouthHintLine)),
                new Hint(mouthAlertSeconds, () => Cue(mouthAlertCue)));
            while (step.MoveNext()) yield return step.Current;
            yield return Pause();

            // 3. 食感が切り替わる
            Say(changingLine);
            step = WaitWithHints("食感の切り替わり", t => WaitForTextureChanges(textureChanges, t), changingGiveUpSeconds);
            while (step.MoveNext()) yield return step.Current;
            yield return Pause();

            // 4. ボタンで決定する
            Cue(buttonGlowCue);
            Say(buttonLine);
            step = WaitWithHints("決定", t => WaitForConfirm(t), buttonGiveUpSeconds,
                new Hint(buttonHintSeconds, () => Cue(buttonHintCue)));
            while (step.MoveNext()) yield return step.Current;

            // 5. 終わり
            Cue(calmCue);
            Cue(buttonOffCue);
            Say(goodLine);
            if (!_gaveUp) Memory.Set(LearnedKey); // あきらめて進んだときは、覚えたことにしない（T2 で説明を短くしない）
            if (endHoldSeconds > 0f) yield return new WaitForSeconds(endHoldSeconds);
        }

        // 時間で出すヒント。At は、待ち始めからの秒数。0 なら出さない。
        readonly struct Hint
        {
            public Hint(float at, Action action)
            {
                At = at;
                Action = action;
            }

            public float At { get; }
            public Action Action { get; }
        }

        // 待ちに、ヒントとあきらめる時間を付ける。begin は、時間切れの秒数（0 なら無し）を受けて待ちを作る。ヒントは、早い順に渡す。
        IEnumerator WaitWithHints(string what, Func<float, TutorialWait> begin, float giveUpSeconds, params Hint[] hints)
        {
            float elapsed = 0f;
            foreach (Hint hint in hints)
            {
                if (!HintComesFirst(hint.At, elapsed, giveUpSeconds)) continue;
                TutorialWait untilHint = begin(hint.At - elapsed);
                yield return untilHint;
                if (!untilHint.TimedOut) yield break;
                elapsed = hint.At;
                hint.Action?.Invoke();
            }

            TutorialWait last = begin(RemainingSeconds(giveUpSeconds, elapsed));
            yield return last;
            if (!last.TimedOut) yield break;
            _gaveUp = true;
            Debug.LogWarning("[T1Fork] " + what + "を待つのをあきらめて、次へ進みます。");
        }

        /// <summary>at 秒のヒントを、いま（elapsed 秒）から出す番か。0 以下のヒントと、あきらめる時間より後のヒントは出さない。</summary>
        internal static bool HintComesFirst(float at, float elapsed, float giveUpSeconds)
            => at > 0f && at > elapsed && (giveUpSeconds <= 0f || at < giveUpSeconds);

        /// <summary>elapsed 秒待ったあと、あきらめるまでの残りの秒数。待ちの時間切れに渡す。0 なら待ち続ける。残りがなくても、0（待ち続ける）にはしない。</summary>
        internal static float RemainingSeconds(float giveUpSeconds, float elapsed)
            => giveUpSeconds <= 0f ? 0f : Mathf.Max(0.01f, giveUpSeconds - elapsed);

        object Pause() => beatPauseSeconds > 0f ? new WaitForSeconds(beatPauseSeconds) : null;
    }
}
