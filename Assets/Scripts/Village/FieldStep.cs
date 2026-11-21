using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールドの戻り方の 1 段（ステップ）です。<see cref="FieldStages"/> の子に置き、Hierarchy の並び順がステップの順番になります。
    /// </summary>
    /// <remarks>
    /// 段に番号はありません。足す・挟む・消す・並べ替えるは、Hierarchy の操作だけで済みます。
    /// ステップが済むと、この物の子が出て、<c>hideWhenDone</c> の物が消えます。吹き出しは、<c>helped</c> の 1 人目の頭の上に出ます。
    /// 子の物に自分のスクリプトを付ければ、出たとき（OnEnable）に動き出せます。フィールドごとの演出は、そこに書きます。
    /// </remarks>
    public sealed class FieldStep : MonoBehaviour
    {
        [Tooltip("欲しい物の FoodId（食べ物のデータの ID）。空なら欲しい物は出さない（開発中の右クリックメニューでだけ済ませられる）")]
        [SerializeField] string food;

        [Tooltip("このステップで困りごとが解ける人。1 人目の頭の上に吹き出しを出す。全部のステップが済むと元気になる")]
        [SerializeField] VillagerMood[] helped = System.Array.Empty<VillagerMood>();

        [Tooltip("このステップが済むと消える物")]
        [SerializeField] GameObject[] hideWhenDone = System.Array.Empty<GameObject>();

        [Tooltip("子の物が出るとき、小さい所から「ぽんっ」と大きくする。物を置くだけで効く。出方を自分で作る物には、SkipPopIn を付ける。全部やめるなら外す")]
        [SerializeField] bool popIn = true;

        [Tooltip("このステップが済んだとき（音・光など）。Inspector からつなぐ")]
        [SerializeField] UnityEvent onDone = new UnityEvent();

        public string Food { get => food; set => food = value; }
        public IReadOnlyList<VillagerMood> Helped => helped;
        public IReadOnlyList<GameObject> HideWhenDone => hideWhenDone;

        /// <summary>吹き出しを出す場所です。1 人目の頭の上の印、その人、この物の順に探します。</summary>
        public Transform Anchor
        {
            get
            {
                var first = helped.Length > 0 ? helped[0] : null;
                if (first == null) return transform;
                return first.WorryMark != null ? first.WorryMark.transform : first.transform;
            }
        }

        public void SetHelped(params VillagerMood[] villagers) => helped = villagers;

        public void SetHideWhenDone(params GameObject[] objects) => hideWhenDone = objects;

        /// <summary>子の物を出すか、消します。</summary>
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
