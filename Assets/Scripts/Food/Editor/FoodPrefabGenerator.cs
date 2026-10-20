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
        // carrot / pumpkin / sheep / pig は、村の欲しい物（Draft/VillageLayout）。apple / fish / cow は、ほかのフィールドと試験用。
        static readonly Dictionary<string, (string Path, float Size)> Sources = new Dictionary<string, (string, float)>
        {
            { "carrot", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Carrot.fbx", 0.2f) },
            { "pumpkin", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Pumpkin.fbx", 0.3f) },
            { "sheep", (Quaternius + "Farm Animals by @Quaternius/FBX/Sheep.fbx", 0.4f) },
            { "pig", (Quaternius + "Farm Animals by @Quaternius/FBX/Pig.fbx", 0.4f) },
            { "apple", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Apple.fbx", 0.12f) },
            { "fish", (Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Fish.fbx", 0.3f) },
            { "cow", (Quaternius + "Farm Animals by @Quaternius/FBX/Cow.fbx", 0.45f) },
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
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);
            int created = 0, skipped = 0;
            foreach (var pair in Sources)
            {
                var path = OutputFolder + pair.Key + ".prefab";
                if (File.Exists(path)) { skipped++; continue; }

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
            Debug.Log($"[Food] Prefab を {created} 件作った（既にあって飛ばした: {skipped} 件）→ {OutputFolder}");
        }
    }
}
