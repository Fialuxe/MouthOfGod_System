using System.Collections.Generic;

namespace MouthOfGod.Tutorial
{
    /// <summary>
    /// チュートリアルのあいだで引き継ぐ「覚えたこと」。名前（文字列）だけを持つ。
    /// <see cref="TutorialRunner"/> が 1 つ持ち、人形劇スクリプトに渡す。T3 は、T2 のことではなく、名前だけを見る。
    /// </summary>
    /// <remarks>
    /// - 名前は「事実」にする（<c>texture-mapping-learned</c>）。特定のチュートリアルを指す名前（<c>t2-done</c>）は避ける。
    /// - 名前の一覧は docs/progress-keys.md に書く。一覧にない名前を、コードに書かない。
    /// - 値を持たせるのは、必要になってから。
    /// - <see cref="TutorialRunner.Begin"/> で空にする（次の体験者に引き継がない）。
    /// </remarks>
    public sealed class TutorialMemory
    {
        readonly HashSet<string> _keys = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>覚えているか。名前が空なら false。</summary>
        public bool Has(string key) => !string.IsNullOrEmpty(key) && _keys.Contains(key);

        /// <summary>覚える。名前が空なら何もしない。</summary>
        public void Set(string key)
        {
            if (!string.IsNullOrEmpty(key)) _keys.Add(key);
        }

        /// <summary>覚えていることを、すべて忘れる。</summary>
        public void Clear() => _keys.Clear();

        public int Count => _keys.Count;
    }
}
