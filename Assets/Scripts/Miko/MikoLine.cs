using System;
using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>巫女のセリフ 1 つ。合図 <c>miko.say.&lt;id&gt;</c> で出る。シーンの Miko の MikoSpeech に並べる。</summary>
    [Serializable]
    public sealed class MikoLine
    {
        public string id;
        [TextArea] public string text;
        public AudioClip voice; // 空なら、字幕だけ出す
        public bool changeExpression;
        public MikoExpression expression;
        public bool point; // しゃべりはじめに指さしをする
        public Transform pointTarget; // 指さす物。空なら、MikoCueReceiver の Point Target（それも空なら向きを変えない）
        public Transform moveTo; // しゃべりはじめに、この場所へ飛んで行く。空なら、動かない
        public bool speakAfterMove; // 入れると、着いてからしゃべる。外すと、動きながらしゃべる
        [Min(0f)] public float seconds; // 字幕を出している長さ。0 なら、音声の長さか文字数から決まる
    }
}
