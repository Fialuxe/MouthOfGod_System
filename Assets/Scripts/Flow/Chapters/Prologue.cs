using MouthOfGod.Tutorial;
using UnityEngine;

namespace MouthOfGod.Flow
{
    /// <summary>プロローグ（シナリオブック S1）です。導入の動画を流し、体験者に立ってもらいます。</summary>
    public sealed class Prologue : Chapter
    {
        [Tooltip("導入の動画を流す画面です。空なら、動画を飛ばして巫女のセリフから始めます。")]
        [SerializeField] VideoScreen screen;

        [Header("声（空なら字幕だけ）")]
        [SerializeField] AudioClip standUpVoice;

        protected override async Awaitable Run()
        {
            await Video.Play(screen);
            await Miko.Say("Please stand up and follow the booth staff.", standUpVoice);
            await Wait.Seconds(2f);
        }
    }
}
