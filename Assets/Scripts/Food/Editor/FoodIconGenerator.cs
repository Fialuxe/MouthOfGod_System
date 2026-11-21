using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MouthOfGod.Food.Editor
{
    /// <summary>
    /// 食べ物の Prefab を斜め前から写し、背景が透明な PNG にします。村人の吹き出しのアイコンに使います。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Assets/Prefabs/Food/ のすべての Prefab から、PNG を作ります。
    /// 出力先は Assets/Art/Icons/Food/&lt;食べ物の ID&gt;.png です。
    /// 1 回実行して、できた PNG をコミットします。Play 中には使いません。
    /// </para>
    /// <para>
    /// 既にある PNG は上書きしません。
    /// 手で描いたアイコンを同じ名前で置けば、そちらが使われます。
    /// </para>
    /// <para>
    /// 作り直すときは、Project で食べ物の Prefab を選んで右クリックします。
    /// 「MouthOfGod/Food/Generate Icon From Selected」は、上書きして作ります。
    /// </para>
    /// </remarks>
    public static class FoodIconGenerator
    {
        const string PrefabFolder = "Assets/Prefabs/Food/";
        const string OutputFolder = "Assets/Art/Icons/Food/";
        const int Size = 256;  // px
        const float FieldOfView = 30f;
        // 物を囲む箱が、画像の幅と高さのどれだけを占めるかです。1 に近いほど余白が狭くなります。
        const float Fill = 0.92f;

        // 物の中心から見た、カメラの方向です。斜め前の少し上から写します。
        static readonly Vector3 ViewDirection = new Vector3(0.55f, 0.45f, 1f).normalized;

        /// <summary>
        /// まだ PNG が無い食べ物の Prefab だけ、アイコンを作ります。
        /// </summary>
        [MenuItem("MouthOfGod/Food/Generate Icons")]
        public static void Generate()
        {
            var prefabs = new List<GameObject>();
            int skipped = 0;
            foreach (string path in Directory.GetFiles(PrefabFolder, "*.prefab"))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path.Replace('\\', '/'));
                if (prefab == null) continue;
                if (File.Exists(OutputFolder + prefab.name + ".png")) { skipped++; continue; }
                prefabs.Add(prefab);
            }
            int created = Render(prefabs);
            Debug.Log($"[Food] アイコンを {created} 件作りました（既にあって飛ばした: {skipped} 件）→ {OutputFolder}");
        }

        // Project で選んだ食べ物の Prefab だけ、アイコンを作り直します。既にある PNG も上書きします。
        [MenuItem("Assets/MouthOfGod/Food/Generate Icon From Selected")]
        static void GenerateSelected()
        {
            int created = Render(SelectedFoodPrefabs());
            Debug.Log($"[Food] 選んだ Prefab のアイコンを {created} 件作り直しました → {OutputFolder}");
        }

        [MenuItem("Assets/MouthOfGod/Food/Generate Icon From Selected", true)]
        static bool CanGenerateSelected() => SelectedFoodPrefabs().Count > 0;

        static List<GameObject> SelectedFoodPrefabs()
        {
            var prefabs = new List<GameObject>();
            foreach (GameObject prefab in Selection.GetFiltered<GameObject>(SelectionMode.Assets))
                if (AssetDatabase.GetAssetPath(prefab).StartsWith(PrefabFolder)) prefabs.Add(prefab);
            return prefabs;
        }

        // 渡した Prefab を写して PNG にし、作った数を返します。
        // 見た目（Renderer）が無い Prefab は飛ばし、数に入れません。
        static int Render(List<GameObject> prefabs)
        {
            if (prefabs.Count == 0) return 0;
            Directory.CreateDirectory(OutputFolder);
            Scene scene = EditorSceneManager.NewPreviewScene();
            RenderTexture target = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            int created = 0;
            try
            {
                Camera camera = CreateCamera(scene, target);
                AddLight(scene, new Vector3(50f, -30f, 0f), 1.1f);
                AddLight(scene, new Vector3(20f, 150f, 0f), 0.5f);  // 影の側を明るくします

                foreach (GameObject prefab in prefabs)
                {
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
            foreach (GameObject prefab in prefabs) SetImportSettings(OutputFolder + prefab.name + ".png");
            return created;
        }

        static Camera CreateCamera(Scene scene, RenderTexture target)
        {
            var go = new GameObject("Icon Camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.AddComponent<Camera>();
            camera.scene = scene;
            camera.cameraType = CameraType.Preview;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);  // 背景を透明にします
            camera.fieldOfView = FieldOfView;
            camera.aspect = 1f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 100f;
            camera.targetTexture = target;
            camera.enabled = false;  // Render() を呼んだときだけ写します
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

        // 物全体が画面に収まる距離に、カメラを置きます。見た目が無ければ false を返します。
        static bool Frame(Camera camera, GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);

            // まず、外側を囲む球が収まる距離に置きます。
            // 次に、箱の角の写り方を見て、Fill に合うまで近づけます。
            // 遠近があるので 1 回では合わず、数回くり返します。
            float radius = bounds.extents.magnitude;
            float distance = radius / Mathf.Sin(FieldOfView * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < 4; i++)
            {
                Place(camera, bounds.center, distance);
                float extent = ViewportExtent(camera, bounds);
                if (extent <= 0f) break;
                distance = Mathf.Max(distance * extent / Fill, radius * 1.05f);  // カメラが箱の中に入らないようにします
            }
            Place(camera, bounds.center, distance);
            return true;
        }

        static void Place(Camera camera, Vector3 center, float distance)
        {
            camera.transform.position = center + ViewDirection * distance;
            camera.transform.LookAt(center);
        }

        // 箱の 8 つの角が、画像のどこまで広がるかを返します。
        // 画像の幅と高さに対する割合で、1 でちょうど端です。
        static float ViewportExtent(Camera camera, Bounds bounds)
        {
            float extent = 0f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 v = camera.WorldToViewportPoint(corner);
                extent = Mathf.Max(extent, Mathf.Abs(v.x - 0.5f) * 2f, Mathf.Abs(v.y - 0.5f) * 2f);
            }
            return extent;
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
