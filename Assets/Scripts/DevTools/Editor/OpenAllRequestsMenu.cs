using System.Collections.Generic;
using MouthOfGod.Core;
using MouthOfGod.Tutorial;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.DevTools.Editor
{
    /// <summary>
    /// Play 中に使うメニュー。チュートリアルが終わっていて、まだ出ていない欲しい物（吹き出し）があれば、すべてのフィールドで出せるだけ出す。
    /// - チュートリアルの途中・始まる前は、何もしない（チュートリアルの流れを崩さないため）。シーンに TutorialRunner が無ければ、終わっているものとする。
    /// - 1 つのフィールドに同時に出せる数は、そのフィールドの Max Open Requests まで。全部出したいときは、
    ///   「MouthOfGod/Dev/Set Up Main Copy」で、ステップの数にそろえておく。
    /// </summary>
    public static class OpenAllRequestsMenu
    {
        const string MenuPath = "MouthOfGod/Dev/Open All Requests (Play mode, after tutorial)";

        [MenuItem(MenuPath)]
        public static void OpenAll()
        {
            var runner = Object.FindFirstObjectByType<TutorialRunner>();
            if (runner != null && !runner.HasFinished)
            {
                Debug.LogWarning("[OpenAllRequests] チュートリアルが、まだ終わっていません（途中か、始まる前）。終わってから使ってください。");
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
