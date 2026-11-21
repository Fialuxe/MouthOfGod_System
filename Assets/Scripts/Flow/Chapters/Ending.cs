using TMPro;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>エンディングです。届けた数を伝え、巫女が締めくくります。セリフは仮です（#357 で確定します）。</summary>
    public sealed class Ending : Chapter
    {
        [Tooltip("結果を出す文字。空なら、巫女が言うだけです")]
        [SerializeField] TMP_Text resultText;

        [Header("声（空なら字幕だけ）")]
        [SerializeField] AudioClip thanksVoice;
        [SerializeField] AudioClip byeVoice;

        protected override async Awaitable Run()
        {
            int answered = MainGame.Answered;
            string result = answered == 1 ? "You answered 1 wish." : $"You answered {answered} wishes.";
            if (resultText != null)
            {
                resultText.text = result;
                resultText.gameObject.SetActive(true);
            }

            Miko.Follow();
            await Miko.Say(result);
            await Miko.Say("Thank you, our god. The villagers will remember this day.", thanksVoice);
            await Miko.Say("You may take off the headset now.", byeVoice);
        }

        protected internal override void OnRestart()
        {
            if (resultText != null) resultText.gameObject.SetActive(false);
        }
    }
}
