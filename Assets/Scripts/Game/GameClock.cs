using System;

namespace MouthOfGod.Game
{
    /// <summary>
    /// 本編の制限時間の時計。Unity に依存しない（<see cref="Tick"/> に経過秒を渡して進める）。
    /// </summary>
    /// <remarks>
    /// - 残り秒、残り <see cref="CountdownSeconds"/> 秒以下か、時間切れか、を持つ。
    /// - <see cref="CountdownStarted"/> と <see cref="TimeUp"/> は、それぞれ 1 回だけ出る。時間切れのあとに Tick しても、何も出ない。
    /// - <see cref="RemainingChanged"/> は、切り上げた残り秒（120.0〜119.01 秒なら 120）が変わったときに出る。
    /// - 一度に大きく進めても、カウントダウン開始 → 残り秒 → 時間切れの順に、出すべきものは出る。
    /// </remarks>
    public sealed class GameClock
    {
        /// <summary>カウントダウンを始める残り秒。</summary>
        public const float CountdownSeconds = 10f;

        float _remaining;
        bool _countdownRaised;
        bool _timeUpRaised;

        /// <param name="totalSeconds">制限時間（秒）。0 以下は 0（最初の Tick で時間切れ）。</param>
        public GameClock(float totalSeconds)
        {
            TotalSeconds = totalSeconds > 0f ? totalSeconds : 0f;
            _remaining = TotalSeconds;
        }

        public float TotalSeconds { get; }

        /// <summary>残り秒（0 以上）。</summary>
        public float Remaining => _remaining;

        /// <summary>切り上げた残り秒。UI に出す数。</summary>
        public int RemainingWholeSeconds => (int)Math.Ceiling(_remaining);

        /// <summary>残り <see cref="CountdownSeconds"/> 秒以下（時間切れも含む）。</summary>
        public bool IsCountdown => _remaining <= CountdownSeconds;

        public bool IsTimeUp => _remaining <= 0f;

        /// <summary>切り上げた残り秒が変わった（新しい残り秒を運ぶ）。</summary>
        public event Action<int> RemainingChanged;

        /// <summary>残りが <see cref="CountdownSeconds"/> 秒以下になった（1 回だけ）。</summary>
        public event Action CountdownStarted;

        /// <summary>時間切れになった（1 回だけ）。</summary>
        public event Action TimeUp;

        /// <summary>経過秒だけ進める。0 以下の値と、時間切れのあとの呼び出しは何もしない。</summary>
        public void Tick(float deltaSeconds)
        {
            if (_timeUpRaised) return;
            float dt = deltaSeconds > 0f ? deltaSeconds : 0f; // 負と NaN は 0 にする
            if (dt == 0f && !IsTimeUp) return;               // 制限時間 0 のときだけ、0 秒の Tick でも時間切れにする

            int before = RemainingWholeSeconds;
            _remaining = Math.Max(0f, _remaining - dt);
            int after = RemainingWholeSeconds;

            if (IsCountdown && !_countdownRaised)
            {
                _countdownRaised = true;
                CountdownStarted?.Invoke();
            }
            if (after != before) RemainingChanged?.Invoke(after);
            if (IsTimeUp && !_timeUpRaised)
            {
                _timeUpRaised = true;
                TimeUp?.Invoke();
            }
        }
    }
}
