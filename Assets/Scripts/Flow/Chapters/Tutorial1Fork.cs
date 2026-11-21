using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>チュートリアル 1「フォークの使い方」（シナリオブック S3）です。</summary>
    /// <remarks>
    /// 雲を刺し、口に入れ、食感を感じて、ボタンで決めてもらいます。
    /// 最後まで自分でできたら、<see cref="Chapter.Memory"/> に "fork-learned" を入れます。
    /// </remarks>
    public sealed class Tutorial1Fork : Chapter
    {
        [Header("声（空なら字幕だけ）")]
        [SerializeField] AudioClip pierceVoice;
        [SerializeField] AudioClip mouthVoice;
        [SerializeField] AudioClip mouthAgainVoice;
        [SerializeField] AudioClip changingVoice;
        [SerializeField] AudioClip buttonVoice;
        [SerializeField] AudioClip goodVoice;

        protected override async Awaitable Run()
        {
            // 1. 雲を刺してもらいます。10 秒たっても刺さなければ、雲を手前に寄せます。
            Clouds.Glow();
            Miko.SayNow("Take the fork in your hand. Pierce that glowing cloud.", pierceVoice);
            if (!await Player.Pierced(10f))
            {
                Clouds.Approach();
                await Player.Pierced(Remaining);
            }
            await Wait.Seconds(0.5f);

            // 2. 口に入れてもらいます。10 秒たっても入れなければ、もう一度言います。
            Miko.SayNow("Now bring it to your mouth.", mouthVoice);
            if (!await Player.InMouth(10f))
            {
                Miko.SayNow("Bring it to your mouth.", mouthAgainVoice);
                await Player.InMouth(Remaining);
            }
            await Wait.Seconds(0.5f);

            // 3. 食感が切り替わるのを、3 回感じてもらいます。
            Miko.SayNow("Keep it in your mouth. Feel it? The texture keeps changing.", changingVoice);
            await Player.TextureChanged(3, Remaining);

            // 4. ボタンで決めてもらいます。
            Miko.SayNow("When it feels right, press the button.", buttonVoice);
            bool confirmed = await Player.Confirmed(Remaining);

            // 5. 締めくくります。最後まで自分でできたら、チュートリアル 2 の説明を短くします。
            Clouds.Calm();
            await Miko.Say("Good.", goodVoice);
            if (confirmed) Memory.Add("fork-learned");
            await Wait.Seconds(2f);
        }

        protected override void CleanUp() => Clouds.Calm();
    }
}
