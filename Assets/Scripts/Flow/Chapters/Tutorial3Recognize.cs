using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>チュートリアル 3「食感で変わる・欲しい物」（シナリオブック S5）です。海の人が欲しがる魚を、食感で選んで生んでもらいます。</summary>
    public sealed class Tutorial3Recognize : Chapter
    {
        [SerializeField] string field = "sea";
        [Tooltip("届けてほしい食べ物（食べ物の Prefab）")]
        [SerializeField] GameObject wantedFood;

        [Tooltip("この場面の間、どこに立っていても Wanted Food を生む食感")]
        [SerializeField] string wantedTexture = "placeholder-e";

        [Header("声（空なら字幕だけ）")]
        [SerializeField] AudioClip troubledVoice;
        [SerializeField] AudioClip whichFeelVoice;
        [SerializeField] AudioClip wrongVoice;
        [SerializeField] AudioClip fishHereVoice;
        [SerializeField] AudioClip alreadyDeliveredVoice;

        /// <summary>この場面で、魚がたまたま海に届いたときに、<see cref="Chapter.Memory"/> に入れる印です。チュートリアル 4 は、これがあると歩く所を飛ばします。</summary>
        internal const string DeliveredEarly = "fish-delivered-early";

        protected override async Awaitable Run()
        {
            // 1. 海の人に、魚を欲しがらせます。
            Field target = Field.Get(field);
            target.Want(wantedFood);
            Foods.Override(wantedTexture, wantedFood);
            target.Glow(true);
            await Miko.Say("Oh, the people by the sea are troubled. They want fish.", troubledVoice);

            // 2. 魚が生まれるまで待ちます。違う物なら、もう一度。
            Clouds.Glow();
            Miko.SayNow("Pierce a new cloud. Which feel was the fish?", whichFeelVoice);
            while (!GaveUp)
            {
                GameObject got = await Player.Spawned(Remaining);
                if (got == null || got == wantedFood) break;
                Miko.SayNow("Not that one. Which feel was the fish?", wrongVoice);
            }

            // 3. 生まれた魚が落ちるまで待ちます。届いた判定には 1 秒の間があるので、少し長めに待ちます。
            //    たまたま海に届いたらほめて、チュートリアル 4 の歩く所を飛ばします。
            Delivery? landed = GaveUp ? null : await Player.Delivered(3f);
            if (landed is { } d && d.Field == field && d.Food == wantedFood)
            {
                Memory.Add(DeliveredEarly);
                await Miko.Say("Oh! The fish reached the sea already. Splendid!", alreadyDeliveredVoice);
                return;
            }

            // 恵みは立っている場所に落ちる、と教えます。魚の欲しい物は出したまま、チュートリアル 4 で海に届けます。
            await Miko.Say("That's the fish — but blessings fall where you stand. Fish belong in the sea.", fishHereVoice);
        }

        protected override void CleanUp()
        {
            Foods.ClearOverrides();
            Clouds.Calm();
        }
    }
}
