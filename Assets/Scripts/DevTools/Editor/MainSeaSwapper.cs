using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouthOfGod.DevTools.Editor
{
    /// <summary>
    /// 共有シーン（Main）の仮の海（Misc/Sea(Temporal)）を、完成版の海（Sea/Sea_work）に入れ替える（#329・#338）。
    /// 仮の海は、村人も海の食べ物の表も持たない。そのため、吹き出しが村人から離れた所に出て、海では共通の表（e・f だけ）しか引けなかった。
    /// 位置・向き・親・兄弟の順・体験者の頭（StandArea の Head）は、仮の海から引き継ぐ。仮の海を指していた参照は、新しい海に付け替える。
    /// 1 回実行して、Main をコミットする。Main に仮の海が無ければ、何もしない（何度実行しても同じ）。
    /// </summary>
    public static class MainSeaSwapper
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string OldPath = "Assets/Resources/Prefabs/Misc/Sea(Temporal).prefab";
        const string NewPath = "Assets/Resources/Prefabs/Sea/Sea_work.prefab";
        const string NewName = "Sea";

        [MenuItem("MouthOfGod/Dev/Swap Main Sea (Temporal to Sea_work)")]
        public static void Swap()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject old = FindInstanceRoot(scene, OldPath);
            if (old == null)
            {
                Debug.Log("[MainSeaSwapper] Main に仮の海（Sea(Temporal)）が無いので、何もしません。");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NewPath);
            if (prefab == null)
            {
                Debug.LogError($"[MainSeaSwapper] 新しい海の Prefab が見つかりません: {NewPath}");
                return;
            }

            Transform oldTransform = old.transform;
            Transform parent = oldTransform.parent;
            Vector3 position = oldTransform.localPosition;
            Quaternion rotation = oldTransform.localRotation;
            int siblingIndex = oldTransform.GetSiblingIndex();
            Object head = ReadHead(old);

            var created = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(created, "Swap Main Sea");
            RemoveSurfaceAnchor(created); // 位置と大きさを強制するので、手で置けなくなる。仮の海にも付いていなかった
            created.transform.SetParent(parent, false);
            created.transform.localPosition = position;
            created.transform.localRotation = rotation;
            created.transform.localScale = oldTransform.localScale;
            created.transform.SetSiblingIndex(siblingIndex);
            created.name = NewName;
            WriteHead(created, head);

            int rewired = Rewire(scene, oldTransform, created.transform, old, created);
            Object.DestroyImmediate(old);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MainSeaSwapper] 海を入れ替えました（{OldPath} → {NewPath}）。付け替えた参照: {rewired} 件。位置と大きさを、Scene ビューで見て確かめてください。");
        }

        // すでに入れ替えたあとの Main を直す。SurfaceAnchor を外し、仮の海のときの位置と大きさに戻す。
        static readonly Vector3 PlacedPosition = new Vector3(2.9307847f, 4.955f, 0.02940923f);
        static readonly Vector3 PlacedScale = new Vector3(0.54f, 0.2f, 0.54f);

        [MenuItem("MouthOfGod/Dev/Fix Main Sea Placement (remove SurfaceAnchor)")]
        public static void FixPlacement()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject sea = FindInstanceRoot(scene, NewPath);
            if (sea == null)
            {
                Debug.LogWarning("[MainSeaSwapper] Main に新しい海（Sea_work）が無いので、何もしません。先に入れ替えを実行してください。");
                return;
            }

            RemoveSurfaceAnchor(sea);
            sea.transform.localPosition = PlacedPosition;
            sea.transform.localRotation = Quaternion.identity;
            sea.transform.localScale = PlacedScale;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MainSeaSwapper] 海の SurfaceAnchor を外し、位置 {PlacedPosition}・大きさ {PlacedScale} に戻しました。以後は、Inspector で動かせます。");
        }

        static void RemoveSurfaceAnchor(GameObject sea)
        {
            var anchor = sea.GetComponent<MouthOfGod.Village.SurfaceAnchor>();
            if (anchor != null) Undo.DestroyObjectImmediate(anchor);
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

        // StandArea の Head（体験者の頭）を読む。名前で探すのは、この道具が Village の型を参照しないため。
        static Object ReadHead(GameObject root)
        {
            Component stand = FindStandArea(root);
            if (stand == null) return null;
            return new SerializedObject(stand).FindProperty("head")?.objectReferenceValue;
        }

        static void WriteHead(GameObject root, Object head)
        {
            if (head == null) return;
            Component stand = FindStandArea(root);
            if (stand == null) return;
            var so = new SerializedObject(stand);
            SerializedProperty property = so.FindProperty("head");
            if (property == null) return;
            property.objectReferenceValue = head;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static Component FindStandArea(GameObject root)
        {
            foreach (Component c in root.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name == "StandArea") return c;
            return null;
        }

        // 古い海（の Transform と GameObject）を指している参照を、新しい海に付け替える。数を返す。
        static int Rewire(Scene scene, Transform oldTransform, Transform newTransform, GameObject oldObject, GameObject newObject)
        {
            int count = 0;
            var changed = new List<SerializedObject>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Component c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c == null || c is Transform || c.transform.IsChildOf(oldTransform)) continue;
                    var so = new SerializedObject(c);
                    bool any = false;
                    for (SerializedProperty it = so.GetIterator(); it.Next(true);)
                    {
                        if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                        Object value = it.objectReferenceValue;
                        if (value == null) continue;
                        if (value == oldTransform) { it.objectReferenceValue = newTransform; any = true; count++; }
                        else if (value == oldObject) { it.objectReferenceValue = newObject; any = true; count++; }
                    }
                    if (any) changed.Add(so);
                }
            }
            foreach (SerializedObject so in changed) so.ApplyModifiedPropertiesWithoutUndo();
            return count;
        }
    }
}
