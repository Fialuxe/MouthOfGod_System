using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>チュートリアル 3「食感で変わる・欲しい物」（シナリオブック S5）です。海の人が欲しがる魚を、食感で選んで生んでもらいます。</summary>
    public sealed class Tutorial3Recognize : Chapter
    {
        [SerializeField] string field = "sea";
        [SerializeField] string food = "fish";

        [Header("声（空なら字幕だけ）")]
        [SerializeField] AudioClip troubledVoice;
        [SerializeField] AudioClip whichFeelVoice;
        [SerializeField] AudioClip wrongVoice;
        [SerializeField] AudioClip fishHereVoice;

        protected override async Awaitable Run()
        {
            // 1. 海の人に、魚を欲しがらせます。
            Field target = Field.Get(field);
            target.Want(food);
            target.Glow(true);
            await Miko.Say("Oh, the people by the sea are troubled. They want fish.", troubledVoice);

            // 2. 魚が生まれるまで待ちます。違う物なら、もう一度。
            Clouds.Glow();
            Miko.SayNow("Pierce a new cloud. Which feel was the fish?", whichFeelVoice);
            while (!GaveUp)
            {
                string got = await Player.Spawned(Remaining);
                if (got == null || got == food) break;
                Miko.SayNow("Not that one. Which feel was the fish?", wrongVoice);
            }

            // 3. 恵みは立っている場所に落ちる、と教えます。魚の欲しい物は出したまま、チュートリアル 4 で海に届けます。
            await Wait.Seconds(0.5f);
            await Miko.Say("That's the fish — but blessings fall where you stand. Fish belong in the sea.", fishHereVoice);
        }

        protected override void CleanUp() => Clouds.Calm();
    }
}
