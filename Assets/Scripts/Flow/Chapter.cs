using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>
    /// 場面 1 つの土台です。プロローグ・チュートリアル・本編・エンディングは、これを継いで <see cref="Run"/> を書きます。
    /// </summary>
    /// <remarks>
    /// <see cref="TutorialHandler"/> の子に並べた順に、上から流れます。
    /// </remarks>
    public abstract class Chapter : MonoBehaviour
    {
        [Tooltip("この秒数を過ぎたら、待つのをあきらめて先へ進みます（GaveUp が true になります）。0 なら、あきらめません。単位は秒です。")]
        [SerializeField, Min(0f)] float giveUpSeconds = 90f;

        float _startedAt;

        /// <summary>場面を流します。終わったら次の場面へ進みます。</summary>
        /// <remarks>
        /// 場面が飛ばされると、待っている所から OperationCanceledException で抜けます。
        /// 後片付けは <see cref="CleanUp"/> に書きます。
        /// </remarks>
        protected abstract Awaitable Run();

        /// <summary>場面が終わったとき、飛ばされたときに呼ばれます。出した物を片付けます。</summary>
        protected virtual void CleanUp() { }

        /// <summary>最初からやり直すときに、すべての場面で呼ばれます。</summary>
        /// <remarks>場面が終わったあとも残している物を消します。結果の表示などです。</remarks>
        protected internal virtual void OnRestart() { }

        /// <summary>この場面を始めてから、giveUpSeconds を過ぎたとき <see langword="true"/> です。</summary>
        protected bool GaveUp => giveUpSeconds > 0f && Time.time - _startedAt >= giveUpSeconds;

        /// <summary>あきらめるまでの残りの秒数です。待つときの timeout に渡します。</summary>
        /// <value>
        /// あきらめないなら 0 です。0 は、待つ側では「無制限」の意味になります。
        /// 時間を過ぎたときは 0.01 秒を返します。0 を返すと、無制限に待ってしまうためです。
        /// </value>
        protected float Remaining => giveUpSeconds > 0f ? Mathf.Max(0.01f, giveUpSeconds - (Time.time - _startedAt)) : 0f;

        /// <summary>場面をまたいで覚えておくことです（例: "fork-learned"）。最初からやり直すと消えます。</summary>
        protected static System.Collections.Generic.HashSet<string> Memory => TutorialHandler.Memory;

        // TutorialHandler から呼びます。例外で抜けても、必ず CleanUp を呼びます。
        internal async Awaitable Play()
        {
            _startedAt = Time.time;
            try { await Run(); }
            finally { CleanUp(); }
        }
    }
}
