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
        TutorialHistory _history;
        bool _ownsHistory; // Runner なしで動かした（テストなど）ときは、自分で作って自分で止める

        /// <summary>このチュートリアルの場面の名前（<c>fork</c>、<c>spawn</c> など）。動いているあいだ <see cref="Phases.Current"/> になる。番号は使わない。</summary>
        public abstract PhaseId Phase { get; }

        /// <summary>流れを書く。<c>yield return</c> で待つ。</summary>
        public abstract IEnumerator Play();

        /// <summary>前のチュートリアルで覚えたこと。<see cref="TutorialRunner"/> が渡す。</summary>
        protected TutorialMemory Memory { get; private set; }

        /// <summary>このチュートリアルが始まってから、体験者がしたことの記録。先回りした操作を数えるのに使う。</summary>
        protected TutorialHistory History
        {
            get
            {
                if (_history == null)
                {
                    _history = new TutorialHistory();
                    _ownsHistory = true;
                }
                return _history;
            }
        }

        internal void Attach(TutorialMemory memory, TutorialHistory history = null)
        {
            Memory = memory;
            if (history == null) return;
            if (_ownsHistory) _history?.Dispose();
            _history = history;
            _ownsHistory = false;
        }

        /// <summary>kind の操作が、先回りして（まだ待ちに使われずに）残っているか。先回りした段のセリフを飛ばすときに使う。</summary>
        protected bool AlreadyDone(TutorialEvent kind) => History.Pending(kind) > 0;

        /// <summary>合図を出す。見せ方は、受けた側が決める。</summary>
        protected void Cue(string cue)
        {
            if (!string.IsNullOrEmpty(cue)) Cues.Request(new CueId(cue)); // Inspector で合図を空にしたら、出さない
        }

        /// <summary>フィールドに物が届くのを待つ。field を空にすると、どこへ届いてもよい。timeoutSeconds が 0 なら、時間切れはない。</summary>
        protected DeliveryWait WaitForDelivery(string field = "", float timeoutSeconds = 0f)
            => Track(new DeliveryWait(new FieldId(field), timeoutSeconds, null));

        // 待ちは、このチュートリアルが始まってから、まだ使われていない操作も数える（先回りした操作。TutorialHistory）。
        // もう起きていれば、すぐ終わる。終わったら、その種類の操作は使ったことにする（同じ操作で 2 回進まない）。

        /// <summary>デバイスで決定するのを待つ。先に決定していれば、すぐ終わる。timeoutSeconds が 0 なら、時間切れはない。</summary>
        protected ConfirmWait WaitForConfirm(float timeoutSeconds = 0f)
        {
            TutorialHistory history = History; // 待ちより先に聞き始める（同じ知らせで、記録が先に増えるように）
            ConfirmWait wait = Track(new ConfirmWait(timeoutSeconds, null));
            if (history.TakeConfirm(out TextureId texture)) wait.CompleteWith(texture);
            else wait.Completed = () => history.UseAll(TutorialEvent.Confirmed);
            return wait;
        }

        /// <summary>フォークで雲を刺すのを待つ。もう刺していれば（先に刺していれば）、すぐ終わる。timeoutSeconds が 0 なら、時間切れはない。</summary>
        protected ForkWait WaitForPierce(float timeoutSeconds = 0f)
            => ForkStep(ForkGoal.Pierce, TutorialEvent.Pierced, timeoutSeconds);

        /// <summary>雲のかけらが付いたフォークの先を、口に含むのを待つ。もう含んでいれば（先に含んでいれば）、すぐ終わる。timeoutSeconds が 0 なら、時間切れはない。</summary>
        protected ForkWait WaitForMouth(float timeoutSeconds = 0f)
            => ForkStep(ForkGoal.Mouth, TutorialEvent.MouthEntered, timeoutSeconds);

        ForkWait ForkStep(ForkGoal goal, TutorialEvent kind, float timeoutSeconds)
        {
            TutorialHistory history = History;
            ForkWait wait = Track(new ForkWait(goal, timeoutSeconds, null));
            if (!wait.IsDone && history.Pending(kind) > 0) wait.CompleteNow();
            if (wait.IsDone) history.UseAll(kind);
            else wait.Completed = () => history.UseAll(kind);
            return wait;
        }

        /// <summary>食べ物が生まれるのを待つ。food を空にすると、何が生まれてもよい。先に生まれていれば、すぐ終わる。timeoutSeconds が 0 なら、時間切れはない。</summary>
        protected SpawnWait WaitForSpawn(string food = "", float timeoutSeconds = 0f)
        {
            TutorialHistory history = History;
            var only = new FoodId(food);
            SpawnWait wait = Track(new SpawnWait(only, timeoutSeconds, null));
            if (history.TakeSpawn(only, out Spawned spawned)) wait.CompleteWith(spawned);
            else wait.Completed = () => history.UseAll(TutorialEvent.Spawned);
            return wait;
        }

        /// <summary>噛んでいる間に、食感が count 回切り替わるのを待つ。先に切り替わった分も数える。timeoutSeconds が 0 なら、時間切れはない。</summary>
        protected TextureChangeWait WaitForTextureChanges(int count, float timeoutSeconds = 0f)
        {
            TutorialHistory history = History;
            TextureChangeWait wait = Track(new TextureChangeWait(count, timeoutSeconds, null, history.Pending(TutorialEvent.TextureChanged)));
            if (wait.IsDone) history.UseAll(TutorialEvent.TextureChanged);
            else wait.Completed = () => history.UseAll(TutorialEvent.TextureChanged);
            return wait;
        }

        /// <summary>体験者が、そのフィールドの前に立つのを待つ。field を空にすると、どこでもよい。もう立っていれば、すぐ終わる。timeoutSeconds が 0 なら、時間切れはない。</summary>
        protected StandWait WaitForStanding(string field = "", float timeoutSeconds = 0f)
            => Track(new StandWait(new FieldId(field), timeoutSeconds, null));

        /// <summary>巫女にセリフを言わせる（合図 <c>miko.say.&lt;id&gt;</c>）。id が空なら何もしない。セリフの中身は、シーンの Miko の LineBook に書く。</summary>
        protected void Say(string lineId)
        {
            if (!string.IsNullOrEmpty(lineId)) Cue("miko.say." + lineId);
        }

        /// <summary>渡した待ちのうち、どれかが終わるのを待つ。終わったら、ほかは取り消す。</summary>
        protected AnyWait WaitForAny(params TutorialWait[] waits) => new AnyWait(waits);

        T Track<T>(T wait) where T : TutorialWait
        {
            _waits.Add(wait);
            return wait;
        }

        /// <summary>途中でやめられた（<see cref="TutorialRunner.Stop"/>）。流している動画を止めるなど、後片付けを書く。最後まで進んだときは呼ばれない。</summary>
        protected virtual void OnAborted() { }

        internal void Abort() => OnAborted();

        // このチュートリアルが終わった・やめたときに、待ちが残っていれば聞くのをやめる。
        internal void CancelWaits()
        {
            foreach (TutorialWait wait in _waits.ToArray()) wait.Cancel();
            _waits.Clear();
            // 自分で作った記録だけを片づける。Runner から受け取った記録は、Runner が片づける。
            if (!_ownsHistory) return;
            _history?.Dispose();
            _history = null;
            _ownsHistory = false;
        }

        protected virtual void OnDestroy()
        {
            if (_ownsHistory) _history?.Dispose();
        }
    }
}
