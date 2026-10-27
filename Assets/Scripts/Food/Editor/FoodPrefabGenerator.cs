using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.Food.Editor
{
    /// <summary>
    /// 食べ物の Prefab を、素材（FBX）から Assets/Resources/Prefabs/Food/ に作る。
    /// 1 回実行して、できた Prefab をコミットする（Play 時には使わない）。既にある Prefab は上書きしない。
    /// Prefab のファイル名が食べ物の ID（FoodId）になるので、村の欲しい物（FieldStep の Food）と同じ小文字にする。
    /// 素材の置き場所は docs/quaternius-assets.md。
    /// </summary>
    public static class FoodPrefabGenerator
    {
        const string Quaternius = "Assets/ArtisticResources/ThirdParty/Quaternius/";
        const string OutputFolder = "Assets/Resources/Prefabs/Food/";

        // 食べ物の ID → FBX の場所と、一番長い辺の長さ（m）。長さは仮。口から飛ばして見やすい大きさにしている。
        // pumpkin / carrot / turnip / tomato は、村の欲しい物（Draft/VillageLayout）。fish / shrimp / shell は、海の産物。sheep / pig / apple / cow は、ほかのフィールドと試験用。
        static readonly Dictionary<string, (string Path, float Size)> Sources = new Dictionary<string, (string, float)>
        {
            { "carrot", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Carrot.fbx", 0.1f) },
            { "pumpkin", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Pumpkin.fbx", 0.15f) },
            { "turnip", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Turnip.fbx", 0.1f) },
            { "tomato", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Tomato.fbx", 0.06f) },
            { "sheep", (Quaternius + "Farm Animals by @Quaternius/FBX/Sheep.fbx", 0.2f) },
            { "pig", (Quaternius + "Farm Animals by @Quaternius/FBX/Pig.fbx", 0.2f) },
            { "apple", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Apple.fbx", 0.06f) },
            { "fish", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Fish.fbx", 0.15f) },
            { "cow", (Quaternius + "Farm Animals by @Quaternius/FBX/Cow.fbx", 0.225f) },
            // 海フィールドの産物（魚介）。素材の FBX は Prefabs/Sea に置いている。
            { "shrimp", ("Assets/Resources/Prefabs/Sea/low_poly_shrimp.fbx", 0.1f) },
            { "shell", ("Assets/Resources/Prefabs/Sea/low_poly_shell.fbx", 0.08f) },
        };

        const float Mass = 0.3f; // kg。飛び方は FoodSpawner の Launch Speed と、この重さで決まる

        // 一番上は空のオブジェクト（大きさ 1）にして、Rigidbody と箱の当たり判定を付ける。モデルはその子にし、大きさを合わせ、底を原点に置く。
        static GameObject Build(string id, GameObject model, float size)
        {
            var root = new GameObject(id);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.transform.SetParent(root.transform, false);

            var bounds = BoundsOf(instance);
            var longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            if (longest > 0f) instance.transform.localScale *= size / longest;
            bounds = BoundsOf(instance);
            instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            bounds = BoundsOf(instance);

            var collider = root.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = bounds.size;
            root.AddComponent<Rigidbody>().mass = Mass;
            return root;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }

        [MenuItem("MouthOfGod/Food/Generate Prefabs")]
        public static void Generate() => Run(overwrite: false);

        // 大きさ（Sources の長さ）を変えたあとに使う。ファイルを消さず、同じ場所に上書きするので、GUID が変わらない。
        // シーンの FoodTable など、Prefab を参照している所は、つながったまま。Prefab を手で直した所（Mass など）は、元に戻る。
        [MenuItem("MouthOfGod/Food/Regenerate Prefabs (Overwrite)")]
        public static void Regenerate()
        {
            if (!EditorUtility.DisplayDialog("食べ物の Prefab を作り直す",
                    "Assets/Resources/Prefabs/Food/ の Prefab を、すべて上書きします。\nGUID は変わりません。手で直した所（Mass など）は元に戻ります。",
                    "上書きする", "やめる")) return;
            Run(overwrite: true);
        }

        static void Run(bool overwrite)
        {
            Directory.CreateDirectory(OutputFolder);
            int created = 0, skipped = 0;
            foreach (var pair in Sources)
            {
                var path = OutputFolder + pair.Key + ".prefab";
                if (!overwrite && File.Exists(path)) { skipped++; continue; }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value.Path);
                if (model == null)
                {
                    Debug.LogWarning($"[Food] 素材が見つからない: {pair.Value.Path}");
                    continue;
                }

                var root = Build(pair.Key, model, pair.Value.Size);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                created++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Food] Prefab を {created} 件{(overwrite ? "上書きした" : "作った")}（既にあって飛ばした: {skipped} 件）→ {OutputFolder}");
        }
    }
}
