using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>チュートリアル 4「届ける」（シナリオブック S6）です。海の前まで歩き、魚を海に届けてもらいます。</summary>
    public sealed class Tutorial4Deliver : Chapter
    {
        [SerializeField] string field = "sea";
        [SerializeField] string food = "fish";

        [Header("声（空なら字幕だけ）")]
        [SerializeField] AudioClip walkVoice;
        [SerializeField] AudioClip hereVoice;
        [SerializeField] AudioClip notHereVoice;
        [SerializeField] AudioClip splendidVoice;
        [SerializeField] AudioClip trueGodVoice;

        protected override async Awaitable Run()
        {
            // 1. 前の場面の食べ物を片付け、海を光らせ、足あとを出して、巫女がついてきます。
            Foods.Clear();
            Field target = Field.Get(field);
            target.Want(food);
            target.Glow(true);
            target.Footprint(true);
            Miko.Follow();
            Miko.SayNow("Walk to the sea, and create the fish there.", walkVoice);

            // 2. 海の前に着いたら、ひと言そえます。海に魚が届くまで待ちます。
            bool arrived = false;
            bool delivered = false;
            while (!GaveUp)
            {
                if (!arrived && Player.Field == field)
                {
                    arrived = true;
                    target.Footprint(false);
                    Miko.SayNow("Now, create it here.", hereVoice);
                }

                Delivery? got = await Player.Delivered(1f); // 1 秒ごとに、着いたかどうかも見直します
                if (got == null) continue;
                if (got.Value.Field == field && got.Value.Food == food) { delivered = true; break; }
                if (got.Value.Field == field) { target.Want(food); continue; } // 違う物が届いた。また欲しがらせます

                // よその場所に届いた。足あとをもう一度出します。
                Miko.SayNow("Not here — go to the place that glows.", notHereVoice);
                target.Footprint(true);
                arrived = false;
            }

            // 3. 喜んで、締めくくります。
            if (delivered) await Miko.Say("Splendid!", splendidVoice);
            target.Footprint(false);
            target.Glow(false);
            Miko.Stay();
            await Miko.Say("Find what each place needs, go there, and create it. Now you are a true god.", trueGodVoice);
        }

        protected override void CleanUp()
        {
            Field target = Field.Get(field);
            target.Footprint(false);
            target.Glow(false);
            Miko.Stay();
        }
    }
}
