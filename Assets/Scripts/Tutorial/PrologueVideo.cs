using System.Collections;
using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// プロローグ（シナリオブック S1）。導入の動画を <see cref="VideoScreen"/> で流し、終わったら「立ち上がって、スタッフの指示を聞いて」と伝える。
    /// <see cref="TutorialRunner"/> の最初の子に置く。終わると、次のチュートリアル 1 へ進む。
    /// </summary>
    /// <remarks>
    /// - シーンは切り替えない。同じシーンの中で流すので、XR リグと AprilTag の位置合わせ、村がそのまま残る。
    /// - 動画が無い・読み込めないときは、警告を出してすぐ次へ進む（体験を止めない）。
    /// - 動画は、途中でスキップできる。スキップのキー（既定は K）を押すと、動画をフェードして消し、巫女のセリフへ進む。展示では、スタッフが PC のキーボードで押す。
    /// 出す合図:
    /// - <c>prologue.start</c>: 動画を流し始める前。暗くする・BGM を止めるなどを、受ける側が足す
    /// - <c>prologue.end</c>: 動画が終わった
    /// - <c>miko.say.prologue-stand-up</c>: "Please stand up and follow the booth staff."（立ち上がり、ブース担当者の指示を聞いてください）
    /// </remarks>
    public sealed class PrologueVideo : TutorialScript
    {
        [Tooltip("動画を流す画面。空なら、このオブジェクトか子の VideoScreen")]
        [SerializeField] VideoScreen screen;

        [Tooltip("動画を流し始める前に待つ秒数。Quest をかぶった直後に、目が慣れるまで待つ")]
        [SerializeField, Min(0f)] float delayBeforeSeconds = 1.5f;

        [Tooltip("動画を流す前に出す合図。空なら出さない")]
        [SerializeField] string startCue = "prologue.start";

        [Tooltip("動画が終わったあとに出す合図。空なら出さない")]
        [SerializeField] string endCue = "prologue.end";

        [Tooltip("動画のあとに言わせるセリフの id（巫女の LineBook）。空なら言わない")]
        [SerializeField] string standUpLine = "prologue-stand-up";

        [Tooltip("セリフのあと、次へ進むまでの秒数")]
        [SerializeField, Min(0f)] float holdAfterSeconds = 4f;

        [Tooltip("動画を飛ばすキー。動画を流している間だけ効く。スタッフが PC のキーボードで押す")]
        [SerializeField] InputAction skipAction = new InputAction("SkipVideo", InputActionType.Button, "<Keyboard>/k");

        public override PhaseId Phase => new PhaseId("prologue");

        public override IEnumerator Play()
        {
            if (screen == null) screen = GetComponentInChildren<VideoScreen>(true);

            if (delayBeforeSeconds > 0f) yield return new WaitForSeconds(delayBeforeSeconds);
            Cue(startCue);

            if (screen != null)
            {
                skipAction.Enable();
                screen.Play();
                while (screen.IsBusy)
                {
                    if (skipAction.WasPressedThisFrame()) screen.Skip();
                    yield return null;
                }
                skipAction.Disable();
            }
            else Debug.LogWarning("[PrologueVideo] VideoScreen が無いので、動画を流さずに進みます。");

            Cue(endCue);
            Say(standUpLine);
            if (holdAfterSeconds > 0f) yield return new WaitForSeconds(holdAfterSeconds);
        }

        // Stop で途中でやめたとき、画面を消す。
        protected override void OnAborted()
        {
            skipAction.Disable();
            if (screen != null && screen.IsBusy) screen.Skip();
        }
    }
}
