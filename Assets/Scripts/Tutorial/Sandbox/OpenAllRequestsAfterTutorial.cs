using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Tutorial.Sandbox
{
    /// <summary>
    /// チュートリアルが終わったら、すべてのフィールドに、欲しい物をいっぺんに出す仮のスクリプト。吹き出しが出るか、フィールドの設定に抜けがないかを確かめるために使う。
    /// 本編（Game）ができたら消す。
    /// </summary>
    /// <remarks>
    /// - <see cref="TutorialRunner.Finished"/> を受けて、登録されているフィールドごとに、出せるだけ（フィールドのデータの上限まで）出す。
    /// - 出せなかったフィールドは、理由の見当を Console に出す。
    /// </remarks>
    public sealed class OpenAllRequestsAfterTutorial : MonoBehaviour
    {
        [Tooltip("終わりを待つチュートリアル。空なら、シーンの TutorialRunner")]
        [SerializeField] TutorialRunner runner;

        [Tooltip("1 つのフィールドに出す数の上限。0 なら、フィールドが出せるだけ")]
        [SerializeField, Min(0)] int maxPerField;

        void OnEnable()
        {
            if (runner == null) runner = FindFirstObjectByType<TutorialRunner>(FindObjectsInactive.Include);
            if (runner == null)
            {
                Debug.LogWarning("[AfterTutorial] TutorialRunner が見つかりません。");
                return;
            }
            runner.Finished += OnFinished;
        }

        void OnDisable()
        {
            if (runner != null) runner.Finished -= OnFinished;
        }

        [ContextMenu("Open Now")]
        void OnFinished()
        {
            var fields = new System.Collections.Generic.List<IFieldRequests>(Fields.All);
            Debug.Log($"[AfterTutorial] チュートリアルが終わりました。{fields.Count} 個のフィールドに、欲しい物を出します。");
            foreach (IFieldRequests field in fields)
            {
                int opened = 0;
                while ((maxPerField <= 0 || opened < maxPerField) && field.CanOpen && field.Open()) opened++;
                if (opened > 0) Debug.Log($"[AfterTutorial] {field.Field}: 欲しい物を {opened} つ出しました。");
                else Debug.LogWarning($"[AfterTutorial] {field.Field}: 欲しい物を出せませんでした（ステップが無い・欲しい物（Food）が空・もう上限まで出ている・すっかり戻っている、のどれか）。");
            }
        }
    }
}
