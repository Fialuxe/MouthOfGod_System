using System;
using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>巫女のセリフ 1 つです。表情・指さし・移動は、MikoController.Say で一緒に指定します。</summary>
    [Serializable]
    public sealed class MikoLine
    {
        [TextArea] public string text;
        public AudioClip voice; // 空なら、字幕だけ出す
        [Min(0f)] public float seconds; // 字幕を出している長さ。0 なら、音声の長さか文字数から決まる
    }
}
