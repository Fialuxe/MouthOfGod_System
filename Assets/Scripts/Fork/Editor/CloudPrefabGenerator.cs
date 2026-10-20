using System.IO;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.Fork.Editor
{
    /// <summary>
    /// 雲の Prefab を、白い球を重ねて Assets/Resources/Prefabs/Fork/Cloud.prefab に作る。
    /// 1 回実行して、できた Prefab をコミットする（Play 時には使わない）。既にある Prefab は上書きしない。
    /// 素材の雲が無いので仮の見た目にしている。素材ができたら、この Prefab の見た目だけを差し替える（Cloud と当たり判定は残す）。
    /// </summary>
    public static class CloudPrefabGenerator
    {
        const string OutputFolder = "Assets/Resources/Prefabs/Fork/";
        const string PrefabPath = OutputFolder + "Cloud.prefab";
        const string MaterialPath = OutputFolder + "Cloud.mat";

        // 球の位置と大きさ（m）。全体で、こぶし 2 つ分くらいの大きさにする。
        static readonly (Vector3 Position, float Size)[] Puffs =
        {
            (new Vector3(0f, 0f, 0f), 0.12f),
            (new Vector3(0.06f, -0.01f, 0.01f), 0.09f),
            (new Vector3(-0.06f, -0.015f, 0f), 0.085f),
            (new Vector3(0.01f, 0.04f, -0.01f), 0.08f),
        };

        const float ColliderRadius = 0.08f; // フォークの先が触れたら刺したとする範囲。見た目より少し大きくして、刺しやすくする

        [MenuItem("MouthOfGod/Fork/Generate Cloud Prefab")]
        public static void Generate()
        {
            if (File.Exists(PrefabPath))
            {
                Debug.Log($"[Fork] 雲の Prefab は既にあります（上書きしません）→ {PrefabPath}");
                return;
            }
            Directory.CreateDirectory(OutputFolder);

            var material = LoadOrCreateMaterial();
            var root = new GameObject("Cloud");
            foreach (var puff in Puffs)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.transform.SetParent(root.transform, false);
                sphere.transform.localPosition = puff.Position;
                sphere.transform.localScale = Vector3.one * puff.Size;
                sphere.GetComponent<Renderer>().sharedMaterial = material;
            }
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = ColliderRadius;
            root.AddComponent<Cloud>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Fork] 雲の Prefab を作りました → {PrefabPath}");
        }

        // URP の既定のマテリアルのシェーダーで、白に近い色のマテリアルを作る。
        static Material LoadOrCreateMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            var color = new Color(0.95f, 0.96f, 1f);
            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var shader = pipeline != null ? pipeline.defaultMaterial.shader : Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }
    }
}
