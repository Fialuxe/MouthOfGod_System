using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MouthOfGod.Village.Editor
{
    /// <summary>
    /// 村の置き物の Prefab を、素材（FBX）から Assets/Resources/Prefabs/Village/ に作る。
    /// 1 回実行して、できた Prefab をコミットする（Play 時には使わない）。
    /// 既にある Prefab は上書きしない。ただし村人と Villager.controller は、毎回作り直す（悩む動き・印を変えたら反映するため）。
    /// 素材の置き場所は docs/quaternius-assets.md。
    /// </summary>
    public static class VillagePrefabGenerator
    {
        const string Quaternius = "Assets/ArtisticResources/ThirdParty/Quaternius/";
        const string OutputFolder = "Assets/Resources/Prefabs/Village/";

        // Prefab 名（下書きの Draft/VillageLayout と、Village.prefab で使う名前）→ FBX の場所。House_Sample は既にある Prefab を使うので載せない。
        static readonly Dictionary<string, string> Sources = new Dictionary<string, string>
        {
            { "Well", Quaternius + "Farm Buildings - Sept 2018/FBX/Well.fbx" },
            { "Windmill", Quaternius + "Farm Buildings - Sept 2018/FBX/Windmill.fbx" },
            { "Barn", Quaternius + "Farm Buildings - Sept 2018/FBX/Barn.fbx" },
            { "Fence", Quaternius + "Farm Buildings - Sept 2018/FBX/Fence.fbx" },
            { "ChickenCoop", Quaternius + "Farm Buildings - Sept 2018/FBX/ChickenCoop.fbx" },
            { "Wagon", Quaternius + "Medieval Village MegaKit[Standard]/FBX/Prop_Wagon.fbx" },
            { "Crate", Quaternius + "Medieval Village MegaKit[Standard]/FBX/Prop_Crate.fbx" },
            { "Sheep", Quaternius + "Farm Animals by @Quaternius/FBX/Sheep.fbx" },
            { "Pig", Quaternius + "Farm Animals by @Quaternius/FBX/Pig.fbx" },
            { "Casual_Male", Quaternius + "Ultimate Animated Character Pack - Nov 2019/FBX/Casual_Male.fbx" },
            { "Casual_Female", Quaternius + "Ultimate Animated Character Pack - Nov 2019/FBX/Casual_Female.fbx" },
            { "OldClassy_Male", Quaternius + "Ultimate Animated Character Pack - Nov 2019/FBX/OldClassy_Male.fbx" },
            { "Worker_Male", Quaternius + "Ultimate Animated Character Pack - Nov 2019/FBX/Worker_Male.fbx" },
            { "Worker_Female", Quaternius + "Ultimate Animated Character Pack - Nov 2019/FBX/Worker_Female.fbx" },
            { "Carrot", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Carrot.fbx" },
            { "Turnip", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Turnip.fbx" },
            { "Pumpkin", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Pumpkin.fbx" },
            { "Tomato", Quaternius + "Ultimate Food Pack - Oct 2019/FBX/Tomato.fbx" },
        };

        // 村人。同じパックの人物は骨組み（CharacterArmature）が同じなので、Casual_Male のクリップと Villager.controller を共有する。
        static readonly HashSet<string> Villagers = new HashSet<string>
        {
            "Casual_Male", "Casual_Female", "OldClassy_Male", "Worker_Male", "Worker_Female",
        };
        const string ClipSource = Quaternius + "Ultimate Animated Character Pack - Nov 2019/FBX/Casual_Male.fbx";
        const string ControllerPath = OutputFolder + "Villager.controller";

        /// <summary>村人の Prefab の中の、悩んでいる印（頭の上）の名前。大きさを合わせるときは数えない。</summary>
        public const string WorryMarkName = "WorryMark";

        // 畑は素材に無いので、箱を組み合わせて作る（土の板と、畝 4 本）。
        // 畝の位置と高さは Draft/VillageLayout.RidgeX / RidgeTop と合わせる（畑の一辺を 1 とした値）。
        const string FarmlandName = "Farmland";

        // 素材に無いので、箱・円柱・球を組み合わせて作る物。
        static readonly Dictionary<string, System.Func<GameObject>> Built = new Dictionary<string, System.Func<GameObject>>
        {
            { FarmlandName, CreateFarmland },
            { "Path", CreatePath },
            { "Plaza", CreatePlaza },
            { "Tree", () => CreateTree("Tree", "Tree_Leaves", new Color(0.30f, 0.55f, 0.25f)) },
            { "Tree_Bare", () => CreateTree("Tree_Bare", "Tree_LeavesDry", new Color(0.55f, 0.42f, 0.22f)) },
            { "Crop_Withered", CreateWitheredCrop },
            { "Crop_Sprout", CreateSprout },
            { RuinedHouseName, CreateRuinedHouse },
            { "Lantern", CreateLantern },
            { "Flowerbed", CreateFlowerbed },
        };

        // 屋根が無い家。House_Sample（既にある Prefab）から、屋根の部品（名前が Roof_ で始まる物）を外して作る。
        // 飢饉の間の家で、ステップが進むと House_Sample に差し替わる（屋根が直る）。
        const string RuinedHouseName = "House_Ruined";
        const string HouseSamplePath = OutputFolder + "House_Sample.prefab";
        static readonly float[] RidgeX = { -0.375f, -0.125f, 0.125f, 0.375f };

        // 村人のアニメーション（TPO: 飢饉の村で、食べ物を待つ → 届いたら喜ぶ）。状態の切り替えは VillagerMood。
        //   Worried: 悩んでいる。Idle をゆっくり（0.6 倍）再生して、肩を落として立っている感じにする（止まって見えないように動きは残す）。
        //   Slump: 悩んでいる間に、ときどきがっくりうなだれる。Defeat（ループさせず、最後の姿勢で止まる）。
        //   Idle: 元気。Victory: 届いたときに 1 回 → Idle に戻る。
        //   使わない: Walk/Run（どこかへ行ってしまう）、Death（暗すぎる）、Punch/SwordSlash/Shoot/Roll/RecieveHit（戦闘）、
        //   PickUp（拾う物が無いと不自然）。
        static AnimatorController BuildVillagerController()
        {
            var fbxPath = ClipSource;
            SetLoop(fbxPath, "Idle", true);
            SetLoop(fbxPath, "Victory", false);
            SetLoop(fbxPath, "Defeat", false);

            var idle = LoadClip(fbxPath, "Idle");
            var victory = LoadClip(fbxPath, "Victory");
            var defeat = LoadClip(fbxPath, "Defeat");
            if (idle == null || victory == null || defeat == null)
            {
                Debug.LogWarning($"[Village] {fbxPath} に Idle / Victory / Defeat のクリップが無い。アニメーションは付けない");
                return null;
            }

            AssetDatabase.DeleteAsset(ControllerPath); // 毎回作り直す
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Worried", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Slump", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Delivered", AnimatorControllerParameterType.Trigger);
            var machine = controller.layers[0].stateMachine;
            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            var worriedState = machine.AddState("Worried");
            worriedState.motion = idle;
            worriedState.speed = 0.6f;
            var slumpState = machine.AddState("Slump");
            slumpState.motion = defeat;
            var victoryState = machine.AddState("Victory");
            victoryState.motion = victory;
            machine.defaultState = idleState;

            AddTransition(idleState, worriedState, AnimatorConditionMode.If, "Worried", 0.3f);
            AddTransition(worriedState, idleState, AnimatorConditionMode.IfNot, "Worried", 0.3f);
            AddTransition(worriedState, slumpState, AnimatorConditionMode.If, "Slump", 0.3f);
            AddTransition(slumpState, worriedState, AnimatorConditionMode.IfNot, "Slump", 0.6f); // ゆっくり体を起こす
            AddTransition(slumpState, idleState, AnimatorConditionMode.IfNot, "Worried", 0.6f);
            AddTransition(idleState, victoryState, AnimatorConditionMode.If, "Delivered", 0.2f);
            var back = victoryState.AddTransition(idleState);
            back.hasExitTime = true;
            back.exitTime = 1f;
            back.duration = 0.2f;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void AddTransition(AnimatorState from, AnimatorState to, AnimatorConditionMode mode, string parameter, float duration)
        {
            var t = from.AddTransition(to);
            t.AddCondition(mode, 0f, parameter);
            t.hasExitTime = false;
            t.duration = duration;
        }

        static void AttachVillager(GameObject instance, AnimatorController controller)
        {
            if (controller != null)
            {
                var animator = instance.GetComponentInChildren<Animator>();
                if (animator == null) animator = instance.AddComponent<Animator>(); // Unity の null は ?? で判定できない
                animator.runtimeAnimatorController = controller;
            }
            var mood = instance.GetComponent<VillagerMood>();
            if (mood == null) mood = instance.AddComponent<VillagerMood>();
            mood.WorryMark = AddWorryMark(instance);
        }

        // 悩んでいる印: 頭の上の、暗くて太い縦線 3 本（漫画の「どんより」）。頭の骨に付けて、うなだれても頭に付いてくる。
        // 大きさは人物の背の高さに合わせる。VillageBaker は、大きさを合わせるときにこの印を数えない。印の出し入れは VillagerMood。
        static GameObject AddWorryMark(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            var height = bounds.size.y;

            var head = FindDeep(instance.transform, "Head") ?? instance.transform;
            var mark = new GameObject(WorryMarkName);
            mark.transform.position = new Vector3(head.position.x, bounds.max.y + height * 0.25f, head.position.z);
            var gloom = LoadOrCreateMaterial("WorryMark", new Color(0.25f, 0.22f, 0.45f));
            for (var i = -1; i <= 1; i++)
                Add(PrimitiveType.Cube, mark.transform, gloom, new Vector3(i * height * 0.2f, 0f, 0f), new Vector3(0.09f, 0.45f, 0.09f) * height);
            mark.transform.SetParent(head, worldPositionStays: true);
            return mark;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform child in t)
            {
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // FBX の中のクリップは "CharacterArmature|Idle" の名前。
        static AnimationClip LoadClip(string fbxPath, string name)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__") && clip.name.EndsWith("|" + name))
                    return clip;
            return null;
        }

        static void SetLoop(string fbxPath, string name, bool loop)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            var changed = false;
            foreach (var clip in clips)
                if (clip.name.EndsWith("|" + name) && clip.loopTime != loop) { clip.loopTime = loop; changed = true; }
            if (!changed) return;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }

        static GameObject CreateFarmland()
        {
            var root = new GameObject(FarmlandName);
            var soil = LoadOrCreateMaterial("Farmland_Soil", new Color(0.33f, 0.22f, 0.13f));
            var ridge = LoadOrCreateMaterial("Farmland_Ridge", new Color(0.45f, 0.30f, 0.17f));
            AddBox(root.transform, soil, new Vector3(0f, 0.015f, 0f), new Vector3(1f, 0.03f, 1f));
            foreach (var x in RidgeX)
                AddBox(root.transform, ridge, new Vector3(x, 0.045f, 0f), new Vector3(0.14f, 0.03f, 0.92f));
            return root;
        }

        // 道: 一辺 1、厚さ 3 mm の土の板。底面が原点。VillageBaker が幅と長さに伸ばす（VillageItem.Length / Width）。
        static GameObject CreatePath()
        {
            var root = new GameObject("Path");
            var dirt = LoadOrCreateMaterial("Path_Dirt", new Color(0.62f, 0.50f, 0.34f));
            AddBox(root.transform, dirt, new Vector3(0f, 0.0015f, 0f), new Vector3(1f, 0.003f, 1f));
            return root;
        }

        // 広場: 直径 1 の円盤。道より 1 mm 高くして、重なっても道がちらつかないようにする。
        static GameObject CreatePlaza()
        {
            var root = new GameObject("Plaza");
            var stone = LoadOrCreateMaterial("Plaza_Stone", new Color(0.70f, 0.66f, 0.58f));
            Add(PrimitiveType.Cylinder, root.transform, stone, new Vector3(0f, 0.002f, 0f), new Vector3(1f, 0.002f, 1f));
            return root;
        }

        // 木: 幹（円柱）と、葉（球 2 つ）。素材の木が無いので、ローポリの見た目に合わせた簡単な形にする。
        // 飢饉の間の木（Tree_Bare）は、同じ形で葉が茶色い。
        static GameObject CreateTree(string name, string leavesMaterial, Color leavesColor)
        {
            var root = new GameObject(name);
            var bark = LoadOrCreateMaterial("Tree_Bark", new Color(0.40f, 0.27f, 0.16f));
            var leaves = LoadOrCreateMaterial(leavesMaterial, leavesColor);
            Add(PrimitiveType.Cylinder, root.transform, bark, new Vector3(0f, 0.2f, 0f), new Vector3(0.12f, 0.2f, 0.12f));
            Add(PrimitiveType.Sphere, root.transform, leaves, new Vector3(0f, 0.6f, 0f), new Vector3(0.6f, 0.55f, 0.6f));
            Add(PrimitiveType.Sphere, root.transform, leaves, new Vector3(0.08f, 0.82f, 0.04f), new Vector3(0.4f, 0.36f, 0.4f));
            return root;
        }

        // 枯れた作物: 倒れかけた乾いた茎 3 本。
        static GameObject CreateWitheredCrop()
        {
            var root = new GameObject("Crop_Withered");
            var straw = LoadOrCreateMaterial("Crop_Withered", new Color(0.52f, 0.43f, 0.26f));
            Add(PrimitiveType.Cylinder, root.transform, straw, new Vector3(0f, 0.3f, 0f), new Vector3(0.06f, 0.32f, 0.06f), new Vector3(25f, 0f, 0f));
            Add(PrimitiveType.Cylinder, root.transform, straw, new Vector3(0.1f, 0.25f, 0.05f), new Vector3(0.05f, 0.28f, 0.05f), new Vector3(-10f, 0f, 55f));
            Add(PrimitiveType.Cylinder, root.transform, straw, new Vector3(-0.08f, 0.15f, -0.05f), new Vector3(0.05f, 0.2f, 0.05f), new Vector3(70f, 30f, 0f));
            return root;
        }

        // 芽: 短い茎と、開いた双葉。
        static GameObject CreateSprout()
        {
            var root = new GameObject("Crop_Sprout");
            var green = LoadOrCreateMaterial("Crop_Sprout", new Color(0.45f, 0.75f, 0.30f));
            Add(PrimitiveType.Cylinder, root.transform, green, new Vector3(0f, 0.25f, 0f), new Vector3(0.08f, 0.25f, 0.08f));
            Add(PrimitiveType.Sphere, root.transform, green, new Vector3(0.22f, 0.55f, 0f), new Vector3(0.45f, 0.08f, 0.25f), new Vector3(0f, 0f, 20f));
            Add(PrimitiveType.Sphere, root.transform, green, new Vector3(-0.22f, 0.55f, 0f), new Vector3(0.45f, 0.08f, 0.25f), new Vector3(0f, 0f, -20f));
            return root;
        }

        // ランタン: 木の支柱と、暖かく光る玉。家が直ったときに戸口に出る（光って見えるが、ライトは使わない）。高さ 1。
        static GameObject CreateLantern()
        {
            var root = new GameObject("Lantern");
            var wood = LoadOrCreateMaterial("Tree_Bark", new Color(0.40f, 0.27f, 0.16f));
            var glow = LoadOrCreateGlowMaterial("Lantern_Glow", new Color(1f, 0.75f, 0.35f));
            Add(PrimitiveType.Cylinder, root.transform, wood, new Vector3(0f, 0.35f, 0f), new Vector3(0.08f, 0.35f, 0.08f));
            Add(PrimitiveType.Sphere, root.transform, glow, new Vector3(0f, 0.82f, 0f), new Vector3(0.28f, 0.28f, 0.28f));
            return root;
        }

        // 花壇: 低い土の台に、色とりどりの花。家が直ったときに出る。一辺 1。
        static GameObject CreateFlowerbed()
        {
            var root = new GameObject("Flowerbed");
            var soil = LoadOrCreateMaterial("Farmland_Soil", new Color(0.33f, 0.22f, 0.13f));
            var leaf = LoadOrCreateMaterial("Tree_Leaves", new Color(0.30f, 0.55f, 0.25f));
            AddBox(root.transform, soil, new Vector3(0f, 0.06f, 0f), new Vector3(1f, 0.12f, 0.5f));
            var colors = new[] { new Color(0.95f, 0.45f, 0.5f), new Color(0.98f, 0.85f, 0.35f), new Color(0.95f, 0.95f, 0.95f), new Color(0.65f, 0.5f, 0.9f) };
            for (var i = 0; i < 6; i++)
            {
                var x = -0.4f + i * 0.16f;
                var z = (i % 2 == 0 ? -0.1f : 0.1f);
                Add(PrimitiveType.Sphere, root.transform, leaf, new Vector3(x, 0.17f, z), new Vector3(0.12f, 0.1f, 0.12f));
                Add(PrimitiveType.Sphere, root.transform, LoadOrCreateMaterial("Flower_" + (i % colors.Length), colors[i % colors.Length]),
                    new Vector3(x, 0.25f, z), new Vector3(0.1f, 0.1f, 0.1f));
            }
            return root;
        }

        // 光る色。URP の Lit に発光を足す（ライトは増やさないので、軽い）。
        static Material LoadOrCreateGlowMaterial(string name, Color color)
        {
            var material = LoadOrCreateMaterial(name, color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor("_EmissionColor", color * 1.5f);
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        static void AddBox(Transform parent, Material material, Vector3 position, Vector3 scale)
            => Add(PrimitiveType.Cube, parent, material, position, scale);

        static void Add(PrimitiveType type, Transform parent, Material material, Vector3 position, Vector3 scale, Vector3 euler = default)
        {
            var part = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = Quaternion.Euler(euler);
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        // URP の既定のマテリアルのシェーダーで、色だけ変えたマテリアルを作る。
        static Material LoadOrCreateMaterial(string name, Color color)
        {
            var path = OutputFolder + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var shader = pipeline != null ? pipeline.defaultMaterial.shader : Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static GameObject CreateRuinedHouse()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(HouseSamplePath);
            if (source == null)
            {
                Debug.LogWarning($"[Village] {HouseSamplePath} が無いので、{RuinedHouseName} は作れない");
                return new GameObject(RuinedHouseName);
            }
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = RuinedHouseName;
            var removed = 0;
            foreach (var t in instance.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == instance.transform || !t.name.StartsWith("Roof_")) continue;
                Object.DestroyImmediate(t.gameObject);
                removed++;
            }
            if (removed == 0) Debug.LogWarning($"[Village] {RuinedHouseName}: 名前が Roof_ で始まる部品が見つからない。House_Sample の中の名前を確かめる");
            return instance;
        }

        [MenuItem("MouthOfGod/Village/Generate Prefabs")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);
            int created = 0, skipped = 0;
            var controller = BuildVillagerController();
            foreach (var pair in Sources)
            {
                var path = OutputFolder + pair.Key + ".prefab";
                var villager = Villagers.Contains(pair.Key);
                if (File.Exists(path) && !villager) { skipped++; continue; }

                var model = AssetDatabase.LoadAssetAtPath<GameObject>(pair.Value);
                if (model == null)
                {
                    Debug.LogWarning($"[Village] 素材が見つからない: {pair.Value}");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (villager) AttachVillager(instance, controller);
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                Object.DestroyImmediate(instance);
                created++;
            }
            foreach (var pair in Built)
            {
                var path = OutputFolder + pair.Key + ".prefab";
                if (File.Exists(path)) { skipped++; continue; }
                var instance = pair.Value();
                PrefabUtility.SaveAsPrefabAsset(instance, path);
                Object.DestroyImmediate(instance);
                created++;
            }
            VillageFxGenerator.Generate();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Village] Prefab を {created} 件作った（既にあって飛ばした: {skipped} 件）→ {OutputFolder}");
        }
    }
}
