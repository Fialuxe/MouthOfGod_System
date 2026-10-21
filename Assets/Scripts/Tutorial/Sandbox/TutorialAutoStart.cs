using System.Collections;
using UnityEngine;

namespace MouthOfGod.Tutorial.Sandbox
{
    /// <summary>Play を始めてしばらくしたら、同じ GameObject の TutorialRunner を動かす。試験用。</summary>
    [RequireComponent(typeof(TutorialRunner))]
    public sealed class TutorialAutoStart : MonoBehaviour
    {
        // 装着してシーンが見えるようになるまで、音声や字幕を出さないための待ち時間。
        [SerializeField] float delaySeconds = 6f;

        IEnumerator Start()
        {
            yield return new WaitForSeconds(delaySeconds);
            GetComponent<TutorialRunner>().Begin();
        }
    }
}
