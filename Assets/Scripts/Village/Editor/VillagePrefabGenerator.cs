using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.Village.Editor
{
    /// <summary>
    /// VillageLayout が使う Prefab を、素材（FBX）から Assets/Resources/Prefabs/Village/ に作る。
    /// 1 回実行して、できた Prefab をコミットする（Play 時には使わない）。既にある Prefab は上書きしない。
    /// 素材の置き場所は docs/quaternius-assets.md。
    /// </summary>
    public static class VillagePrefabGenerator
    {
        const string Quaternius = "Assets/ArtisticResources/ThirdParty/Quaternius/";
        const string OutputFolder = "Assets/Resources/Prefabs/Village/";

        // Prefab 名（VillageLayout で使う名前）→ FBX の場所。House_Sample は既にある Prefab を使うので載せない。
        static readonly Dictionary<string, string> Sources = new Dictionary<string, string>
        {
            { "Well", Quaternius + "Farm Buildings - Sept 2018/FBX/Well.fbx" },
            { "Windmill", Quaternius + "Farm Buildings - Sept 2018/FBX/Windmill.fbx" },
            { "Barn", Quaternius + "Farm Buildings - Sept 2018/FBX/Barn.fbx" },
            { "Fence", Quaternius + "Farm Buildings - Sept 2018/FBX/Fence.fbx" },
            { "Casual_Male", Quaternius + "Ultimate Animated Character Pack - Nov 2019/FBX/Casual_Male.fbx" },
            { "Carrot", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Carrot.fbx" },
            { "Turnip", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Turnip.fbx" },
            { "Pumpkin", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Pumpkin.fbx" },
        };

        [MenuItem("MouthOfGod/Village/Generate Prefabs")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);
            int created = 0, skipped = 0;
            foreach (var pair in Sources)
            {
                var path = OutputFolder + pair.Key + ".prefab";
                if (File.Exists(path)) { skipped++; continue; }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);
                if (model == null)
                {
                    Debug.LogWarning($"[Village] 素材が見つからない: {pair.Value}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                Object.DestroyImmediate(instance);
                created++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[Village] Prefab を {created} 件作った（既にあって飛ばした: {skipped} 件）→ {OutputFolder}");
        }
    }
}
