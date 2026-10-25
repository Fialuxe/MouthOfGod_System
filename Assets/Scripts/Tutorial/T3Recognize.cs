using System;
using System.Collections;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// チュートリアル 3「食感で変わる・欲しい物」（シナリオブック S5。場面 <c>recognize</c>）。
    /// 海の村人が魚を欲しがる吹き出しを見せ、体験者に、魚の食感を思い出して魚を生ませる。
    /// </summary>
    /// <remarks>
    /// 流れ:
    /// 1. 海（<see cref="requestField"/>）に、魚（<see cref="requestFood"/>）の欲しい物を出す（<c>Fields.Find(海).Open(魚)</c>。吹き出しは UI が出す）。
    ///    海を光らせ、巫女が「海の者が困っておる」と言う。フィールドが無い・出せないときは、警告を出して先へ進む。
    /// 2. 霞の影を出す（T2 で影を見ていれば薄く）。雲を光らせ、巫女が「魚はどの歯ざわりであったか」と聞く。
    ///    魚が生まれるまで待つ。違う物が生まれたら「それではない」と言い、何回か外したら影を強める。
    /// 3. 影を消し、巫女が「恵みは立つ場所に落ちる。魚は海のもの」と言う。<c>request-seen</c> を覚える。
    ///
    /// 海の欲しい物は、出したまま終える（チュートリアル 4 で海に届ける）。ここでは Expire しない。
    ///
    /// 出す合図（Inspector で変えられる。空にすると出さない）:
    /// - <c>&lt;requestField&gt;.glow</c>（例 <c>sea.glow</c>）: 欲しがっているフィールドを光らせる。
    /// - <c>shadow.show</c>: 霞の影を、ふつうの濃さで出す（T2 で影を見ていないとき）。
    /// - <c>shadow.faint</c>: 霞の影を、S4 より薄く出す（覚えたこと <c>texture-shadow-seen</c> があるとき）。
    /// - <c>shadow.emphasize</c>: 何回か外したので、影をはっきり出す。
    /// - <c>shadow.hide</c>: 影を消す。
    /// - <c>cloud.glow</c>: 刺す雲を光らせる。
    ///
    /// 巫女のセリフ（合図 <c>miko.say.&lt;id&gt;</c>。中身はシーンの Miko の LineBook に書く）:
    /// - <c>t3-troubled</c>: "Oh, the people by the sea are troubled. They want fish."
    /// - <c>t3-which-feel</c>: "Pierce a new cloud. Which feel was the fish?"
    /// - <c>t3-wrong</c>: "Not that one. Which feel was the fish?"（台本に無いので仮）
    /// - <c>t3-fish-here</c>: "That's the fish — but blessings fall where you stand. Fish belong in the sea."
    ///
    /// 魚・海は仮の割り当て（村 → リンゴ、海 → 魚）。決まったら Inspector で変える。
    /// </remarks>
    public sealed class T3Recognize : TutorialScript
    {
        [Header("欲しい物")]
        [Tooltip("欲しい物を出すフィールドの ID（village / mountain / sea / grassland）。光らせる合図は「<この ID>.glow」になる")]
        [SerializeField] string requestField = "sea";

        [Tooltip("欲しがる食べ物の ID。これが生まれたら先へ進む")]
        [SerializeField] string requestFood = "fish";

        [Header("合図（空にすると出さない）")]
        [Tooltip("フィールドを光らせる合図の、フィールド ID の後ろの部分。「glow」なら「sea.glow」を出す。空にすると出さない")]
        [SerializeField] string fieldGlowSuffix = "glow";

        [Tooltip("影を薄く出す合図。T2 で影を見た（texture-shadow-seen）ときに出す")]
        [SerializeField] string shadowFaintCue = "shadow.faint";

        [Tooltip("影をふつうに出す合図。T2 で影を見ていないときに出す")]
        [SerializeField] string shadowShowCue = "shadow.show";

        [Tooltip("何回か外したときに、影をはっきり出す合図")]
        [SerializeField] string shadowEmphasizeCue = "shadow.emphasize";

        [Tooltip("影を消す合図。魚が生まれたら出す")]
        [SerializeField] string shadowHideCue = "shadow.hide";

        [Tooltip("刺す雲を光らせる合図")]
        [SerializeField] string cloudGlowCue = "cloud.glow";

        [Header("巫女のセリフ（LineBook の id。空にすると言わない）")]
        [Tooltip("Oh, the people by the sea are troubled. They want fish.（おや、海の者が困っておる。魚が欲しいようじゃ。）")]
        [SerializeField] string troubledLine = "t3-troubled";

        [Tooltip("Pierce a new cloud. Which feel was the fish?（新しい雲を刺してみよ。魚は、どの歯ざわりであったかの？）")]
        [SerializeField] string whichFeelLine = "t3-which-feel";

        [Tooltip("違う物が生まれたときのセリフ（仮: Not that one. Which feel was the fish?）")]
        [SerializeField] string wrongLine = "t3-wrong";

        [Tooltip("That's the fish — but blessings fall where you stand. Fish belong in the sea.（うむ、魚じゃ。だが恵みは、そなたの立つ場所に落ちる。魚は海のものじゃ。）")]
        [SerializeField] string fishHereLine = "t3-fish-here";

        [Header("間と時間切れ")]
        [Tooltip("「海の者が困っておる」のあと、次のセリフまで待つ秒数")]
        [SerializeField, Min(0f)] float pauseAfterTroubledSeconds = 3f;

        [Tooltip("魚が生まれたあと、「魚は海のもの」と言うまで待つ秒数（魚が落ちて跳ねるのを見せる間）")]
        [SerializeField, Min(0f)] float pauseBeforeFishHereSeconds = 1.5f;

        [Tooltip("最後のセリフのあと、次のチュートリアルへ進むまで待つ秒数")]
        [SerializeField, Min(0f)] float pauseAtEndSeconds = 4f;

        [Tooltip("この回数だけ違う物が生まれたら、影をはっきり出す（shadow.emphasize）。0 なら出さない")]
        [SerializeField, Min(0)] int missesBeforeEmphasize = 2;

        [Tooltip("魚が生まれるのを待つ秒数。過ぎたら、あきらめて先へ進む（ブースで止まらないため）。0 なら、ずっと待つ")]
        [SerializeField, Min(0f)] float giveUpSeconds = 60f;

        // 警告の出し先。テストで差し替える。
        internal static Action<string> ReportWarning = Debug.LogWarning;

        public override PhaseId Phase => new PhaseId("recognize");

        public override IEnumerator Play()
        {
            // 1. 海に、魚の欲しい物を出す
            OpenRequest();
            if (!string.IsNullOrEmpty(requestField) && !string.IsNullOrEmpty(fieldGlowSuffix))
                Cue(requestField + "." + fieldGlowSuffix);
            Say(troubledLine);
            if (pauseAfterTroubledSeconds > 0f) yield return new WaitForSeconds(pauseAfterTroubledSeconds);

            // 2. 影を出して、魚が生まれるのを待つ
            Cue(Memory != null && Memory.Has("texture-shadow-seen") ? shadowFaintCue : shadowShowCue);
            Cue(cloudGlowCue);
            Say(whichFeelLine);

            var target = new FoodId(requestFood);
            float startedAt = Time.time;
            int misses = 0;
            while (true)
            {
                float remaining = 0f;
                if (giveUpSeconds > 0f)
                {
                    remaining = giveUpSeconds - (Time.time - startedAt);
                    if (remaining <= 0f) break;
                }

                SpawnWait spawn = WaitForSpawn(timeoutSeconds: remaining);
                yield return spawn;
                if (spawn.TimedOut || !spawn.Result.HasValue) break;
                if (target.IsEmpty || spawn.Result.Value.Food == target) break;

                misses++;
                Say(wrongLine);
                if (missesBeforeEmphasize > 0 && misses == missesBeforeEmphasize) Cue(shadowEmphasizeCue);
            }

            // 3. 魚が生まれた（または、あきらめた）。恵みは立つ場所に落ちる、と教える
            Cue(shadowHideCue);
            if (pauseBeforeFishHereSeconds > 0f) yield return new WaitForSeconds(pauseBeforeFishHereSeconds);
            Say(fishHereLine);
            Memory?.Set("request-seen");
            if (pauseAtEndSeconds > 0f) yield return new WaitForSeconds(pauseAtEndSeconds);
            // 海の欲しい物は、出したまま。チュートリアル 4 で海に届ける。
        }

        // 欲しい物を出す。出せなくても、体験は止めない。
        void OpenRequest()
        {
            IFieldRequests field = Fields.Find(new FieldId(requestField));
            if (field == null)
            {
                ReportWarning($"[T3Recognize] フィールド「{requestField}」が見つからないので、欲しい物を出さずに進みます。");
                return;
            }
            if (!field.Open(new FoodId(requestFood)))
                ReportWarning($"[T3Recognize] フィールド「{requestField}」に「{requestFood}」の欲しい物を出せませんでした（もう出ている・上限・その物のステップが無い）。そのまま進みます。");
        }
    }
}
