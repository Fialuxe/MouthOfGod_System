using System;
using System.Collections;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.UI
{
    /// <summary>
    /// 合図（<see cref="Cues.Requested"/>）を受けて、物を出す・消す・UnityEvent を呼ぶ。コードを書かずに、Inspector だけで合図に反応できる。
    /// 例: <c>sea.glow</c> で光る物を出す、<c>village.cheer</c> で村人の Animator を呼ぶ、<c>staff.alert</c> でスタッフ向けの表示を出す。
    /// </summary>
    /// <remarks>
    /// - 合図の名前は、大文字小文字を区別して、そのまま比べる。同じ合図の行がいくつあっても、すべて動く。
    /// - <c>Seconds</c> が 0 より大きい行は、その秒数のあとに元に戻す（出した物を消し、消した物を出す）。戻す前に同じ合図が来たら、そこから数え直す。
    /// - このコンポーネントを付けた物は、消さない（Hide に入れない）。無効になると、合図を受けず、元に戻す予定も止まる。
    /// </remarks>
    public sealed class CueReaction : MonoBehaviour
    {
        /// <summary>1 つの合図への反応。</summary>
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("受ける合図の名前（例: sea.glow、village.cheer、device.button.glow）")]
            [SerializeField] string cue = "";

            [Tooltip("合図で出す物")]
            [SerializeField] GameObject[] show = new GameObject[0];

            [Tooltip("合図で消す物")]
            [SerializeField] GameObject[] hide = new GameObject[0];

            [Tooltip("0 なら、そのまま。0 より大きければ、その秒数のあとに元に戻す（Show を消し、Hide を出す）")]
            [SerializeField, Min(0f)] float seconds;

            [Tooltip("合図で呼ぶもの（音、Animator のトリガー、パーティクルなど）")]
            [SerializeField] UnityEvent onCue = new UnityEvent();

            public Entry() { }

            // テスト用。
            internal Entry(string cue, GameObject[] show, GameObject[] hide, float seconds = 0f)
            {
                this.cue = cue ?? "";
                this.show = show ?? new GameObject[0];
                this.hide = hide ?? new GameObject[0];
                this.seconds = seconds;
            }

            public string Cue => cue;
            public float Seconds => seconds;
            public UnityEvent OnCue => onCue;

            internal void Apply(bool on)
            {
                SetAll(show, on);
                SetAll(hide, !on);
            }

            static void SetAll(GameObject[] objects, bool active)
            {
                if (objects == null) return;
                foreach (var go in objects)
                    if (go != null) go.SetActive(active);
            }
        }

        [Tooltip("合図ごとの反応。上から順に動く")]
        [SerializeField] Entry[] entries = new Entry[0];

        // 例外の出し先。テストで差し替える。
        internal static Action<Exception> ReportException = Debug.LogException;

        // 元に戻す予定（行 → コルーチン）。
        readonly Dictionary<Entry, Coroutine> _reverts = new Dictionary<Entry, Coroutine>();

        /// <summary>反応の一覧。</summary>
        public IReadOnlyList<Entry> Entries => entries;

        // テスト用。
        internal void Configure(params Entry[] newEntries) => entries = newEntries ?? new Entry[0];

        void OnEnable() => Cues.Requested += OnCue;

        void OnDisable()
        {
            Cues.Requested -= OnCue;
            _reverts.Clear(); // 無効になると、コルーチンも止まる
        }

        void OnCue(CueRequested request) => Handle(request.Cue);

        /// <summary>その合図に合う行を、すべて動かす。合った行の数を返す。テストからも呼ぶ。</summary>
        internal int Handle(CueId cue)
        {
            if (cue.IsEmpty || entries == null) return 0;
            var count = 0;
            foreach (var entry in entries)
            {
                if (entry == null || entry.Cue != cue.Value) continue;
                count++;
                entry.Apply(true);
                if (entry.Seconds > 0f) ScheduleRevert(entry);
                // 1 つの行の呼び先が例外を投げても、残りの行は動かす（例外は Console に出す）。
                try { entry.OnCue?.Invoke(); }
                catch (Exception e) { ReportException(e); }
            }
            return count;
        }

        void ScheduleRevert(Entry entry)
        {
            if (_reverts.TryGetValue(entry, out var running) && running != null) StopCoroutine(running);
            _reverts.Remove(entry);
            // 編集中（EditMode のテスト）は、コルーチンが動かないので予約しない
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            _reverts[entry] = StartCoroutine(RevertAfter(entry));
        }

        IEnumerator RevertAfter(Entry entry)
        {
            yield return new WaitForSeconds(entry.Seconds);
            _reverts.Remove(entry);
            entry.Apply(false);
        }
    }
}
