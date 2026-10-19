using System;
using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 合図（<see cref="CueRequested"/>）の置き場。Tutorial が <see cref="Request"/> し、Village や UI が <see cref="Requested"/> を受けて、見せ方を決める。
    /// </summary>
    /// <remarks>
    /// - 合図の名前（<see cref="CueId"/>）は、フィールド名から始める（例: <c>sea.glow</c>、<c>village.cheer</c>）。決まりは docs/requests.md。
    /// - 出す側は、誰が受けるかを知らない。受ける側は、合図の見せ方を自由に決める。受ける側がいなければ、何も起きない。
    /// - 受ける側は OnEnable で購読し、OnDisable で外す。
    /// - static なので、Play のたびに空にする。すべてメインスレッドから呼ぶ。
    /// - この形を変えるときは、使う側すべてに効くので、別の PR にする。
    /// </remarks>
    public static class Cues
    {
        // 警告と、購読先の例外の出し先。テストで差し替える。
        internal static Action<string> ReportWarning = Debug.LogWarning;
        internal static Action<Exception> ReportException = Debug.LogException;

        /// <summary>合図が出た。</summary>
        public static event Action<CueRequested> Requested;

        /// <summary>合図を出す。名前が空なら、警告して何もしない。</summary>
        public static void Request(CueId cue)
        {
            if (cue.IsEmpty)
            {
                ReportWarning("[Cues] 名前が空の合図は出せません。");
                return;
            }

            Action<CueRequested> handlers = Requested;
            if (handlers == null) return;
            var request = new CueRequested(cue);
            // 1 つの購読先が例外を投げても、残りには届ける（例外は Console に出す）。
            foreach (Action<CueRequested> handler in handlers.GetInvocationList())
            {
                try { handler(request); }
                catch (Exception e) { ReportException(e); }
            }
        }

        // Play のたびに空にする（ドメインのリロードを切っていても、前回の購読を残さない）。テストからも呼ぶ。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void Reset()
        {
            Requested = null;
        }
    }
}
