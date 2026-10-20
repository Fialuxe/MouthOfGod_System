using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// 人形劇スクリプトで <c>yield return</c> して待つもの。待っているあいだだけ、Core の知らせを聞く。
    /// 条件が満たされるか、時間切れになるか、<see cref="Cancel"/> されると終わり、聞くのをやめる。
    /// </summary>
    public abstract class TutorialWait : CustomYieldInstruction
    {
        readonly float _timeoutSeconds;
        readonly Func<float> _clock;
        readonly float _startedAt;

        protected TutorialWait(float timeoutSeconds, Func<float> clock)
        {
            _timeoutSeconds = timeoutSeconds;
            _clock = clock ?? (() => Time.time);
            _startedAt = _clock();
        }

        /// <summary>終わったか（条件が満たされた、時間切れ、取り消しのどれか）。</summary>
        public bool IsDone { get; private set; }

        /// <summary>時間切れで終わったか。</summary>
        public bool TimedOut { get; private set; }

        public override bool keepWaiting
        {
            get
            {
                Check();
                return !IsDone;
            }
        }

        // 時間切れかを確かめる。待っている側が毎フレーム呼ぶ。
        internal void Check()
        {
            if (IsDone) return;
            if (_timeoutSeconds > 0f && _clock() - _startedAt >= _timeoutSeconds)
            {
                TimedOut = true;
                Finish();
            }
        }

        /// <summary>待つのをやめる。聞くのもやめる。終わっていれば何もしない。</summary>
        public void Cancel() => Finish();

        // 条件が満たされた。
        protected void Complete() => Finish();

        void Finish()
        {
            if (IsDone) return;
            IsDone = true;
            Unsubscribe();
        }

        // 聞くのをやめる。1 回だけ呼ばれる。
        protected abstract void Unsubscribe();
    }

    /// <summary>
    /// フィールドに物が届くのを待つ。<c>only</c> を指定すれば、そのフィールドへの届きだけ。指定しなければ、どこへ届いてもよい。
    /// 終わったあとの <see cref="Result"/> で、何がどこへ届いたか（<see cref="Delivered.Kind"/> で正しいか）が分かる。時間切れなら null。
    /// </summary>
    public sealed class DeliveryWait : TutorialWait
    {
        readonly FieldId _only;
        readonly List<IFieldRequests> _subscribed = new List<IFieldRequests>();

        internal DeliveryWait(FieldId only, float timeoutSeconds, Func<float> clock) : base(timeoutSeconds, clock)
        {
            _only = only;
            foreach (IFieldRequests field in Fields.All) Subscribe(field);
            Fields.Added += Subscribe; // あとから置かれたフィールドも聞く
        }

        public Delivered? Result { get; private set; }

        void Subscribe(IFieldRequests field)
        {
            if (IsDone || _subscribed.Contains(field)) return;
            _subscribed.Add(field);
            field.Received += OnReceived;
        }

        void OnReceived(Delivered delivered)
        {
            if (IsDone) return;
            if (!_only.IsEmpty && delivered.Field != _only) return;
            Result = delivered;
            Complete();
        }

        protected override void Unsubscribe()
        {
            Fields.Added -= Subscribe;
            foreach (IFieldRequests field in _subscribed) field.Received -= OnReceived;
            _subscribed.Clear();
        }
    }

    /// <summary>
    /// デバイスで決定する（<see cref="IDevice.Confirmed"/>）のを待つ。噛む前の決定（食感が空）は数えない。
    /// 終わったあとの <see cref="Result"/> が、決定したときの食感。時間切れなら空。
    /// </summary>
    public sealed class ConfirmWait : TutorialWait
    {
        IDevice _device;

        internal ConfirmWait(float timeoutSeconds, Func<float> clock) : base(timeoutSeconds, clock)
        {
            Devices.WhenReady(Bind); // デバイスがあとから用意されても聞く
        }

        public TextureId Result { get; private set; }

        void Bind(IDevice device)
        {
            if (IsDone) return;
            _device = device;
            _device.Confirmed += OnConfirmed;
        }

        void OnConfirmed(TextureId texture)
        {
            if (IsDone || texture.IsEmpty) return;
            Result = texture;
            Complete();
        }

        protected override void Unsubscribe()
        {
            Devices.CancelWhenReady(Bind);
            if (_device != null) _device.Confirmed -= OnConfirmed;
            _device = null;
        }
    }

    /// <summary>
    /// フォークで雲を刺すのを待つ（<see cref="ForkGoal.Pierce"/>）。または、雲のかけらが付いたフォークの先を、口に含むのを待つ（<see cref="ForkGoal.Mouth"/>）。
    /// 待ち始めたときに、もうその状態なら、すぐ終わる。
    /// </summary>
    public sealed class ForkWait : TutorialWait
    {
        readonly ForkGoal _goal;
        IFork _fork;

        internal ForkWait(ForkGoal goal, float timeoutSeconds, Func<float> clock) : base(timeoutSeconds, clock)
        {
            _goal = goal;
            Forks.WhenReady(Bind); // フォークがあとから用意されても聞く
        }

        void Bind(IFork fork)
        {
            if (IsDone) return;
            _fork = fork;
            _fork.Pierced += OnChanged;
            _fork.MouthEntered += OnChanged;
            OnChanged();
        }

        void OnChanged()
        {
            if (IsDone || _fork == null) return;
            bool reached = _goal == ForkGoal.Pierce ? _fork.IsLoaded : _fork.IsLoaded && _fork.InMouth;
            if (reached) Complete();
        }

        protected override void Unsubscribe()
        {
            Forks.CancelWhenReady(Bind);
            if (_fork != null)
            {
                _fork.Pierced -= OnChanged;
                _fork.MouthEntered -= OnChanged;
            }
            _fork = null;
        }
    }

    /// <summary><see cref="ForkWait"/> が待つもの。</summary>
    public enum ForkGoal
    {
        /// <summary>雲を刺して、先にかけらが付いた。</summary>
        Pierce,
        /// <summary>かけらが付いた先を、口に含んだ。</summary>
        Mouth,
    }

    /// <summary>
    /// 渡した待ちのうち、どれかが終わるのを待つ。終わったら、ほかの待ちは取り消す。
    /// 終わったあとの <see cref="Index"/> が、最初に終わった待ちの番号（0 から）。
    /// 同じフレームに複数が終わったときは、先に渡したほうを選ぶ。
    /// </summary>
    public sealed class AnyWait : CustomYieldInstruction
    {
        readonly TutorialWait[] _waits;

        internal AnyWait(TutorialWait[] waits)
        {
            _waits = waits ?? new TutorialWait[0];
        }

        /// <summary>最初に終わった待ちの番号。まだなら -1。</summary>
        public int Index { get; private set; } = -1;

        /// <summary>最初に終わった待ち。まだなら null。</summary>
        public TutorialWait Winner => Index >= 0 ? _waits[Index] : null;

        public override bool keepWaiting
        {
            get
            {
                if (Index >= 0) return false;
                for (int i = 0; i < _waits.Length; i++)
                {
                    _waits[i].Check();
                    if (!_waits[i].IsDone) continue;
                    Index = i;
                    foreach (TutorialWait other in _waits) other.Cancel();
                    return false;
                }
                return _waits.Length > 0;
            }
        }
    }
}
