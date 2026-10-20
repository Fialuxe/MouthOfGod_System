using System.Collections;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// 人形劇スクリプトの土台。チュートリアル 1 つにつき、これを継いだクラスを 1 つ書く（T1.cs … T4.cs）。
    /// 流れ（合図を出す、待つ、分岐する）をコルーチンで書く。見せ方は、合図を受けるフィールドや UI が決める。
    /// </summary>
    /// <remarks>
    /// - 書くのは「いつ、何の合図を出すか」と「何を待つか」だけ。フィールドの中身は知らない（Core の <see cref="Cues"/> と、各種の知らせだけを使う）。
    /// - そのチュートリアルにしかない物（浮かぶお供え物のリンゴ、影など）は、このクラスの <c>[SerializeField]</c> に持たせてよい。
    /// - <see cref="TutorialRunner"/> の子に置く。Hierarchy の並び順に動く。無効（enabled を外す）にすると、飛ばされる。
    /// - 例外が出たら、このチュートリアルだけをやめて、次へ進む（体験が止まらない）。
    /// </remarks>
    public abstract class TutorialScript : MonoBehaviour
    {
        readonly List<TutorialWait> _waits = new List<TutorialWait>();

        /// <summary>このチュートリアルの場面の名前（<c>fork</c>、<c>spawn</c> など）。動いているあいだ <see cref="Phases.Current"/> になる。番号は使わない。</summary>
        public abstract PhaseId Phase { get; }

        /// <summary>流れを書く。<c>yield return</c> で待つ。</summary>
        public abstract IEnumerator Play();

        /// <summary>前のチュートリアルで覚えたこと。<see cref="TutorialRunner"/> が渡す。</summary>
        protected TutorialMemory Memory { get; private set; }

        internal void Attach(TutorialMemory memory) => Memory = memory;

        /// <summary>合図を出す。見せ方は、受けた側が決める。</summary>
        protected void Cue(string cue) => Cues.Request(new CueId(cue));

        /// <summary>フィールドに物が届くのを待つ。field を空にすると、どこへ届いてもよい。timeoutSeconds が 0 なら、時間切れはない。</summary>
        protected DeliveryWait WaitForDelivery(string field = "", float timeoutSeconds = 0f)
            => Track(new DeliveryWait(new FieldId(field), timeoutSeconds, null));

        /// <summary>デバイスで決定するのを待つ。timeoutSeconds が 0 なら、時間切れはない。</summary>
        protected ConfirmWait WaitForConfirm(float timeoutSeconds = 0f)
            => Track(new ConfirmWait(timeoutSeconds, null));

        /// <summary>渡した待ちのうち、どれかが終わるのを待つ。終わったら、ほかは取り消す。</summary>
        protected AnyWait WaitForAny(params TutorialWait[] waits) => new AnyWait(waits);

        T Track<T>(T wait) where T : TutorialWait
        {
            _waits.Add(wait);
            return wait;
        }

        // このチュートリアルが終わった・やめたときに、待ちが残っていれば聞くのをやめる。
        internal void CancelWaits()
        {
            foreach (TutorialWait wait in _waits.ToArray()) wait.Cancel();
            _waits.Clear();
        }
    }
}
