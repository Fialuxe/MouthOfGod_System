using TMPro;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>本編です。制限時間の間、どのフィールドにも欲しい物を出し、届けた数を数えます。</summary>
    public sealed class MainGame : Chapter
    {
        [SerializeField, Min(1f)] float seconds = 120f;

        [Tooltip("残り時間を出す文字。空なら出しません")]
        [SerializeField] TMP_Text timeText;

        /// <summary>直前の本編で、欲しがられていた物を届けた数です。エンディングが読みます。</summary>
        public static int Answered { get; private set; }

        protected override async Awaitable Run()
        {
            Answered = 0;
            foreach (Field field in Field.All) field.WantAll();

            float end = Time.time + seconds;
            while (Time.time < end)
            {
                if (timeText != null) timeText.text = Mathf.CeilToInt(end - Time.time).ToString();

                Delivery? got = await Player.Delivered(Mathf.Clamp(end - Time.time, 0.01f, 1f));
                if (got?.WasWanted != true) continue;
                Answered++;
                Field.Get(got.Value.Field).WantAll(); // 届けたフィールドに、次の欲しい物を出します
            }
        }

        protected override void CleanUp()
        {
            if (timeText != null) timeText.text = "";
        }
    }
}
