using UnityEngine;

namespace MouthOfGod.Tutorial.Sandbox
{
    /// <summary>Play を始めたら、同じ GameObject の TutorialRunner を動かす。試験用。</summary>
    [RequireComponent(typeof(TutorialRunner))]
    public sealed class TutorialAutoStart : MonoBehaviour
    {
        void Start() => GetComponent<TutorialRunner>().Begin();
    }
}
