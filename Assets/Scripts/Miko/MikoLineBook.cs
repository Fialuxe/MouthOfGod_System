using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女のセリフの一覧。Prefab の Miko の子（LineBook）に付ける。
    /// 文・声・指さす物を、セリフごとに入れる。MikoSpeech が、合図 <c>miko.say.&lt;id&gt;</c> の id で引く。
    /// 指さす物はシーンの物なので、シーンに置いた Miko で入れる。
    /// </summary>
    public sealed class MikoLineBook : MonoBehaviour
    {
        [SerializeField] List<MikoLine> lines = new List<MikoLine>();

        /// <summary>id のセリフを探す。無ければ null。</summary>
        public MikoLine Find(string id)
        {
            foreach (var line in lines)
                if (line != null && line.id == id) return line;
            return null;
        }

#if UNITY_EDITOR
        // コンポーネントを足した（または Reset した）ときに、見本のセリフを入れる。
        void Reset()
        {
            lines = new List<MikoLine>
            {
                Sample("welcome", "Long time no see, our god!\nWell, let's help villagers with me!", "d6e04766b4383b241a5dbae96280df58", null, false),
                Sample("give", "Oh, somewhat he wants something!\nlet's help him", "88d932c109ae25a47b75a5ac83b08e2b", MikoExpression.Normal, true),
                Sample("thanks", "Well done! Thank you for helping the villagers.", null, MikoExpression.Love, false),
            };
        }

        // expression が null なら、表情を変えない。voiceGuid が null なら、字幕だけ。
        static MikoLine Sample(string id, string text, string voiceGuid, MikoExpression? expression, bool point)
        {
            var voice = voiceGuid == null
                ? null
                : UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(voiceGuid));
            return new MikoLine
            {
                id = id,
                text = text,
                voice = voice,
                changeExpression = expression.HasValue,
                expression = expression ?? MikoExpression.Happy,
                point = point,
            };
        }
#endif
    }
}
