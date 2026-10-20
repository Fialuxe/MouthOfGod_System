using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// いまの場面（チュートリアルの名前、本編、エンディングなど）の置き場。
    /// 決める側（Tutorial・Game）が <see cref="Set"/> し、使う側は <see cref="Current"/> で読み、<see cref="Changed"/> で変わった知らせを受ける。
    /// </summary>
    /// <remarks>
    /// - 場面は <see cref="PhaseId"/>（文字列）。番号ではなく名前にする（チュートリアルの順番が変わっても、読む側を直さずに済む）。
    ///   名前の一覧は docs/progress-keys.md の「場面（PhaseId の名前）」に書く。
    /// - フィールドは、この置き場を読んで「いまのチュートリアルなら…」と分けてよい。ふつうの反応は <see cref="Cues"/>（合図）で受ける。
    /// - 何も決まっていないときは <see cref="PhaseId.None"/>。
    /// - static なので、Play のたびに空にする。すべてメインスレッドから呼ぶ。
    /// - この形を変えるときは、使う側すべてに効くので、別の PR にする。
    /// </remarks>
    public static class Phases
    {
        // 購読先の例外の出し先。テストで差し替える。
        internal static Action<Exception> ReportException = Debug.LogException;

        /// <summary>いまの場面。決まっていなければ <see cref="PhaseId.None"/>。</summary>
        public static PhaseId Current { get; private set; }

        /// <summary>場面が変わった（新しい場面を運ぶ）。<see cref="Current"/> を更新してから出す。同じ場面を Set しても出ない。</summary>
        public static event Action<PhaseId> Changed;

        /// <summary>いまの場面を決める。いまと同じなら何もしない。</summary>
        public static void Set(PhaseId phase)
        {
            if (phase == Current) return;
            Current = phase;

            Action<PhaseId> handlers = Changed;
            if (handlers == null) return;
            // 1 つの購読先が例外を投げても、残りには届ける（例外は Console に出す）。
            foreach (Action<PhaseId> handler in handlers.GetInvocationList())
            {
                try { handler(phase); }
                catch (Exception e) { ReportException(e); }
            }
        }

        // Play のたびに空にする（ドメインのリロードを切っていても、前回の場面と購読を残さない）。テストからも呼ぶ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            Current = PhaseId.None;
            Changed = null;
        }
    }
}
