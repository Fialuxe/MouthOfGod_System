using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MouthOfGod.Food;

namespace MouthOfGod.Village.Editor.Draft
{
    /// <summary>
    /// 下書きの配置（<see cref="VillageLayout"/>）から、村のフィールドの Prefab（Village.prefab）を作る。
    /// 作った後は Prefab を開いて手で直す。もう一度 Bake すると手で直した分が消えるので、上書きの前に確かめる。
    ///
    /// できる Prefab の形:
    ///   Village（<see cref="FieldStages"/>・<see cref="SurfaceAnchor"/>。シーンの Village の地面の子に置く）
    ///     ├ 置き物・村人（素材の Prefab のインスタンス。1 = 1 m。はじめからある物）
    ///     └ Steps
    ///         ├ 1 Pumpkin（<see cref="FieldStep"/>: 欲しい物・困りごとが解ける人・消す物）
    ///         │   └ 済むと出る物
    ///         └ 2 Carrot …
    /// </summary>
    public static class VillageBaker
    {
        const string PartsFolder = "Assets/Resources/Prefabs/Village/";
        const string OutputPath = PartsFolder + "Village.prefab";
        const string DoneClipPath = "Assets/ArtisticResources/ThirdParty/Kenney/Audio/jingles_PIZZI02.ogg";

        const string FoodFolder = "Assets/Resources/Prefabs/Food/";
        static readonly (string Texture, string FoodId)[] FoodTextures =
        {
            ("placeholder-a", "carrot"),
            ("placeholder-b", "turnip"),
            ("placeholder-c", "pumpkin"),
        };

        [MenuItem("MouthOfGod/Village/Bake Draft Village Prefab")]
        public static void Bake()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(OutputPath) != null && !Application.isBatchMode
                && !EditorUtility.DisplayDialog("Village.prefab を作り直す",
                    $"{OutputPath} はもうあります。下書きから作り直すと、Prefab を手で直した分は消えます。作り直しますか？",
                    "作り直す", "やめる"))
                return;

            var field = VillageLayout.CreateDefault().Find(f => f.Id == VillageLayout.Village);
            var root = new GameObject("Village");
            var stages = root.AddComponent<FieldStages>();
            root.AddComponent<SurfaceAnchor>();
            AddDeliveryFx(root);
            AddDeliveryZone(root);
            AddFoodTable(root);
            var so = new SerializedObject(stages);
            so.FindProperty("fieldId").stringValue = field.Id;
            so.ApplyModifiedPropertiesWithoutUndo();

            // ステップ。Hierarchy の並び順がそのまま順番になる（番号は名前の目印だけ）
            var stepsRoot = new GameObject("Steps").transform;
            stepsRoot.SetParent(root.transform, false);
            var steps = new List<FieldStep>();
            for (var i = 0; i < VillageLayout.Steps.Count; i++)
            {
                var stepGo = new GameObject($"{i + 1} {VillageLayout.Steps[i].Name}");
                stepGo.transform.SetParent(stepsRoot, false);
                var step = stepGo.AddComponent<FieldStep>();
                step.Food = VillageLayout.Steps[i].Food;
                steps.Add(step);
            }
            var helped = steps.Select(_ => new List<VillagerMood>()).ToList();
            var hidden = steps.Select(_ => new List<GameObject>()).ToList();

            var missing = 0;
            foreach (var item in field.Items)
            {
                var parent = item.AppearsAt > 0 ? steps[item.AppearsAt - 1].transform : root.transform;
                var go = CreateItem(item, root.transform, parent);
                if (go == null) { missing++; continue; }
                if (item.GoneAt > 0) hidden[item.GoneAt - 1].Add(go);
                if (item.IsVillager)
                {
                    var mood = go.GetComponent<VillagerMood>();
                    if (mood == null) mood = go.AddComponent<VillagerMood>(); // Unity の null は ?? で判定できない
                    if (item.HelpedAt > 0) helped[item.HelpedAt - 1].Add(mood);
                }
            }
            for (var i = 0; i < steps.Count; i++)
            {
                steps[i].SetHelped(helped[i].ToArray());
                steps[i].SetHideWhenDone(hidden[i].ToArray());
            }

            PrefabUtility.SaveAsPrefabAsset(root, OutputPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Village] {OutputPath} を作った（置き物 {field.Items.Count - missing} 個。Prefab が無くて飛ばした: {missing} 個）。" +
                      "シーンの Village（地面）の子に置く");
        }

        // 食べ物が落ちる場所。村（0.54 m 四方）の上の空間を、Is Trigger の箱で覆う。届け先は、親のフィールド（この Prefab の FieldStages）。
        static void AddDeliveryZone(GameObject root)
        {
            var box = root.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.54f, 0.4f, 0.54f);
            box.center = new Vector3(0f, 0.2f, 0f);
            root.AddComponent<DeliveryZone>();
        }

        // この村で出す食べ物の表。食感の ID（仮）と食べ物の Prefab を結ぶ。Prefab は作り直しても GUID が同じなので、つながったまま。
        // Field Id は村の Id。フィールドごとの表は、現在のフィールドを教える仕組みができるまで使われない。
        static void AddFoodTable(GameObject root)
        {
            var rows = new List<FoodRow>();
            foreach (var (texture, foodId) in FoodTextures)
            {
                var food = AssetDatabase.LoadAssetAtPath<GameObject>(FoodFolder + foodId + ".prefab");
                if (food == null) Debug.LogWarning($"[Village] 食べ物の Prefab が無い: {FoodFolder}{foodId}.prefab（Food の Generate Prefabs を先に実行する）");
                rows.Add(new FoodRow(texture, food));
            }
            root.AddComponent<FoodTable>().Configure(VillageLayout.Village, rows.ToArray());
        }

        // ステップが済んだときの音と喜びの粒。素材が無ければ、空のまま付ける（Inspector で後から入れる）。
        static void AddDeliveryFx(GameObject root)
        {
            var fx = root.AddComponent<DeliveryFx>();
            var so = new SerializedObject(fx);
            var joy = AssetDatabase.LoadAssetAtPath<GameObject>(PartsFolder + "Fx_Joy.prefab");
            so.FindProperty("joy").objectReferenceValue = joy != null ? joy.GetComponent<ParticleSystem>() : null;
            so.FindProperty("doneClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(DoneClipPath);
            so.ApplyModifiedPropertiesWithoutUndo();
            if (joy == null) Debug.LogWarning($"[Village] {PartsFolder}Fx_Joy.prefab が無いので、喜びの粒は空のまま（Generate Prefabs を先に実行する）");
        }

        // root は位置の基準（フィールドの中心）、parent は置く場所（root か、ステップ）。
        static GameObject CreateItem(VillageItem item, Transform root, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PartsFolder + item.Prefab + ".prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[Village] Prefab が無いので飛ばす: {PartsFolder}{item.Prefab}.prefab（先に Generate Prefabs を実行する）");
                return null;
            }

            // 素材の Prefab へのつながりを残す（素材の Prefab を直せば、村にも反映される）。
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            // 素材の根元の向き（FBX の軸の違いを直す回転）は残し、その上から Y で向きを付ける。
            go.transform.localRotation = Quaternion.Euler(0f, item.YawDegrees, 0f) * prefab.transform.localRotation;
            var target = root.TransformPoint(new Vector3(item.X, item.Y, item.Z));
            go.transform.position = target;

            if (item.IsEffect)
            {
                // 煙などは見た目の大きさが無い。大きさは合わせず、位置だけ置く。
            }
            else if (item.Length > 0f)
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
