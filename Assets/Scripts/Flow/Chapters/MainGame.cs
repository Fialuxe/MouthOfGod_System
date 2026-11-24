using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>本編です。制限時間の間、どのフィールドにも欲しい物を出し、届けた数と点数を数えます。</summary>
    /// <remarks>
    /// 欲しがられていた物を届けると、100 点です。続けて届けると倍率が上がり、最大 3 倍です。
    /// 欲しがられていない物を届けると、続けた数が 0 に戻ります。点は引きません。
    /// </remarks>
    public sealed class MainGame : Chapter
    {
        [Tooltip("本編の制限時間です。単位は秒です。")]
        [SerializeField, Min(1f)] float seconds = 120f;

        [Tooltip("残り時間と点数を出す表示です。空なら出しません。")]
        [SerializeField] ScoreBoard scoreBoard;

        const int PointsPerWish = 100;
        const int MaxStreakMultiplier = 3;

        /// <summary>直前の本編で、欲しがられていた物を届けた数です。エンディングが読みます。</summary>
        public static int Answered { get; private set; }

        /// <summary>直前の本編の点数です。続けて届けるほど、1 回の点が増えます。エンディングが読みます。</summary>
        public static int Score { get; private set; }

        protected override async Awaitable Run()
        {
            if (scoreBoard == null) scoreBoard = FindFirstObjectByType<ScoreBoard>();
            if (scoreBoard == null) Debug.LogWarning("[MainGame] ScoreBoard が見つかりません。残り時間と点数は出ません。", this);

            Answered = 0;
            Score = 0;
            int streak = 0;
            if (scoreBoard != null) scoreBoard.ShowScore(Score, streak, celebrate: false);
            foreach (Field field in Field.All) field.WantAll();

            float end = Time.time + seconds;
            while (Time.time < end)
            {
                if (scoreBoard != null) scoreBoard.ShowRemainingSeconds(Mathf.CeilToInt(end - Time.time));

                // 残り時間の表示を更新するため、待つのは長くても 1 秒にします。
                // 0 を渡すと無制限に待つので、短くても 0.01 秒にします。
                Delivery? got = await Player.Delivered(Mathf.Clamp(end - Time.time, 0.01f, 1f));
                if (got == null) continue;
                if (!got.Value.WasWanted)
                {
                    // 頼まれていない物を届けると、続けた数が途切れます。
                    streak = 0;
                    if (scoreBoard != null) scoreBoard.ShowScore(Score, streak, celebrate: false);
                    continue;
                }

                Answered++;
                streak++;
                Score += PointsPerWish * Mathf.Min(streak, MaxStreakMultiplier);
                if (scoreBoard != null) scoreBoard.ShowScore(Score, streak, celebrate: true);
                Field.Get(got.Value.Field).WantAll(); // 届けたフィールドに、次の欲しい物を出します。
            }
        }

        protected internal override void OnRestart()
        {
            if (scoreBoard != null) scoreBoard.HideAndClear();
        }
    }
}
