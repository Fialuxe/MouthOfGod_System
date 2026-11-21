using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールドの戻り方の 1 段（ステップ）です。
    /// <see cref="FieldStages"/> の子に置きます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Hierarchy の並び順が、ステップの順番になります。段に番号はありません。
    /// 足す・挟む・消す・並べ替えるは、Hierarchy の操作だけで済みます。
    /// </para>
    /// <para>
    /// ステップが済むと、この物の子が出て、<c>hideWhenDone</c> の物が消えます。
    /// 吹き出しは、<c>helped</c> の 1 人目の頭の上に出ます。
    /// </para>
    /// <para>
    /// 子の物に自分のスクリプトを付ければ、出たときの OnEnable で動き出せます。
    /// フィールドごとの演出は、そこに書きます。
    /// </para>
    /// </remarks>
    public sealed class FieldStep : MonoBehaviour
    {
        [Tooltip("このステップで欲しい物です（食べ物の Prefab）。空なら欲しい物を出しません。FieldStages の右クリックの Complete Next Step なら、空のステップも済ませられます。")]
        [SerializeField] GameObject wantedFood;

        [Tooltip("このステップで困りごとが解ける人です。1 人目の頭の上に吹き出しを出します。その人の名前があるステップが全部済むと、元気になります。")]
        [SerializeField] VillagerMood[] helped = System.Array.Empty<VillagerMood>();

        [Tooltip("このステップが済むと消える物です。ほかのステップで出る物でも、こちらが優先されて消えます。")]
        [SerializeField] GameObject[] hideWhenDone = System.Array.Empty<GameObject>();

        [Tooltip("子の物が出るとき、小さい所から「ぽんっ」と大きくします。物を置くだけで効きます。出方を自分で作る物には、SkipPopIn を付けます。全部やめるなら外します。")]
        [SerializeField] bool popIn = true;

        [Tooltip("このステップが済んだときに呼ぶものです（音・光など）。Inspector からつなぎます。")]
        [SerializeField] UnityEvent onDone = new UnityEvent();

        /// <summary>このステップで欲しい物です。</summary>
        /// <value>食べ物の Prefab です。<see langword="null"/> なら、欲しい物を出しません。</value>
        public GameObject Food => wantedFood;
        public IReadOnlyList<VillagerMood> Helped => helped;
        public IReadOnlyList<GameObject> HideWhenDone => hideWhenDone;

        /// <summary>吹き出しを出す場所です。</summary>
        /// <value>
        /// 1 人目の頭の上の印、その人、この物の順に探します。
        /// <see langword="null"/> にはなりません。
        /// </value>
        public Transform Anchor
        {
            get
            {
                var first = helped.Length > 0 ? helped[0] : null;
                if (first == null) return transform;
                return first.WorryMark != null ? first.WorryMark.transform : first.transform;
            }
        }

        /// <summary>助かる人を差し替えます。</summary>
        /// <param name="villagers">新しい助かる人です。渡した配列をそのまま持ちます。</param>
        public void SetHelped(params VillagerMood[] villagers) => helped = villagers;

        /// <summary>済んだときに消える物を差し替えます。</summary>
        /// <param name="objects">新しく消える物です。渡した配列をそのまま持ちます。</param>
        public void SetHideWhenDone(params GameObject[] objects) => hideWhenDone = objects;

        /// <summary>子の物を出すか、消します。</summary>
        /// <param name="done"><see langword="true"/> なら子を出し、<see langword="false"/> なら消します。</param>
        /// <param name="celebrate">
        /// <see langword="true"/> なら、新しく出た子を <see cref="PopInEffect"/> で大きくします。
        /// Play 中で、Pop In が有効で、<see cref="SkipPopIn"/> が無い子だけです。
        /// </param>
        internal void ShowChildren(bool done, bool celebrate = false)
        {
            foreach (Transform child in transform)
            {
                var go = child.gameObject;
                var wasActive = go.activeSelf;
                go.SetActive(done);
                if (done && !wasActive && celebrate && popIn && Application.isPlaying && !go.TryGetComponent<SkipPopIn>(out _))
                    go.AddComponent<PopInEffect>();
            }
        }

        internal void RaiseDone() => onDone.Invoke();
    }
}
