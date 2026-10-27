using System;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Game
{
    /// <summary>
    /// 本編の進行役。チュートリアルが終わったら本編（場面 main-game）を始め、制限時間が来たら終わらせる（場面 game-over）。
    /// 時間の計算は <see cref="GameClock"/>。ここは Unity とつなぐだけ。
    /// </summary>
    /// <remarks>
    /// - チュートリアルとの接続は「場面を見る」案（A）にした。チュートリアルは全部終わると場面を空に戻すので、
    ///   <see cref="Phases.Changed"/> で「<c>afterPhase</c>（既定 deliver）のあとに場面が空になった」を見て <see cref="Begin"/> する。
    ///   Tutorial の型を参照せず、手でつなぐ設定も要らない（Play するだけで動く）。UnityEvent で手つなぎする案（B）は、Play 後の手作業が残るので選ばなかった。
    ///   最後のチュートリアルの場面名を変えたら、<c>afterPhase</c> を合わせる（docs/progress-keys.md）。
    /// - 本編を始めるときに、フィールドは初期化しない（チュートリアルで戻した村をそのまま使う。Restart は体験の終わりで呼ぶ側が決める）。
    /// - 時間切れで、出ている欲しい物や飛んでいる食べ物は消さない（Core にそれをする手段が無い）。
    /// - 制限時間の既定 120 秒は仮。
    /// </remarks>
    public sealed class GameRunner : MonoBehaviour
    {
        static readonly PhaseId MainGame = new PhaseId("main-game");
        static readonly PhaseId GameOver = new PhaseId("game-over");

        [Tooltip("本編の制限時間（秒）。120 は仮の値")]
        [SerializeField, Min(1f)] float timeLimitSeconds = 120f;

        [Tooltip("この場面が終わって場面が空になったら、本編を始める（最後のチュートリアルの場面名。空にすると自動では始めない）")]
        [SerializeField] string afterPhase = "deliver";

        GameClock _clock;
        PhaseId _last;

        /// <summary>残り秒（切り上げ）が変わった。本編の開始時にも最初の値を出す。</summary>
        public event Action<int> RemainingChanged;

        /// <summary>残り 10 秒以下になった（カウントダウン開始。1 回）。</summary>
        public event Action CountdownStarted;

        /// <summary>時間切れ（1 回）。場面を game-over にしてから出す。</summary>
        public event Action TimeUp;

        /// <summary>本編が動いている。</summary>
        public bool IsRunning => _clock != null && !_clock.IsTimeUp;

        /// <summary>残り秒。本編が始まる前は制限時間。</summary>
        public float Remaining => _clock != null ? _clock.Remaining : timeLimitSeconds;

        void OnEnable() => Attach();

        void OnDisable() => Unattach();

        // 場面の変化を見はじめる／やめる。EditMode では OnEnable が呼ばれないので、テストからも呼ぶ。
        internal void Attach()
        {
            Phases.Changed -= OnPhaseChanged; // 二重に付けない
            _last = Phases.Current;
            Phases.Changed += OnPhaseChanged;
        }

        internal void Unattach()
        {
            Phases.Changed -= OnPhaseChanged;
        }

        void OnPhaseChanged(PhaseId phase)
        {
            PhaseId before = _last;
            _last = phase;
            if (!phase.IsEmpty || string.IsNullOrEmpty(afterPhase)) return;
            if (before == new PhaseId(afterPhase)) Begin();
        }

        /// <summary>本編を始める（場面を main-game にして、時計を動かす）。動いている間に呼ぶと、はじめからやり直す。</summary>
        public void Begin()
        {
            Detach();
            _clock = new GameClock(timeLimitSeconds);
            _clock.RemainingChanged += OnRemainingChanged;
            _clock.CountdownStarted += OnCountdownStarted;
            _clock.TimeUp += OnTimeUp;
            Phases.Set(MainGame);
            RemainingChanged?.Invoke(_clock.RemainingWholeSeconds);
        }

        void Update()
        {
            Tick(Time.deltaTime);
        }

        // Update から呼ぶ。テストからも呼ぶ。
        internal void Tick(float deltaSeconds)
        {
            if (IsRunning) _clock.Tick(deltaSeconds);
        }

        void OnRemainingChanged(int seconds) => RemainingChanged?.Invoke(seconds);
        void OnCountdownStarted() => CountdownStarted?.Invoke();

        void OnTimeUp()
        {
            Phases.Set(GameOver); // 先に場面を変える（TimeUp の購読先が Phases.Current を読めるように）
            TimeUp?.Invoke();
        }

        void Detach()
        {
            if (_clock == null) return;
            _clock.RemainingChanged -= OnRemainingChanged;
            _clock.CountdownStarted -= OnCountdownStarted;
            _clock.TimeUp -= OnTimeUp;
        }
    }
}
