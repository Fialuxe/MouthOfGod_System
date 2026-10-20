using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouthOfGod.Food.Editor
{
    /// <summary>
    /// 食べ物の Prefab を、遠近のあるカメラで斜め前から写し、背景が透明な PNG にする。村人の吹き出しのアイコンに使う。
    /// Assets/Resources/Prefabs/Food/ のすべての Prefab から、Assets/Resources/Icons/Food/&lt;食べ物の ID&gt;.png を作る。
    /// 1 回実行して、できた PNG をコミットする（Play 時には使わない）。毎回すべて作り直す（Prefab を直したら、もう一度実行する）。
    /// </summary>
    public static class FoodIconGenerator
    {
        const string PrefabFolder = "Assets/Resources/Prefabs/Food/";
        const string OutputFolder = "Assets/Resources/Icons/Food/";
        const int Size = 256; // px
        const float FieldOfView = 30f;
        const float Margin = 1.15f; // 物の周りに残す余白の割合

        // 写す向き（物の中心から見たカメラの方向）。斜め前・少し上から。
        static readonly Vector3 ViewDirection = new Vector3(0.55f, 0.45f, 1f).normalized;

        [MenuItem("MouthOfGod/Food/Generate Icons")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            int created = 0;
            try
            {
                Camera camera = CreateCamera(scene, target);
                AddLight(scene, new Vector3(50f, -30f, 0f), 1.1f);
                AddLight(scene, new Vector3(20f, 150f, 0f), 0.5f); // 影の側を明るくする

                foreach (string path in Directory.GetFiles(PrefabFolder, "*.prefab"))
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                    if (prefab == null) continue;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    if (!Frame(camera, instance))
                    {
                        Debug.LogWarning($"[Food] {prefab.name} に見た目（Renderer）が無いので、アイコンを作りません。");
                        Object.DestroyImmediate(instance);
                        continue;
                    }

                    camera.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                    pixels.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(OutputFolder + prefab.name + ".png", pixels.EncodeToPNG());
                    Object.DestroyImmediate(instance);
                    created++;
                }
            }
            finally
            {
                Object.DestroyImmediate(pixels);
                RenderTexture.ReleaseTemporary(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }

            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(OutputFolder, "*.png")) SetImportSettings(path.Replace('\\', '/'));
            Debug.Log($"[Food] アイコンを {created} 件作りました → {OutputFolder}");
        }

        static Camera CreateCamera(Scene scene, RenderTexture target)
        {
            var go = new GameObject("Icon Camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>();
            camera.scene = scene;
            camera.cameraType = CameraType.Preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f); // 背景は透明
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.targetTexture = target;
            camera.enabled = false; // Render() を呼んだときだけ写す
            return camera;
        }

        static void AddLight(Scene scene, Vector3 euler, float intensity)
        {
            var go = new GameObject("Icon Light");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(euler);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = intensity;
        }

        // 物全体が画面に収まる距離に、カメラを置く。見た目が無ければ false。
        static bool Frame(Camera camera, GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

            float radius = bounds.extents.magnitude;
            float distance = radius * Margin / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);
            camera.transform.position = bounds.center + ViewDirection * distance;
            camera.transform.LookAt(bounds.center);
            return true;
        }

        static void SetImportSettings(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
    }
}
