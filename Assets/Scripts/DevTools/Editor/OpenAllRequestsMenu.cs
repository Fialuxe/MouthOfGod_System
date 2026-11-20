using System.Collections.Generic;
using MouthOfGod.Core;
using MouthOfGod.Tutorial;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.DevTools.Editor
{
    /// <summary>
    /// Play 中に使うメニュー。チュートリアルが動いていなくて、まだ出ていない欲しい物（吹き出し）があれば、すべてのフィールドで出せるだけ出す。
    /// - チュートリアルが動いている間は、何もしない（チュートリアルの流れを崩さないため）。終わった後・Skip した後・Stop で止めた後・始める前は、出す。
    /// - 1 つのフィールドに同時に出せる数は、そのフィールドの Max Open Requests まで。Main では、どのフィールドもステップの数にそろえてある。
    /// - 試験用の OpenAllRequestsAfterTutorial（Tutorial/Sandbox）は、チュートリアルが終わったときに自動で同じことをする。
    ///   こちらは、好きなときに手で押す（Skip・Stop の後や、届けて減った後に出し直すとき）。
    /// </summary>
    public static class OpenAllRequestsMenu
    {
        const string MenuPath = "MouthOfGod/Dev/Open All Requests (Play mode, after tutorial)";

        [MenuItem(MenuPath)]
        public static void OpenAll()
        {
            var runner = Object.FindFirstObjectByType<TutorialRunner>();
            if (runner != null && runner.IsRunning)
            {
                Debug.LogWarning("[OpenAllRequests] チュートリアルが動いています。終わるか、Skip・Stop してから使ってください。");
                return;
            }

            var fields = new List<IFieldRequests>(Fields.All);
            if (fields.Count == 0)
            {
                Debug.LogWarning("[OpenAllRequests] フィールドが 1 つも登録されていません。");
                return;
            }

            int total = 0;
            foreach (IFieldRequests field in fields)
            {
                int opened = 0;
                while (field.CanOpen && field.Open()) opened++;
                total += opened;
                if (opened > 0) Debug.Log($"[OpenAllRequests] {field.Field}: 欲しい物を {opened} つ出しました。");
            }

            if (total == 0) Debug.Log("[OpenAllRequests] 出せる欲しい物は、もうすべて出ています（すっかり戻ったフィールドには出ません）。");
        }

        [MenuItem(MenuPath, true)]
        static bool CanOpenAll() => EditorApplication.isPlaying;
    }
}
