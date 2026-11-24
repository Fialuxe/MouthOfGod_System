using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>チュートリアル 2「物が生まれる」（シナリオブック S4）です。お供え物のりんごを、口から生んでもらいます。</summary>
    public sealed class Tutorial2Spawn : Chapter
    {
        [Tooltip("村人が供えたお供え物。この場面の間だけ出します")]
        [SerializeField] GameObject offering;

        [Tooltip("生んでほしい食べ物")]
        [SerializeField] string targetFood = "apple";

        [Header("声（空なら字幕だけ）")]
        [SerializeField] AudioClip rememberVoice;
        [SerializeField] AudioClip rememberShortVoice;
        [SerializeField] AudioClip hintPierceVoice;
        [SerializeField] AudioClip hintMouthVoice;
        [SerializeField] AudioClip waitAppleVoice;
        [SerializeField] AudioClip wrongVoice;
        [SerializeField] AudioClip bornVoice;

        protected override async Awaitable Run()
        {
            // 1. お供え物を出し、刺して口に入れてもらいます。チュートリアル 1 を自分で終えていれば、説明を短くします。
            if (offering != null) offering.SetActive(true);
            Clouds.Glow();
            if (Memory.Contains("fork-learned"))
                Miko.SayNow("Remember the apple the villagers offered? Try it again.", rememberShortVoice);
            else
                Miko.SayNow("Remember the apple the villagers offered? Pierce a cloud and put it in your mouth.", rememberVoice);

            while (!GaveUp && !await Player.Pierced(15f)) Miko.SayNow("Pierce a cloud with your fork.", hintPierceVoice);
            while (!GaveUp && !await Player.InMouth(15f)) Miko.SayNow("Bring it to your mouth.", hintMouthVoice);

            // 2. りんごが生まれるまで待ちます。違う物なら、もう一度。
            Miko.SayNow("As the feel changes, so does the blessing. Wait for the apple.", waitAppleVoice);
            bool born = false;
            while (!GaveUp)
            {
                string food = await Player.Spawned(20f);
                if (food == null) Miko.SayNow("As the feel changes, so does the blessing. Wait for the apple.", waitAppleVoice);
                else if (food == targetFood) { born = true; break; }
                else Miko.SayNow("That too is a blessing — but now, the apple.", wrongVoice);
            }

            // 3. お供え物を消し、生まれたことを伝えます。
            if (offering != null) offering.SetActive(false);
            if (born)
            {
                await Wait.Seconds(0.5f);
                await Miko.Say("Look! A blessing is born from your mouth! Remember that feel.", bornVoice);
            }
            await Wait.Seconds(1.5f);
        }

        protected override void CleanUp()
        {
            if (offering != null) offering.SetActive(false);
            Clouds.Calm();
        }
    }
}
