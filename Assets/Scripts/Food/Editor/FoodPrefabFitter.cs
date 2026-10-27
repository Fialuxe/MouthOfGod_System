using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.Food.Editor
{
    /// <summary>
    /// 手で作った食べ物の Prefab（FoodPrefabGenerator を通っていないもの）の大きさを、その場で合わせる。
    /// 一番長い辺が目標の長さ（m）になるよう、一番上の大きさを同じ倍率で変える。GUID も Mass などの設定も変わらない。
    /// すでに目標どおりの Prefab は、何もしない（何度実行しても同じ）。
    /// 食べ物を飛ばして見やすい大きさは、FoodPrefabGenerator の Sources と同じ考えで、長さは仮。
    /// </summary>
    public static class FoodPrefabFitter
    {
        const string Folder = "Assets/Resources/Prefabs/Food/";
        const float Tolerance = 0.01f; // 目標との差が 1% 以内なら、合っているとみなす
        const float Mass = 0.3f; // kg。FoodPrefabGenerator と同じ

        // 食べ物の ID → 一番長い辺の長さ（m）。
        internal static readonly Dictionary<string, float> Targets = new Dictionary<string, float>
        {
            { "chestnut", 0.05f },
            { "chicken", 0.15f },
            { "mushroom", 0.06f },
            { "shrimp", 0.1f },
            { "shell", 0.08f },
        };

        [MenuItem("MouthOfGod/Food/Fit Prefab Sizes")]
        public static void Fit()
        {
            int changed = 0, kept = 0;
            foreach (var pair in Targets)
            {
                var path = Folder + pair.Key + ".prefab";
                if (FitOne(path, pair.Value)) changed++; else kept++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Food] 大きさを合わせた: {changed} 件（もう合っていた: {kept} 件）→ {Folder}");
        }

        // 合わせたら true。Prefab が無い、または、もう合っているなら false。
        static bool FitOne(string path, float target)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[Food] Prefab が見つからない: {path}");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var bounds = BoundsOf(root);
                var longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (longest <= 0f)
                {
                    Debug.LogWarning($"[Food] 描画する物が無く、大きさを測れない: {path}");
                    return false;
                }
                if (Mathf.Abs(longest - target) <= target * Tolerance) return false;

                root.transform.localScale *= target / longest;
                bounds = BoundsOf(root);

                var collider = root.GetComponent<BoxCollider>();
                if (collider == null) collider = root.AddComponent<BoxCollider>();
                var scale = root.transform.lossyScale;
                collider.center = root.transform.InverseTransformPoint(bounds.center);
                collider.size = new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z);

                if (root.GetComponent<Rigidbody>() == null) root.AddComponent<Rigidbody>().mass = Mass;

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }
    }
}
