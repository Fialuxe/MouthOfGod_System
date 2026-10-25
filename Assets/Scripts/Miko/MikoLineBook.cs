using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女のセリフの一覧。文・声・指さす物を、セリフごとに入れる。MikoSpeech が、合図 <c>miko.say.&lt;id&gt;</c> の id で引く。
    /// </summary>
    /// <remarks>
    /// - 置き場所は 2 つある。巫女の共通のセリフは、Prefab の Miko の子（LineBook）に置く。
    ///   チュートリアルなど場面ごとのセリフは、その場面のオブジェクトに置く。担当者が、自分の場面のセリフだけを直せるようにするため。
    /// - 有効な LineBook は、シーンのどこにあっても引ける（<see cref="FindAny"/>）。id はシーン全体で重ならないようにする。重なると、警告を出す。
    /// - 指さす物はシーンの物なので、シーンに置いた LineBook で入れる。
    /// </remarks>
    public sealed class MikoLineBook : MonoBehaviour
    {
        [SerializeField] List<MikoLine> lines = new List<MikoLine>();

        static readonly List<MikoLineBook> Enabled = new List<MikoLineBook>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetEnabled() => Enabled.Clear();

        /// <summary>この一覧の id。</summary>
        public IEnumerable<string> Ids
        {
            get
            {
                foreach (var line in lines)
                    if (line != null && !string.IsNullOrEmpty(line.id)) yield return line.id;
            }
        }

        /// <summary>id のセリフを探す。無ければ null。</summary>
        public MikoLine Find(string id)
        {
            foreach (var line in lines)
                if (line != null && line.id == id) return line;
            return null;
        }

        /// <summary>有効なすべての LineBook から、id のセリフを探す。先に有効になった LineBook を先に見る。無ければ null。</summary>
        public static MikoLine FindAny(string id)
        {
            foreach (var book in Enabled)
            {
                var line = book.Find(id);
                if (line != null) return line;
            }
            return null;
        }

        void OnEnable()
        {
            foreach (var other in Enabled)
                foreach (var id in Ids)
                    if (other.Find(id) != null)
                        Debug.LogWarning($"[MikoLineBook] セリフの id「{id}」が、{other.name} と {name} の両方にあります。{other.name} のほうを使います。", this);
            Enabled.Add(this);
        }

        void OnDisable() => Enabled.Remove(this);

#if UNITY_EDITOR
        // コンポーネントを足した（または Reset した）ときに、見本のセリフを入れる。
        // 巫女の LineBook（オブジェクトの名前が LineBook）のときだけ入れる。場面ごとの LineBook に入れると、見本の id がほかと重なるため。
        void Reset()
        {
            if (name != "LineBook") return;

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
