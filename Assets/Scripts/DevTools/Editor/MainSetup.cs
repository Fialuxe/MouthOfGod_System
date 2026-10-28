using MouthOfGod.Game;
using MouthOfGod.Village;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MouthOfGod.DevTools.Editor
{
    /// <summary>
    /// 共有シーン Main を、本編まで通して遊べる形にする。
    /// - 本編の進行役（<see cref="GameRunner"/>）が無ければ置く。チュートリアルが終わると、これが本編を始める。
    /// - すべてのフィールドの Max Open Requests を、そのフィールドのステップの数にする。欲しい物を、全部同時に出せるようにするため。
    ///   値は、シーンの上書き（Prefab override）として残す。フィールドの Prefab は変えない。
    /// Main を開いて 1 回実行し、シーンをコミットする。Main は共有シーンなので、Scene Fusion のセッションの中で実行する（CONTRIBUTING 第 3 章）。
    /// 何度実行しても同じになる。
    /// </summary>
    public static class MainSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("MouthOfGod/Dev/Set Up Main (GameRunner, Max Open Requests)")]
        public static void SetUp()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogWarning($"[MainSetup] {ScenePath} を開いてから実行してください（いま開いているのは {scene.path}）。");
                return;
            }

            if (Object.FindFirstObjectByType<GameRunner>(FindObjectsInactive.Include) == null)
            {
                var go = new GameObject("GameRunner");
                Undo.RegisterCreatedObjectUndo(go, "Add GameRunner");
                go.AddComponent<GameRunner>();
                Debug.Log("[MainSetup] GameRunner を置きました。");
            }
            else
            {
                Debug.Log("[MainSetup] GameRunner は、もうあります。");
            }

            foreach (var field in Object.FindObjectsByType<FieldStages>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                int steps = Mathf.Max(1, field.Steps.Count);
                var so = new SerializedObject(field);
                var max = so.FindProperty("maxOpenRequests");
                if (max.intValue != steps)
                {
                    Undo.RecordObject(field, "Set Max Open Requests");
                    max.intValue = steps;
                    so.ApplyModifiedProperties();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(field);
                }
                Debug.Log($"[MainSetup] {field.FieldId}（{field.name}）: Max Open Requests = {steps}（ステップの数）");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
