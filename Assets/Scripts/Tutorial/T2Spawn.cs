using System.Collections;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// チュートリアル 2「物が生まれる」（シナリオブック S4。場面 <c>spawn</c>）。口に含んだ食感で、口から物が生まれることを見せる。
    /// </summary>
    /// <remarks>
    /// 流れ:
    /// 1. お供え物のリンゴを目の前に浮かべ、雲を光らせ、影を出し始める。雲を刺して、口に含むのを待つ（待ちきれないと、ヒントをくり返す）。
    /// 2. 「リンゴの影を待て」と言い、何かが生まれるのを待つ。目当ての食べ物（既定 <c>apple</c>）なら次へ。
    ///    ほかの物なら「それも恵みじゃ。だが今はリンゴを」と言って、もう一度待つ。決めた回数外したら、影を強く見せる。
    /// 3. お供え物と影を消し、「見よ、恵みが生まれたぞ」と言う。<c>food-born</c> と <c>texture-shadow-seen</c> を覚える。
    ///
    /// 出す合図（Inspector で名前を変えられる。空にすると出さない）:
    /// - <c>offering.show</c> / <c>offering.hide</c>: お供え物のリンゴを見せる・消す。<c>Offering</c> に物を入れれば、このスクリプトも直接出し入れする。
    /// - <c>cloud.glow</c>: 雲を光らせる。
    /// - <c>shadow.show</c> / <c>shadow.emphasize</c> / <c>shadow.hide</c>: 影を出す・強く見せる・消す（UI の FoodShadow が受ける）。
    /// - <c>miko.say.&lt;id&gt;</c>: 巫女のセリフ（<see cref="TutorialScript.Say"/>）。
    ///
    /// セリフ（英文はシーンの Miko の LineBook に書く。ここは既定の id と、そこに入れる文）:
    /// - <c>t2-remember</c>: "Remember the apple the villagers offered? Pierce a cloud and put it in your mouth."
    /// - <c>t2-remember-short</c>: "Remember the apple the villagers offered? Try it again." （<c>fork-learned</c> を覚えているときに使う）
    /// - <c>t2-hint-pierce</c>: "Pierce a cloud with your fork."
    /// - <c>t2-hint-mouth</c>: "Bring it to your mouth."
    /// - <c>t2-wait-apple</c>: "As the feel changes, so does the blessing. Wait for the apple."
    /// - <c>t2-wrong</c>: "That too is a blessing — but now, the apple."
    /// - <c>t2-born</c>: "Look! A blessing is born from your mouth! Remember that feel."
    ///
    /// ブースで止まらないように、あきらめる時間（<c>Give Up Seconds</c>）を過ぎたら、生まれていなくても先へ進む。
    /// そのときは <c>t2-born</c> を言わず、見たことだけを覚える（何か生まれていれば <c>food-born</c>、口に含んでいれば <c>texture-shadow-seen</c>）。
    /// </remarks>
    public sealed class T2Spawn : TutorialScript
    {
        public override PhaseId Phase => new PhaseId("spawn");

        [Header("食べ物")]
        [Tooltip("生んでほしい食べ物の ID。これが生まれたら成功")]
        [SerializeField] string targetFood = "apple";

        [Tooltip("目当て以外を何回生んだら、影を強く見せるか（0 なら強くしない）")]
        [SerializeField, Min(0)] int missesBeforeEmphasize = 3;

        [Header("お供え物")]
        [Tooltip("目の前に浮かべるお供え物のリンゴ。入れれば、始めに表示し、終わりに隠す。空なら合図だけ出す")]
        [SerializeField] GameObject offering;

        [Header("時間（秒）")]
        [Tooltip("雲を刺さないとき、刺すヒントをくり返す間隔（0 ならくり返さない）")]
        [SerializeField, Min(0f)] float pierceHintSeconds = 10f;

        [Tooltip("口に含まないとき、含むヒントをくり返す間隔（0 ならくり返さない）")]
        [SerializeField, Min(0f)] float mouthHintSeconds = 10f;

        [Tooltip("何も生まれないとき、「リンゴの影を待て」をくり返す間隔（0 ならくり返さない）")]
        [SerializeField, Min(0f)] float spawnHintSeconds = 20f;

        [Tooltip("始めてからこの秒数がたったら、生まれていなくても先へ進む（ブースで止まらないように）。0 なら、いつまでも待つ")]
        [SerializeField, Min(0f)] float giveUpSeconds = 90f;

        [Tooltip("目当ての物が生まれてから、「見よ」と言うまでの間（物が落ちるのを見せる）")]
        [SerializeField, Min(0f)] float bornDelaySeconds = 0.5f;

        [Tooltip("最後のセリフのあと、次のチュートリアルへ進むまでの間（セリフを聞き終える時間）")]
        [SerializeField, Min(0f)] float endPauseSeconds = 4f;

        [Header("セリフの id（Miko の LineBook の Id。空なら言わない）")]
        [Tooltip("最初の語り。「供えたリンゴを覚えておるか。雲を刺して、口に含んでみよ」")]
        [SerializeField] string rememberLine = "t2-remember";

        [Tooltip("チュートリアル 1 を済ませた（fork-learned）ときの、短い最初の語り。空なら Remember Line を使う")]
        [SerializeField] string rememberShortLine = "t2-remember-short";

        [Tooltip("雲を刺さないときのヒント。「フォークで雲を刺すのじゃ」")]
        [SerializeField] string pierceHintLine = "t2-hint-pierce";

        [Tooltip("口に含まないときのヒント。「口に含むのじゃ」")]
        [SerializeField] string mouthHintLine = "t2-hint-mouth";

        [Tooltip("口に含んだあと。「歯ざわりが変われば、生まれる物も変わる。リンゴの影が見えたら、そこじゃ」")]
        [SerializeField] string waitAppleLine = "t2-wait-apple";

        [Tooltip("ほかの物が生まれたとき。「それも恵みじゃ。だが今はリンゴを」")]
        [SerializeField] string wrongLine = "t2-wrong";

        [Tooltip("目当ての物が生まれたとき。「見よ、そなたの口から恵みが生まれたぞ！」")]
        [SerializeField] string bornLine = "t2-born";

        [Tooltip("あきらめて先へ進むときのセリフ（空なら言わない）")]
        [SerializeField] string giveUpLine = "";

        [Header("合図の名前（空なら出さない）")]
        [Tooltip("お供え物のリンゴを見せる")]
        [SerializeField] string offeringShowCue = "offering.show";

        [Tooltip("お供え物のリンゴを消す")]
        [SerializeField] string offeringHideCue = "offering.hide";

        [Tooltip("雲を光らせる")]
        [SerializeField] string cloudGlowCue = "cloud.glow";

        [Tooltip("影を出し始める（UI の FoodShadow が受ける）")]
        [SerializeField] string shadowShowCue = "shadow.show";

        [Tooltip("外し続けたとき、影を強く見せる")]
        [SerializeField] string shadowEmphasizeCue = "shadow.emphasize";

        [Tooltip("影を消す")]
        [SerializeField] string shadowHideCue = "shadow.hide";

        float _startedAt;

        public override IEnumerator Play()
        {
            _startedAt = Time.time;
            bool reachedMouth = false;
            bool anythingBorn = false;
            bool targetBorn = false;

            // 1. お供え物を浮かべ、雲を光らせ、影を出し始める。刺して、口に含むのを待つ。
            if (offering != null) offering.SetActive(true);
            Cue(offeringShowCue);
            Cue(cloudGlowCue);
            Cue(shadowShowCue);
            bool useShort = Memory != null && Memory.Has("fork-learned") && !string.IsNullOrEmpty(rememberShortLine);
            Say(useShort ? rememberShortLine : rememberLine);

            while (!GaveUp)
            {
                ForkWait pierce = WaitForPierce(NextTimeout(pierceHintSeconds));
                yield return pierce;
                if (!pierce.TimedOut) break;
                if (!GaveUp) Say(pierceHintLine);
            }

            while (!GaveUp)
            {
                ForkWait mouth = WaitForMouth(NextTimeout(mouthHintSeconds));
                yield return mouth;
                if (!mouth.TimedOut)
                {
                    reachedMouth = true;
                    break;
                }
                if (!GaveUp) Say(mouthHintLine);
            }

            // 2. 何かが生まれるのを待つ。目当てでなければ、もう一度。
            if (!GaveUp) Say(waitAppleLine);
            int misses = 0;
            while (!GaveUp)
            {
                SpawnWait spawn = WaitForSpawn("", NextTimeout(spawnHintSeconds));
                yield return spawn;
                if (spawn.TimedOut || spawn.Result == null)
                {
                    if (!GaveUp) Say(waitAppleLine);
                    continue;
                }

                anythingBorn = true;
                reachedMouth = true; // 生まれたなら、口に含んで影を見ている（刺し直したあとの分も含める）
                if (IsTarget(spawn.Result.Value.Food, targetFood))
                {
                    targetBorn = true;
                    break;
                }

                misses++;
                Say(wrongLine);
                if (ShouldEmphasize(misses, missesBeforeEmphasize)) Cue(shadowEmphasizeCue);
            }

            // 3. お供え物と影を消し、生まれたことを伝える。
            if (targetBorn && bornDelaySeconds > 0f) yield return new WaitForSeconds(bornDelaySeconds);
            if (offering != null) offering.SetActive(false);
            Cue(offeringHideCue);
            Cue(shadowHideCue);
            Say(targetBorn ? bornLine : giveUpLine);

            if (anythingBorn) Memory?.Set("food-born");
            if (reachedMouth) Memory?.Set("texture-shadow-seen");

            if (endPauseSeconds > 0f) yield return new WaitForSeconds(endPauseSeconds);
        }

        // Stop で途中でやめたとき、お供え物と影を消す。
        protected override void OnAborted()
        {
            if (offering != null) offering.SetActive(false);
            Cue(offeringHideCue);
            Cue(shadowHideCue);
        }

        bool GaveUp => giveUpSeconds > 0f && Time.time - _startedAt >= giveUpSeconds;

        float NextTimeout(float hintSeconds) => TimeoutFor(hintSeconds, giveUpSeconds, Time.time - _startedAt);

        /// <summary>
        /// 次の待ちの時間切れ（秒）。ヒントの間隔と、あきらめるまでの残りの、短いほう。どちらも無ければ 0（時間切れなし）。
        /// 残りが 0 以下でも、すぐ終わるように小さな正の数を返す（0 だと「時間切れなし」になってしまうため）。
        /// </summary>
        internal static float TimeoutFor(float hintSeconds, float giveUpSeconds, float elapsedSeconds)
        {
            float hint = hintSeconds > 0f ? hintSeconds : 0f;
            if (giveUpSeconds <= 0f) return hint;
            float remaining = Mathf.Max(0.001f, giveUpSeconds - elapsedSeconds);
            return hint > 0f ? Mathf.Min(hint, remaining) : remaining;
        }

        /// <summary>生まれた物が目当てか。目当てが空なら、何でもよい。</summary>
        internal static bool IsTarget(FoodId born, string target)
            => string.IsNullOrEmpty(target) || born == new FoodId(target);

        /// <summary>この回で影を強くするか。決めた回数に届いたとき 1 回だけ。0 なら強くしない。</summary>
        internal static bool ShouldEmphasize(int misses, int missesBeforeEmphasize)
            => missesBeforeEmphasize > 0 && misses == missesBeforeEmphasize;
    }
}
