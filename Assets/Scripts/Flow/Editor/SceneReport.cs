using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MouthOfGod.Flow.Editor
{
    /// <summary>
    /// シーンの中身を、テキストに書き出します。消す物を決めるときに使います。
    /// </summary>
    /// <remarks>
    /// 書き出すのは、物の階層・付いている部品・元の Prefab です。
    /// </remarks>
    public static class SceneReport
    {
        /// <summary>Main シーンを開き、中身を一時フォルダの main-scene-report.txt に書き出します。</summary>
        /// <remarks>
        /// 開いているシーンは Main に切り替わります。保存していない変更があれば、先に保存してください。
        /// メニューには出していないので、スクリプトか -executeMethod から呼びます。
        /// </remarks>
        public static void ReportMain()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
            var sb = new StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects()) Write(root.transform, 0, sb);
            string path = Path.Combine(Path.GetTempPath(), "main-scene-report.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[SceneReport] 書き出しました: " + path);
        }

        // 1 行に、名前・無効かどうか・部品・元の Prefab を書き、子へ深さ優先でたどります。
        static void Write(Transform t, int depth, StringBuilder sb)
        {
            var types = t.GetComponents<Component>()
                .Select(c => c == null ? "(Missing)" : c.GetType().Name)
                .Where(n => n != "Transform" && n != "RectTransform");
            string prefab = PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)
                ? " <" + PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) + ">"
                : "";
            sb.Append(' ', depth * 2).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)")
              .Append(" [").Append(string.Join(", ", types)).Append(']').Append(prefab).Append('\n');
            foreach (Transform child in t) Write(child, depth + 1, sb);
        }
    }
}
