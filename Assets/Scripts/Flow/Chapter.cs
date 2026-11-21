using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 場面 1 つの土台です。プロローグ・チュートリアル・本編・エンディングは、これを継いで Run を書きます。
    /// TutorialHandler の子に並べた順に、上から流れます。
    /// </summary>
    public abstract class Chapter : MonoBehaviour
    {
        [Tooltip("この秒数を過ぎたら、待つのをあきらめて先へ進みます（GaveUp が true になる）。0 なら、あきらめません")]
        [SerializeField, Min(0f)] float giveUpSeconds = 90f;

        float _startedAt;

        /// <summary>場面を流します。終わったら次の場面へ進みます。</summary>
        protected abstract Awaitable Run();

        /// <summary>場面が終わったとき、飛ばされたときに呼ばれます。出した物を片付けます。</summary>
        protected virtual void CleanUp() { }

        /// <summary>最初からやり直すときに、すべての場面で呼ばれます。場面が終わったあとも残している物（結果の表示など）を消します。</summary>
        protected internal virtual void OnRestart() { }

        /// <summary>この場面を始めてから、giveUpSeconds を過ぎました。</summary>
        protected bool GaveUp => giveUpSeconds > 0f && Time.time - _startedAt >= giveUpSeconds;

        /// <summary>あきらめるまでの残りの秒数です。待つときの timeout に渡します。あきらめないなら 0 です。</summary>
        protected float Remaining => giveUpSeconds > 0f ? Mathf.Max(0.01f, giveUpSeconds - (Time.time - _startedAt)) : 0f;

        /// <summary>場面をまたいで覚えておくことです（例: "fork-learned"）。最初からやり直すと消えます。</summary>
        protected static System.Collections.Generic.HashSet<string> Memory => TutorialHandler.Memory;

        internal async Awaitable Play()
        {
            _startedAt = Time.time;
            try { await Run(); }
            finally { CleanUp(); }
        }
    }
}
