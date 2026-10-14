using UnityEditor;
using UnityEngine;

namespace MouthOfGod.Village.Editor.Draft
{
    /// <summary>
    /// 下書きの配置（<see cref="VillageLayout"/>）から、村のフィールドの Prefab（Village.prefab）を作る。
    /// 作った後は Prefab を開いて手で直す。もう一度 Bake すると手で直した分が消えるので、上書きの前に確かめる。
    ///
    /// できる Prefab の形:
    ///   Village（<see cref="FieldStages"/>・<see cref="SurfaceAnchor"/>。シーンの Village の地面の子に置く）
    ///     └ 置き物（素材の Prefab のインスタンス。1 = 1 m）
    ///         段階で出入りする物には <see cref="StageVisibility"/>、村人には <see cref="VillagerMood"/> の設定が付く
    /// </summary>
    public static class VillageBaker
    {
        const string PartsFolder = "Assets/Resources/Prefabs/Village/";
        const string OutputPath = PartsFolder + "Village.prefab";

        [MenuItem("MouthOfGod/Village/Bake Draft Village Prefab")]
        public static void Bake()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath) != null
                && !EditorUtility.DisplayDialog("Village.prefab を作り直す",
                    $"{OutputPath} はもうあります。下書きから作り直すと、Prefab を手で直した分は消えます。作り直しますか？",
                    "作り直す", "やめる"))
                return;

            var field = VillageLayout.CreateDefault().Find(f => f.Id == VillageLayout.Village);
            var root = new GameObject("Village");
            var stages = root.AddComponent<FieldStages>();
            root.AddComponent<SurfaceAnchor>();
            var so = new SerializedObject(stages);
            so.FindProperty("fieldId").stringValue = field.Id;
            so.FindProperty("maxStage").intValue = VillageLayout.MaxStage;
            so.ApplyModifiedPropertiesWithoutUndo();

            var missing = 0;
            foreach (var item in field.Items)
                if (CreateItem(item, root.transform) == null) missing++;

            PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Village] {OutputPath} を作った（置き物 {field.Items.Count - missing} 個。Prefab が無くて飛ばした: {missing} 個）。" +
                      "シーンの Village（地面）の子に置く");
        }

        static GameObject CreateItem(VillageItem item, Transform root)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartsFolder + item.Prefab + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[Village] Prefab が無いので飛ばす: {PartsFolder}{item.Prefab}.prefab（先に Generate Prefabs を実行する）");
                return null;
            }

            // 素材の Prefab へのつながりを残す（素材の Prefab を直せば、村にも反映される）。
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            // 素材の根元の向き（FBX の軸の違いを直す回転）は残し、その上から Y で向きを付ける。
            go.transform.localRotation = Quaternion.Euler(0f, item.YawDegrees, 0f) * prefab.transform.localRotation;
            var target = root.TransformPoint(new Vector3(item.X, item.Y, item.Z));
            go.transform.position = target;

            if (item.Length > 0f)
            {
                // 道・広場は、一辺 1・底面が原点の板。そのまま縦横に伸ばす。
                go.transform.localScale = new Vector3(item.Width, 1f, item.Length);
            }
            else if (TryGetBounds(go, out var bounds))
            {
                // 素材は元の大きさも原点の位置もまちまち。見た目の一番長い辺を Size に合わせ、底面の中心を target に置く。
                var longest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                if (longest > 0f) go.transform.localScale *= item.Size / longest;
                TryGetBounds(go, out bounds);
                go.transform.position += target - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            if (item.FromStage > 0 || item.UntilStage != Stages.UntilEnd)
            {
                var visibility = go.AddComponent<StageVisibility>();
                visibility.FromStage = item.FromStage;
                visibility.UntilStage = item.UntilStage;
            }
            if (item.IsVillager)
            {
                var mood = go.GetComponent<VillagerMood>();
                if (mood == null) mood = go.AddComponent<VillagerMood>(); // Unity の null は ?? で判定できない
                mood.CalmFromStage = item.CalmFromStage;
            }
            return go;
        }

        // 見た目の外形。村人の頭の上の印（WorryMark）は数えない。
        static bool TryGetBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var found = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                if (IsInWorryMark(r.transform, go.transform)) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return found;
        }

        static bool IsInWorryMark(Transform t, Transform root)
        {
            for (; t != null && t != root; t = t.parent)
                if (t.name == VillagePrefabGenerator.WorryMarkName) return true;
            return false;
        }
    }
}
