using System.Text;
using MouthOfGod.Core;
using MouthOfGod.Food;
using MouthOfGod.Village;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.DevTools.Editor
{
    /// <summary>
    /// 場所ごとに食べ物が引けているかを調べる材料を、Console に 1 回でまとめて出します。
    /// </summary>
    /// <remarks>
    /// <para>
    /// メニューの MouthOfGod/Dev から、Play 中に押します。
    /// フィールドの前に立った状態で押します。
    /// 結果を見れば、立つ場所の箱に入っていないのかが分かります。
    /// </para>
    /// <para>
    /// 出すのは、いま立っているフィールドと、頭の位置です。
    /// 各「立つ場所」については、位置・広さ・頭との距離・入っているかを出します。
    /// </para>
    /// </remarks>
    public static class FieldDiagnostics
    {
        /// <summary>調べた結果を Console に出します。Play 中でなければ、警告を出すだけです。</summary>
        [MenuItem("MouthOfGod/Dev/Print Field Diagnostics (Play mode)")]
        public static void Print()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FieldDiagnostics] Play 中に押してください。");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[FieldDiagnostics]");
            sb.AppendLine("立っているフィールド（Standing）: " + (Standing.Field.IsEmpty ? "なし" : Standing.Field.Value));

            Camera camera = Camera.main;
            if (camera == null) sb.AppendLine("頭（Main Camera）: 見つかりません");
            else sb.AppendLine("頭の位置（ワールド）: " + camera.transform.position.ToString("F2"));

            StandArea[] areas = Object.FindObjectsByType<StandArea>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.AppendLine("立つ場所（StandArea）: " + areas.Length + " 個");
            foreach (StandArea area in areas)
            {
                // center と size は StandArea の非公開の欄なので、SerializedObject で読みます。
                // StandArea の欄の名前を変えたら、ここも合わせます。
                var so = new SerializedObject(area);
                Vector3 center = so.FindProperty("center").vector3Value;
                Vector3 size = so.FindProperty("size").vector3Value;
                Transform t = area.transform;
                Vector3 worldCenter = t.TransformPoint(center);
                Vector3 s = t.lossyScale;
                var worldSize = new Vector3(Mathf.Abs(size.x * s.x), Mathf.Abs(size.y * s.y), Mathf.Abs(size.z * s.z));
                string distance = "-";
                if (camera != null)
                {
                    Vector3 local = t.InverseTransformPoint(camera.transform.position) - center;
                    distance = "横 " + Mathf.Abs(local.x * s.x).ToString("F2") + " m / 奥行き " + Mathf.Abs(local.z * s.z).ToString("F2") + " m（中心から、ワールドの長さ）";
                }
                sb.AppendLine("  - " + (area.Field.IsEmpty ? "(フィールド ID が空)" : area.Field.Value)
                    + " [" + area.name + "] 中心 " + worldCenter.ToString("F2")
                    + " / 広さ（横 × 奥行き）" + worldSize.x.ToString("F2") + " × " + worldSize.z.ToString("F2") + " m"
                    + " / 頭まで: " + distance
                    + " / 入っている: " + area.IsInside
                    + " / 有効: " + area.isActiveAndEnabled);
            }


            Debug.Log(sb.ToString());
        }
    }
}
