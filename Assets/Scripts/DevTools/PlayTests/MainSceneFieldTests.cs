#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MouthOfGod.Core;
using MouthOfGod.Food;
using MouthOfGod.Village;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MouthOfGod.DevTools.PlayTests
{
    // 本物の共有シーン（Main）を開き、各フィールドの「立つ場所」の中心に頭を置いて、立った扱いになるか・そのフィールドの食べ物が出るかを確かめる。
    // ヘッドセットの位置合わせは再現しない。シーンの部品のつなぎが正しいかだけを見る。
    public class MainSceneFieldTests
    {
        const string MainScene = "Assets/Scenes/Main.unity";
        static readonly string[] Textures = { "placeholder-a", "placeholder-b", "placeholder-c" };

        // フィールド ID → 食感 a〜c で出る食べ物（docs/field-setup.md の表）。
        static readonly Dictionary<string, string[]> Expected = new Dictionary<string, string[]>
        {
            { "village", new[] { "carrot", "turnip", "pumpkin" } },
            { "grassland", new[] { "pig", "cow", "sheep" } },
            { "mountain", new[] { "mushroom", "apple", "chestnut" } },
            { "sea", new[] { "shell", "shrimp", "fish" } },
        };

        [UnityTest]
        public IEnumerator 各フィールドの前に立つと_そのフィールドの食べ物が出る()
        {
            // ヘッドセットが無いと、Meta のパススルーがエラーログを出す。この確認と関係が無いので、失敗にしない。
            LogAssert.ignoreFailingMessages = true;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(MainScene, new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            yield return null;

            var spawner = Object.FindFirstObjectByType<FoodSpawner>();
            Assert.IsNotNull(spawner, "Main に FoodSpawner が無い");
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "Main に Main Camera が無い");

            StandArea[] areas = Object.FindObjectsByType<StandArea>(FindObjectsSortMode.None);
            var report = new StringBuilder("[MainSceneFieldTests]\n立つ場所: " + areas.Length + " 個\n");
            var problems = new List<string>();
            var seen = new HashSet<string>();

            foreach (StandArea area in areas)
            {
                string field = area.Field.Value;
                seen.Add(field);

                var so = new SerializedObject(area);

                // 立つ場所が見る頭は、空（Main Camera）か Main Camera そのものにする。リグの根元だと、HMD で歩いても動かず、立ったことにならない。
                var headTarget = so.FindProperty("head").objectReferenceValue as Transform;
                if (headTarget != null && headTarget != camera.transform)
                    problems.Add($"{field}: 立つ場所の Head が「{headTarget.name}」を指している（空か Main Camera にする）");

                Vector3 center = area.transform.TransformPoint(so.FindProperty("center").vector3Value);
                // カメラの位置は、OVR のリグが毎フレーム追跡値で上書きする。リグの根元ごと動かして、頭を中心へ運ぶ。
                Vector3 head = camera.transform.position;
                Vector3 move = new Vector3(center.x - head.x, 0f, center.z - head.z);
                camera.transform.root.position += move;
                yield return null;
                yield return null;

                string standing = Standing.Field.IsEmpty ? "なし" : Standing.Field.Value;
                report.AppendLine($"  {field}: 頭を中心 {center.ToString("F2")} に置く → Standing={standing} / 入っている={area.IsInside}");
                if (Standing.Field != area.Field) problems.Add($"{field}: 中心に立っても Standing が「{standing}」のまま");

                var got = new List<string>();
                foreach (string texture in Textures)
                {
                    GameObject food = spawner.Spawn(new TextureId(texture));
                    got.Add(food != null ? food.name : "(出ない)");
                    if (food != null) Object.Destroy(food);
                }
                report.AppendLine($"    a〜c → {string.Join(", ", got)}");
                if (Expected.TryGetValue(field, out string[] want) && !want.Equals(got) && string.Join(",", want) != string.Join(",", got))
                    problems.Add($"{field}: 期待 {string.Join(", ", want)}、実際 {string.Join(", ", got)}");

                camera.transform.root.position -= move;
                yield return null;
            }

            foreach (string field in Expected.Keys)
                if (!seen.Contains(field)) problems.Add($"{field}: Main に、このフィールドの立つ場所が無い");

            Debug.Log(report.ToString());
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }
    }
}
#endif
