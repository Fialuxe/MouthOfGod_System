using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールドの戻り方の 1 段（ステップ）。<see cref="FieldStages"/> の下の <c>Steps</c> に、空のオブジェクトを作って付ける。
    /// Hierarchy の並び順が、ステップの順番（番号はどこにも書かない。足す・挟む・消す・並べ替えるは Hierarchy の操作だけ）。
    /// - このオブジェクトの子: ステップが済むと出る物（芽、羊など）。
    /// - <see cref="hideWhenDone"/>: ステップが済むと消える物（枯れた茎など。どこに置いてあってもよい）。
    /// - <see cref="helped"/>: このステップで困りごとが解ける人。1 人目の頭の上に吹き出しを出す。
    /// 子の物に自分のスクリプトを付ければ、出たとき（OnEnable）に動き出せる。フィールドごとの独自の演出はそこに書く。
    /// </summary>
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

        /// <summary>吹き出しの場所。1 人目の頭の上の印、無ければその人、誰もいなければこのステップ。</summary>
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

        /// <summary>子の物を出す・消す。消す物は <see cref="FieldStages"/> がまとめて扱う（あとのステップの「消す」が勝つように）。</summary>
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
