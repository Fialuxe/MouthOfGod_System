using System.Collections;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Tutorial.Sandbox
{
    /// <summary>
    /// 村だけで、チュートリアルを進める仕組みが動くか確かめる仮のスクリプト。本番の T1〜T4 ができたら消す。
    /// 村の欲しい物を 1 つずつ出し、食べ物が届くのを待つ。3 つ届くか、時間切れで終わる。
    /// </summary>
    public sealed class VillageTutorialProbe : TutorialScript
    {
        [SerializeField] string fieldId = "village";
        [SerializeField] int steps = 3;
        [SerializeField] float timeoutSeconds = 30f;

        public override PhaseId Phase => new PhaseId("sandbox-village");

        public override IEnumerator Play()
        {
            // 村が置かれるまで待つ。
            IFieldRequests field = Fields.Find(new FieldId(fieldId));
            while (field == null)
            {
                yield return null;
                field = Fields.Find(new FieldId(fieldId));
            }

            Cue("sandbox-start");
            Cue("miko.say.welcome");
            for (int i = 0; i < steps; i++)
            {
                if (!field.Open())
                {
                    Debug.LogWarning($"[VillageTutorialProbe] {i + 1} 個目の欲しい物を出せません。");
                    yield break;
                }
                Cue("miko.say.give"); // 巫女が「これせい！」と指さして言う
                // 欲しがっていない物（人参にかぶ、など）が届いても、そのステップは終わらない。欲しい物が届くまで待つ。
                DeliveryWait wait;
                do
                {
                    wait = WaitForDelivery(fieldId, timeoutSeconds);
                    yield return wait;
                    if (wait.TimedOut)
                    {
                        Debug.Log($"[VillageTutorialProbe] {i + 1} 個目は時間切れです。");
                        field.Expire();
                        yield break;
                    }
                    Debug.Log($"[VillageTutorialProbe] {i + 1} 個目の届き: {wait.Result?.Kind}");
                    if (wait.Result?.WasRequested != true) Cue("miko.say.wrong");
                }
                while (wait.Result?.WasRequested != true);
                Cue("miko.say.thanks");
            }
            Cue("sandbox-done");
            Debug.Log("[VillageTutorialProbe] 終わりました。");
        }
    }
}
