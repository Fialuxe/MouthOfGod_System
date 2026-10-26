using System.Collections.Generic;
using System.IO;
using MouthOfGod.Miko;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.Tutorial.Editor
{
    /// <summary>
    /// プロローグとチュートリアル 1〜4 の Prefab を、Assets/Resources/Prefabs/Tutorial/ に作る。メニュー「MouthOfGod/Tutorial/Generate Tutorial Prefabs」。
    /// 1 回実行して、できた Prefab をコミットする（Play 時には使わない）。既にある Prefab は上書きしない（担当者が直した所を消さないため）。
    /// </summary>
    /// <remarks>
    /// - 1 つの Prefab に、人形劇スクリプト、そのチュートリアルのセリフ（<see cref="MikoLineBook"/>）、そのチュートリアルだけの物を入れる。
    ///   担当者は、自分のチュートリアルの Prefab だけを直す（並列で作業できるように。docs/decisions.md 2026-10-25）。
    /// - セリフは見本（英語）。台本は docs/scenario-book.md の S1・S3〜S6。台本にない文は仮。
    /// - シーンの物（セリフの指さす物・行き先、動画のあいだ隠す村）は Prefab に入れられない。シーンに置いた Prefab の上で入れる。
    /// </remarks>
    public static class TutorialPrefabGenerator
    {
        public const string OutputFolder = "Assets/Resources/Prefabs/Tutorial/";
        const string OfferingMaterialPath = OutputFolder + "OfferingPlaceholder.mat";

        /// <summary>TutorialRunner の子に置く順。</summary>
        public static readonly string[] InOrder = { "Prologue", "T1Fork", "T2Spawn", "T3Recognize", "T4Deliver" };

        public static string PathOf(string name) => OutputFolder + name + ".prefab";

        // Prefab の名前、id、英文。
        static readonly string[,] Lines =
        {
            { "Prologue", "prologue-stand-up", "Please stand up and follow the booth staff." },
            { "T1Fork", "t1-pierce", "Take the fork in your hand. Pierce that glowing cloud." },
            { "T1Fork", "t1-mouth", "Now bring it to your mouth." },
            { "T1Fork", "t1-mouth-again", "Bring it to your mouth." },
            { "T1Fork", "t1-changing", "Keep it in your mouth. Feel it? The texture keeps changing." },
            { "T1Fork", "t1-button", "When it feels right, press the button." },
            { "T1Fork", "t1-good", "Good." },
            { "T2Spawn", "t2-remember", "Remember the apple the villagers offered? Pierce a cloud and put it in your mouth." },
            { "T2Spawn", "t2-remember-short", "Remember the apple the villagers offered? Try it again." },
            { "T2Spawn", "t2-hint-pierce", "Pierce a cloud with your fork." },
            { "T2Spawn", "t2-hint-mouth", "Bring it to your mouth." },
            { "T2Spawn", "t2-wait-apple", "As the feel changes, so does the blessing. Wait for the apple." },
            { "T2Spawn", "t2-wrong", "That too is a blessing — but now, the apple." },
            { "T2Spawn", "t2-born", "Look! A blessing is born from your mouth! Remember that feel." },
            { "T3Recognize", "t3-troubled", "Oh, the people by the sea are troubled. They want fish." },
            { "T3Recognize", "t3-which-feel", "Pierce a new cloud. Which feel was the fish?" },
            { "T3Recognize", "t3-wrong", "Not that one. Which feel was the fish?" },
            { "T3Recognize", "t3-fish-here", "That's the fish — but blessings fall where you stand. Fish belong in the sea." },
            { "T4Deliver", "t4-walk", "Walk to the sea, and create the fish there." },
            { "T4Deliver", "t4-here", "Now, create it here." },
            { "T4Deliver", "t4-not-here", "Not here — go to the place that glows." },
            { "T4Deliver", "t4-splendid", "Splendid!" },
            { "T4Deliver", "t4-true-god", "Find what each place needs, go there, and create it. Now you are a true god." },
        };

        [MenuItem("MouthOfGod/Tutorial/Generate Tutorial Prefabs")]
        static void GenerateFromMenu()
        {
            List<string> made = GenerateMissing();
            Debug.Log(made.Count == 0
                ? "[Tutorial] チュートリアルの Prefab は、すべて既にあります（上書きしません）→ " + OutputFolder
                : "[Tutorial] チュートリアルの Prefab を作りました → " + string.Join(", ", made));
        }

        /// <summary>まだ無い Prefab だけを作る。作った Prefab のパスを返す。</summary>
        public static List<string> GenerateMissing()
        {
            Directory.CreateDirectory(OutputFolder);
            var made = new List<string>();
            foreach (string name in InOrder)
            {
                string path = PathOf(name);
                if (File.Exists(path)) continue;

                GameObject root = Build(name);
                AddLines(root.AddComponent<MikoLineBook>(), name);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Object.DestroyImmediate(root);
                made.Add(path);
            }
            AssetDatabase.SaveAssets();
            return made;
        }

        static GameObject Build(string name)
        {
            var root = new GameObject(name);
            switch (name)
            {
                case "Prologue":
                    root.AddComponent<PrologueVideo>();
                    var screenGo = new GameObject("VideoScreen");
                    screenGo.transform.SetParent(root.transform, false);
                    var screen = screenGo.AddComponent<VideoScreen>();
                    var so = new SerializedObject(screen);
                    so.FindProperty("streamingAssetsFile").stringValue = "Begin_KeltPiano.mp4"; // Assets/StreamingAssets/ の導入の動画（仮。どちらにするかは未定）
                    so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                case "T1Fork":
                    root.AddComponent<T1Fork>();
                    break;
                case "T2Spawn":
                    var t2 = root.AddComponent<T2Spawn>();
                    var t2so = new SerializedObject(t2);
                    t2so.FindProperty("offering").objectReferenceValue = Offering(root.transform);
                    t2so.ApplyModifiedPropertiesWithoutUndo();
                    break;
                case "T3Recognize":
                    root.AddComponent<T3Recognize>();
                    break;
                case "T4Deliver":
                    root.AddComponent<T4Deliver>();
                    break;
            }
            return root;
        }

        // お供え物のリンゴの仮の見た目（赤い球）。体験者の正面の右上に浮かぶ位置にする。シーンでは、置いた場所に合わせて動かす。
        static GameObject Offering(Transform parent)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Offering";
            Object.DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(parent, false);
            sphere.transform.localPosition = new Vector3(0.4f, 1.5f, 0.9f);
            sphere.transform.localScale = Vector3.one * 0.12f;
            sphere.GetComponent<Renderer>().sharedMaterial = OfferingMaterial();
            sphere.SetActive(false);
            return sphere;
        }

        static Material OfferingMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(OfferingMaterialPath);
            if (existing != null) return existing;

            var color = new Color(0.85f, 0.1f, 0.1f);
            var pipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
            var shader = pipeline != null ? pipeline.defaultMaterial.shader : Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(material, OfferingMaterialPath);
            return material;
        }

        static void AddLines(MikoLineBook book, string prefab)
        {
            var so = new SerializedObject(book);
            SerializedProperty list = so.FindProperty("lines");
            list.arraySize = 0; // 見本の welcome などは入れない
            for (int i = 0; i < Lines.GetLength(0); i++)
            {
                if (Lines[i, 0] != prefab) continue;
                list.arraySize++;
                SerializedProperty line = list.GetArrayElementAtIndex(list.arraySize - 1);
                line.FindPropertyRelative("id").stringValue = Lines[i, 1];
                line.FindPropertyRelative("text").stringValue = Lines[i, 2];
                line.FindPropertyRelative("voice").objectReferenceValue = null;
                line.FindPropertyRelative("changeExpression").boolValue = false;
                line.FindPropertyRelative("point").boolValue = false;
                line.FindPropertyRelative("pointTarget").objectReferenceValue = null;
                line.FindPropertyRelative("moveTo").objectReferenceValue = null;
                line.FindPropertyRelative("speakAfterMove").boolValue = false;
                line.FindPropertyRelative("seconds").floatValue = 0f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
