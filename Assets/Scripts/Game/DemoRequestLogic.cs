using System;
using System.Collections.Generic;
using MouthOfGod.Core;

namespace MouthOfGod.Game
{
    /// <summary>
    /// デモ用の仮の進行の決まり（#287）。いつ・どのフィールドに欲しい物を出すか、いつ待ちきれたことにするかを決める。
    /// 時間は引数で受ける（Unity の時計を読まない）ので、EditMode のテストで手で進められる。
    /// 本編の進行（スコアや届け先の判定を含む）ができたら置き換える。
    /// </summary>
    public sealed class DemoRequestLogic
    {
        readonly float _interval;
        readonly float _expireSeconds;
        readonly float _earlySeconds;
        readonly int _earlyMaxOpen;
        readonly int _maxOpen;

        // 出ている欲しい物（フィールドと番号で見分ける）と、待ちきれる時刻。
        readonly List<Pending> _pending = new List<Pending>();
        float? _startAt;
        float _nextOpenAt;
        int _rotate;

        sealed class Pending
        {
            public IFieldRequests Field;
            public int Request;
            public float ExpireAt;
        }

        /// <param name="interval">欲しい物を出す間隔（秒）。</param>
        /// <param name="expireSeconds">届かずに待ちきれるまでの時間（秒）。</param>
        /// <param name="earlySeconds">最初の何秒間、同時に出す数を <paramref name="earlyMaxOpen"/> にするか。</param>
        public DemoRequestLogic(float interval = 30f, float expireSeconds = 20f, float earlySeconds = 30f, int earlyMaxOpen = 1, int maxOpen = 2)
        {
            _interval = Math.Max(0f, interval);
            _expireSeconds = Math.Max(0f, expireSeconds);
            _earlySeconds = Math.Max(0f, earlySeconds);
            _earlyMaxOpen = Math.Max(0, earlyMaxOpen);
            _maxOpen = Math.Max(0, maxOpen);
        }

        /// <summary>いま出ている（まだ終わっていない）欲しい物の数。</summary>
        public int OpenCount => _pending.Count;

        /// <summary>欲しい物が出た。待ちきれる時刻を決める。同じ番号を 2 回知らされても 1 つ。</summary>
        public void OnIssued(IFieldRequests field, int request, float now)
        {
            if (field == null) return;
            if (Find(field, request) >= 0) return;
            _pending.Add(new Pending { Field = field, Request = request, ExpireAt = now + _expireSeconds });
        }

        /// <summary>欲しい物が終わった（届いた・Expire・リセット）。番号を忘れる。出ていない番号なら何もしない。</summary>
        public void OnClosed(IFieldRequests field, int request)
        {
            int i = Find(field, request);
            if (i >= 0) _pending.RemoveAt(i);
        }

        /// <summary>そのフィールドの分をすべて忘れる（フィールドが外れたとき）。</summary>
        public void Forget(IFieldRequests field)
        {
            _pending.RemoveAll(p => p.Field == field);
        }

        public void Clear()
        {
            _pending.Clear();
        }

        /// <summary>
        /// 1 回の見回り。待ちきれたものに Expire(番号) を呼び、間隔が来ていて空きがあれば、CanOpen のフィールドに Open() を呼ぶ。
        /// <paramref name="allowOpen"/> が false（チュートリアル中など）のときは、Expire だけで Open はしない。
        /// 最初に呼ばれた時刻から数え始め、最初の見回りですぐ 1 つ出す。出したら次は間隔のあと。出せるフィールドが無ければ、次の見回りでまた試す。
        /// </summary>
        public void Tick(IReadOnlyList<IFieldRequests> fields, float now, bool allowOpen = true)
        {
            if (_startAt == null)
            {
                _startAt = now;
                _nextOpenAt = now;
            }

            // Expire で Closed が出て _pending が変わるので、先に取り出してから呼ぶ。
            List<Pending> expired = null;
            foreach (Pending p in _pending)
            {
                if (now < p.ExpireAt) continue;
                if (expired == null) expired = new List<Pending>();
                expired.Add(p);
            }
            if (expired != null)
            {
                foreach (Pending p in expired)
                {
                    _pending.Remove(p); // Closed が来なくても、もう Expire は呼ばない
                    p.Field.Expire(p.Request);
                }
            }

            if (!allowOpen || fields == null || fields.Count == 0) return;
            if (now < _nextOpenAt || _pending.Count >= MaxOpen(now)) return;

            // 同じフィールドばかりにならないよう、出した次のフィールドから探す。
            int count = fields.Count;
            for (int n = 0; n < count; n++)
            {
                int index = (_rotate + n) % count;
                IFieldRequests field = fields[index];
                if (field == null || !field.CanOpen) continue;
                if (!field.Open()) continue;
                _rotate = index + 1;
                _nextOpenAt = now + _interval;
                return;
            }
        }

        // 同時に出してよい数。最初の何秒かは少なくする。
        int MaxOpen(float now) => now - _startAt.Value < _earlySeconds ? _earlyMaxOpen : _maxOpen;

        int Find(IFieldRequests field, int request)
        {
            for (int i = 0; i < _pending.Count; i++)
                if (_pending[i].Field == field && _pending[i].Request == request) return i;
            return -1;
        }
    }
}
