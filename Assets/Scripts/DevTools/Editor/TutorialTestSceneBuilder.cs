using System.IO;
using MouthOfGod.Food;
using MouthOfGod.Fork;
using MouthOfGod.Tutorial;
using MouthOfGod.Tutorial.Editor;
using MouthOfGod.UI;
using MouthOfGod.Village;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouthOfGod.DevTools.Editor
{
    /// <summary>
    /// Quest なしで、プロローグとチュートリアル 1〜4 を通して試すシーンを作る。メニュー「MouthOfGod/Dev/Build Tutorial Test Scene」。
    /// </summary>
    /// <remarks>
    /// - 作るシーンは <see cref="ScenePath"/>。毎回、作り直す（手で直した所は消える）。試したい値は、Play 中か、作ったあとに Inspector で変える。
    /// - 並べる物: 頭（キーボードとマウスで動く Main Camera）、フォークの先、雲、食べ物を生む装置と表、村と海のフィールド、立つ場所、巫女、影、合図で光る印、チュートリアル、状態の表示。
    /// - フィールドに届いたときの反応は、FieldStages が自分で付ける DeliveryFx が出す（違う物は煙を出して消える）。
    /// - チュートリアルは、Assets/Resources/Prefabs/Tutorial/ の Prefab を置く。セリフも Prefab に入っている。Prefab を直せば、作り直さなくても、このシーンに反映される。
    /// - 食感は MockDevice の既定（placeholder-a〜f）を使う。a〜d は各フィールドの食べ物（村の表）、e はリンゴ、f は魚（共通の表）。
    /// - バッチモードからも作れる: <c>-executeMethod MouthOfGod.DevTools.Editor.TutorialTestSceneBuilder.Build</c>
    /// </remarks>
    public static class TutorialTestSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Sandbox/tutorial_test.unity";

        const string FoodFolder = "Assets/Resources/Prefabs/Food/";
        const string MikoPrefab = "Assets/Resources/Prefabs/Miko/Miko.prefab";


        [MenuItem("MouthOfGod/Dev/Build Tutorial Test Scene")]
        static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("Build Tutorial Test Scene", ScenePath + " を作り直します。手で直した所は消えます。", "作り直す", "やめる"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            Ground();
            Camera camera = Head(out Transform mouth);
            ForkTipUnder(camera.transform, mouth);
            Clouds();
            Spawner(mouth);

            Transform villageStand = Field("village", new Vector3(0f, 0f, 1.5f), Vector3.zero, new Color(0.45f, 0.75f, 0.35f), "apple");
            Transform seaStand = Field("sea", new Vector3(3f, 0f, 1.5f), new Vector3(3f, 0f, 0f), new Color(0.25f, 0.5f, 0.9f), "fish");

            PlaceMiko();
            PlaceShadow();
            Indicators(camera.transform);
            TutorialRunner runner = Tutorials();
            Hud(runner, villageStand, seaStand);

            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[TutorialTestSceneBuilder] " + ScenePath + " を作りました。Play で、チュートリアルが始まります。");
        }

        static void Ground()
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(2f, 1f, 2f);
            Paint(ground, new Color(0.55f, 0.5f, 0.42f));
        }

        // 既定の Main Camera を、目の高さの頭にする。口の位置（MouthAnchor）を子に置く。
        static Camera Head(out Transform mouth)
        {
            Camera camera = Camera.main;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);
            camera.nearClipPlane = 0.02f;
            camera.gameObject.AddComponent<DesktopHead>();

            mouth = new GameObject("MouthAnchor").transform;
            mouth.SetParent(camera.transform, false);
            mouth.localPosition = new Vector3(0f, -0.08f, 0.08f);
            return camera;
        }

        // フォークの先。手の代わりに、頭の右下前に固定する。刺す・口に入れるは、P と M のキーで行う。
        static void ForkTipUnder(Transform head, Transform mouth)
        {
            var tip = new GameObject("ForkTip");
            tip.transform.SetParent(head, false);
            tip.transform.localPosition = new Vector3(0.12f, -0.25f, 0.35f);
            ForkTip fork = tip.AddComponent<ForkTip>();

            GameObject piece = Shape(PrimitiveType.Sphere, "LoadedPiece", tip.transform, Vector3.zero, Vector3.one * 0.04f, Color.white);
            piece.SetActive(false);

            Set(fork, "mouth", mouth);
            Set(fork, "loadedPiece", piece);
        }

        static void Clouds()
        {
            var ring = new GameObject("CloudRing");
            ring.AddComponent<CloudRing>();
            ring.AddComponent<CloudCues>();
        }

        // 食べ物を生む装置と、共通の表。共通の表は、どこに立っていても生まれる物（e: リンゴ、f: 魚）だけを持つ。
        static void Spawner(Transform mouth)
        {
            var go = new GameObject("FoodSpawner");
            FoodSpawner spawner = go.AddComponent<FoodSpawner>();
            Set(spawner, "spawnPoint", mouth);

            Table(go, "", new[,] { { "placeholder-e", "apple" }, { "placeholder-f", "fish" } });
        }

        // 「食感 → 食べ物」の表を付ける。fieldId が空なら共通の表。
        static void Table(GameObject go, string fieldId, string[,] pairs)
        {
            FoodTable table = go.AddComponent<FoodTable>();
            var so = new SerializedObject(table);
            so.FindProperty("fieldId").stringValue = fieldId;
            SerializedProperty rows = so.FindProperty("rows");
            rows.arraySize = pairs.GetLength(0);
            for (int i = 0; i < pairs.GetLength(0); i++)
            {
                SerializedProperty row = rows.GetArrayElementAtIndex(i);
                row.FindPropertyRelative("texture").stringValue = pairs[i, 0];
                row.FindPropertyRelative("food").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(FoodFolder + pairs[i, 1] + ".prefab");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// フィールドを 1 つ作る。台・届く範囲・欲しい物のステップ・光る印・喜ぶ印・首を振る印を置く。
        /// 立つ場所（StandArea）は stand に置く。立つ場所の Transform を返す。
        /// </summary>
        static Transform Field(string id, Vector3 position, Vector3 stand, Color color, string food)
        {
            var root = new GameObject("Field_" + id);
            root.transform.position = position;

            Shape(PrimitiveType.Cube, "Base", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(1.4f, 0.1f, 1.4f), color);
            Label(root.transform, id, new Vector3(0f, 1.2f, 0f));

            var zone = root.AddComponent<BoxCollider>();
            zone.isTrigger = true;
            zone.center = new Vector3(0f, 0.6f, 0f);
            zone.size = new Vector3(1.4f, 1.2f, 1.4f);
            root.AddComponent<DeliveryZone>();

            var stages = root.AddComponent<FieldStages>();
            Set(stages, "fieldId", id);

            // 村だけ、本物の村（Village.prefab）と同じ専用の表を付ける。a〜d が村の食べ物になる。
            if (id == "village")
                Table(root, id, new[,] { { "placeholder-a", "carrot" }, { "placeholder-b", "turnip" }, { "placeholder-c", "pumpkin" }, { "placeholder-d", "tomato" } });

            var steps = new GameObject("Steps").transform;
            steps.SetParent(root.transform, false);
            var step = new GameObject("Step_" + food);
            step.transform.SetParent(steps, false);
            step.AddComponent<FieldStep>().Food = food;

            GameObject glow = Shape(PrimitiveType.Cylinder, "Glow", root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(1.5f, 0.02f, 1.5f), new Color(1f, 0.9f, 0.4f));
            glow.SetActive(false);

            // 届いたときの反応（喜ぶ・がっかりする）は、FieldStages が自分で付ける DeliveryFx が出す。合図で出すのは、光だけ。
            var reaction = root.AddComponent<CueReaction>();
            Entries(reaction,
                (id + ".glow", new[] { glow }, null, 0f),
                (id + ".glow-off", null, new[] { glow }, 0f));

            var standGo = new GameObject("Stand_" + id);
            standGo.transform.position = stand;
            GameObject footprint = Shape(PrimitiveType.Cylinder, "Footprint", standGo.transform, new Vector3(0f, 0.01f, 0f), new Vector3(0.5f, 0.005f, 0.5f), new Color(1f, 0.85f, 0.2f));
            StandArea area = standGo.AddComponent<StandArea>();
            Set(area, "fieldId", id);
            Set(area, "footprint", footprint);
            return standGo.transform;
        }

        static void PlaceMiko()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MikoPrefab);
            if (prefab == null)
            {
                Debug.LogWarning("[TutorialTestSceneBuilder] " + MikoPrefab + " がないので、巫女を置きません。セリフは字幕に出ません。");
                return;
            }
            var miko = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            miko.transform.SetPositionAndRotation(new Vector3(-0.9f, 1.3f, 1.6f), Quaternion.Euler(0f, 150f, 0f));

        }

        static void PlaceShadow()
        {
            new GameObject("FoodShadow").AddComponent<FoodShadow>();
        }

        // スタッフへの知らせと、ボタンの光。デバイスの代わりに、頭の右下に出す。
        static void Indicators(Transform head)
        {
            var root = new GameObject("Indicators");
            GameObject alert = Shape(PrimitiveType.Cube, "StaffAlert", head, new Vector3(-0.35f, 0.2f, 1f), Vector3.one * 0.08f, new Color(1f, 0.2f, 0.2f));
            GameObject button = Shape(PrimitiveType.Sphere, "ButtonGlow", head, new Vector3(0.2f, -0.3f, 0.45f), Vector3.one * 0.04f, new Color(1f, 0.9f, 0.3f));
            GameObject strong = Shape(PrimitiveType.Sphere, "ButtonGlowStrong", head, new Vector3(0.2f, -0.3f, 0.45f), Vector3.one * 0.08f, new Color(1f, 0.6f, 0.1f));
            alert.SetActive(false);
            button.SetActive(false);
            strong.SetActive(false);

            var reaction = root.AddComponent<CueReaction>();
            Entries(reaction,
                ("staff.alert", new[] { alert }, null, 3f),
                ("device.button.glow", new[] { button }, null, 0f),
                ("device.button.glow-strong", new[] { strong }, null, 0f),
                ("device.button.glow-off", null, new[] { button, strong }, 0f));
        }

        // チュートリアルは、Assets/Resources/Prefabs/Tutorial/ の Prefab を並べる。無い Prefab は、先に作る。
        static TutorialRunner Tutorials()
        {
            TutorialPrefabGenerator.GenerateMissing();

            var go = new GameObject("TutorialRunner");
            TutorialRunner runner = go.AddComponent<TutorialRunner>();
            Set(runner, "beginOnStart", true);
            Set(runner, "beginDelaySeconds", 1f);

            foreach (string name in TutorialPrefabGenerator.InOrder)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TutorialPrefabGenerator.PathOf(name));
                if (prefab == null)
                {
                    Debug.LogWarning("[TutorialTestSceneBuilder] " + TutorialPrefabGenerator.PathOf(name) + " がないので、置きません。");
                    continue;
                }
                PrefabUtility.InstantiatePrefab(prefab, go.transform);
            }
            return runner;
        }

        static void Hud(TutorialRunner runner, params Transform[] targets)
        {
            TutorialDebugHud hud = new GameObject("DebugHud").AddComponent<TutorialDebugHud>();
            Set(hud, "runner", runner);
            var so = new SerializedObject(hud);
            SerializedProperty list = so.FindProperty("teleportTargets");
            list.arraySize = targets.Length;
            for (int i = 0; i < targets.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = targets[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 小さな道具 ----

        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        // 当たり判定のない形。見た目だけに使う。
        static GameObject Shape(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 scale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            Paint(go, color);
            return go;
        }

        static void Paint(GameObject go, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void Label(Transform parent, string text, Vector3 localPosition)
        {
            GameObject go = Child(parent, "Label");
            go.transform.localPosition = localPosition;
            var mesh = go.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = 0.05f;
            mesh.fontSize = 64;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = Color.black;
        }

        static void Entries(CueReaction reaction, params (string cue, GameObject[] show, GameObject[] hide, float seconds)[] entries)
        {
            var so = new SerializedObject(reaction);
            SerializedProperty list = so.FindProperty("entries");
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("cue").stringValue = entries[i].cue;
                Objects(entry.FindPropertyRelative("show"), entries[i].show);
                Objects(entry.FindPropertyRelative("hide"), entries[i].hide);
                entry.FindPropertyRelative("seconds").floatValue = entries[i].seconds;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Objects(SerializedProperty array, GameObject[] objects)
        {
            objects ??= new GameObject[0];
            array.arraySize = objects.Length;
            for (int i = 0; i < objects.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void Set(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
