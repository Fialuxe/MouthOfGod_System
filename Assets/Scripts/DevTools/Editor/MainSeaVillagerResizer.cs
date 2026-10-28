using System.Collections.Generic;
using System.Linq;
using System.Text;
using MouthOfGod.Village;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouthOfGod.DevTools.Editor
{
    /// <summary>
    /// 共有シーン（Main）の海の村人の背の高さを、村の村人の平均に合わせる。高さはワールド座標で測る。
    /// 海の親は (0.54, 0.2, 0.54) とつぶれていて、海の村人のローカルの大きさは縦だけ大きくして打ち消している。
    /// そのため、ローカルの大きさに同じ倍率を 3 軸ともかけて、打ち消しを崩さないようにする。
    /// 変更は Main の上書きとして残し、Sea_work.prefab は変えない。
    /// 直した海は、バックアップとして独立した Prefab（Sea_work_VillagerResized）にも保存する。
    /// 何度実行しても同じ結果になる。
    /// </summary>
    public static class MainSeaVillagerResizer
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string VillagePath = "Assets/Resources/Prefabs/Misc/Village.prefab";
        const string SeaPath = "Assets/Resources/Prefabs/Sea/Sea_work.prefab";
        const string BackupPath = "Assets/Resources/Prefabs/Sea/Sea_work_VillagerResized.prefab";

        [MenuItem("MouthOfGod/Dev/Match Main Sea Villager Height to Village")]
        public static void Resize()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject village = FindInstanceRoot(scene, VillagePath);
            GameObject sea = FindInstanceRoot(scene, SeaPath);
            if (village == null || sea == null)
            {
                Debug.LogError($"[MainSeaVillagerResizer] Main に村（{VillagePath}）か海（{SeaPath}）が見つかりません。");
                return;
            }

            List<float> villageHeights = Villagers(village).Select(Height).Where(h => h > 0f).ToList();
            if (villageHeights.Count == 0)
            {
                Debug.LogError("[MainSeaVillagerResizer] 村の村人の高さを測れません。");
                return;
            }
            float target = villageHeights.Average();

            var sb = new StringBuilder();
            sb.AppendLine($"[MainSeaVillagerResizer] 目標の高さ（村 {villageHeights.Count} 体の平均、ワールド）: {target:F4}");

            // バックアップの Prefab に写すため、海の根からの道筋と、直したあとのローカルの大きさを覚えておく。
            var scales = new Dictionary<string, Vector3>();
            foreach (Transform villager in Villagers(sea))
            {
                float before = Height(villager);
                if (before <= 0f)
                {
                    sb.AppendLine($"  - {villager.name}: 高さを測れないので、そのままにします。");
                    continue;
                }
                Undo.RecordObject(villager, "Match Sea Villager Height");
                villager.localScale *= target / before;
                PrefabUtility.RecordPrefabInstancePropertyModifications(villager);
                scales[AnimationUtility.CalculateTransformPath(villager, sea.transform)] = villager.localScale;
                sb.AppendLine($"  - {villager.name}: {before:F4} → {Height(villager):F4}（ローカル {villager.localScale.ToString("F5")}）");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            SaveBackup(scene, scales);
            sb.AppendLine($"バックアップの Prefab を保存しました: {BackupPath}");
            Debug.Log(sb.ToString());
        }

        // Sea_work を一時的に置き、村人の大きさを写して、独立した Prefab として保存する。中の Prefab のつながりは残す。
        static void SaveBackup(Scene scene, Dictionary<string, Vector3> scales)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SeaPath);
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            try
            {
                PrefabUtility.UnpackPrefabInstance(temp, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                foreach (KeyValuePair<string, Vector3> pair in scales)
                {
                    Transform t = temp.transform.Find(pair.Key);
                    if (t != null) t.localScale = pair.Value;
                    else Debug.LogWarning($"[MainSeaVillagerResizer] バックアップに {pair.Key} が見つかりません。");
                }
                PrefabUtility.SaveAsPrefabAsset(temp, BackupPath);
            }
            finally
            {
                Object.DestroyImmediate(temp);
            }
        }

        static IEnumerable<Transform> Villagers(GameObject root)
            => root.GetComponentsInChildren<VillagerMood>(true).Select(v => v.transform);

        // 体の SkinnedMeshRenderer だけで、ワールドの縦の長さを測る。心配マークなどの飾りは入れない。
        static float Height(Transform villager)
        {
            SkinnedMeshRenderer[] renderers = villager.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0) return 0f;
            Bounds bounds = renderers[0].bounds;
            foreach (SkinnedMeshRenderer r in renderers) bounds.Encapsulate(r.bounds);
            return bounds.size.y;
        }

        // その Prefab の、いちばん上の印（インスタンスの根）を探す。
        static GameObject FindInstanceRoot(Scene scene, string assetPath)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (!PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
                    if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == assetPath) return t.gameObject;
                }
            }
            return null;
        }
    }
}
