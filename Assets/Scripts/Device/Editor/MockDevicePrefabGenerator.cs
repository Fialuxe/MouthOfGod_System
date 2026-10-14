using UnityEditor;
using UnityEngine;

namespace MouthOfGod.Device.Editor
{
    /// <summary>
    /// MockDevice の Prefab を Assets/Resources/Prefabs/Device/ に作る。1 回実行して、できた Prefab をコミットする。
    /// Play すると、MockDevice はこの Prefab から作られる（Inspector で変えた設定が残る）。既にある Prefab は上書きしない。
    /// </summary>
    public static class MockDevicePrefabGenerator
    {
        const string OutputFolder = "Assets/Resources/Prefabs/Device";
        const string PrefabPath = OutputFolder + "/MockDevice.prefab";

        [MenuItem("MouthOfGod/Device/Create MockDevice Prefab")]
        static void Create()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
            {
                Debug.Log($"[MockDevice] Prefab は既にあります（上書きしません）: {PrefabPath}");
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            EnsureFolder(OutputFolder);

            // エディタでは Awake・OnEnable が走らないので、有効にしてから保存する（Prefab の根を非アクティブにしない）。
            var device = MockDevice.CreateInactive();
            var go = device.gameObject;
            go.SetActive(true);
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
                Debug.Log($"[MockDevice] Prefab を作りました: {PrefabPath}");
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            var slash = folder.LastIndexOf('/');
            var parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
