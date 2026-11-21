using System;
using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>巫女のセリフ 1 つです。</summary>
    /// <remarks>表情・指さし・移動は、<see cref="MikoController.Say"/> で一緒に指定します。</remarks>
    [Serializable]
    public sealed class MikoLine
    {
        /// <summary>字幕に出す文です。体験者に見せるので、英語で書きます。</summary>
        [TextArea] public string text;

        /// <summary>鳴らす音声です。<see langword="null"/> なら、字幕だけ出します。</summary>
        public AudioClip voice;

        /// <summary>字幕を出している秒数です。</summary>
        /// <remarks>0 なら、音声の長さか、文字数から決まります。</remarks>
        [Min(0f)] public float seconds;
    }
}
