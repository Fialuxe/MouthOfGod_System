using TMPro;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>エンディングです。届けた数を伝え、巫女が締めくくります。</summary>
    /// <remarks>
    /// セリフは仮です。#357 で確定します。
    /// 結果の文字は場面が終わっても残し、やり直すときに消します。
    /// </remarks>
    public sealed class Ending : Chapter
    {
        [Tooltip("結果を出す文字です。空なら、巫女が言うだけです。")]
        [SerializeField] TMP_Text resultText;

        [Tooltip("最終の点数を出す表示です。本編と同じ物を入れます。空なら出しません。")]
        [SerializeField] ScoreBoard scoreBoard;

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

            if (scoreBoard == null) scoreBoard = FindFirstObjectByType<ScoreBoard>();
            if (scoreBoard != null) scoreBoard.ShowFinalScore(MainGame.Score, answered);

            Miko.Follow();
            await Miko.Say(result);
            await Miko.Say("Thank you, our god. The villagers will remember this day.", thanksVoice);
            await Miko.Say("You may take off the headset now.", byeVoice);
        }

        protected internal override void OnRestart()
        {
            if (resultText != null) resultText.gameObject.SetActive(false);
            if (scoreBoard != null) scoreBoard.HideAndClear();
        }
    }
}
