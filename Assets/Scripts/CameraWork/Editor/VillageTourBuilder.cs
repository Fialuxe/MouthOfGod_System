using UnityEditor;
using UnityEngine;

namespace MouthOfGod.CameraWork.Editor
{
    /// <summary>
    /// 草原から村へ引いていくカットを、開いているシーンに組みます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 草原と村の位置をもとに、目印とカメラを置きます。
    /// 草原は <c>MinitureBase/Content/Grass/Tree1</c> です。
    /// 村は <c>.../Village/Village/Plaza</c> です。
    /// どちらかが無いシーンでは、警告を出して何もしません。
    /// </para>
    /// <para>
    /// Unity を開かずに動きを確かめるときは、<c>tools/camera_preview/village_tour.py</c> を使います。
    /// 同じ位置と同じカットで描きます。
    /// ここの値を変えたら、そちらも合わせます。
    /// </para>
    /// </remarks>
    public static class VillageTourBuilder
    {
        const string GrassPath = "MinitureBase/Content/Grass/Tree1";
        const string VillagePath = "MinitureBase/Content/Village/Village/Plaza";

        // 草原を見てから、曲線で引きながら見る点を村へ移し、村を回り込むカットを組みます。
        // python 側の pullback に当たります。
        [MenuItem("MouthOfGod/CameraWork/Add Village Tour")]
        static void Build()
        {
            GameObject grass = GameObject.Find(GrassPath);
            GameObject village = GameObject.Find(VillagePath);
            if (grass == null || village == null)
            {
                Debug.LogWarning("[VillageTour] 草原か村が見つかりません（" + GrassPath + " / " + VillagePath + "）。Main シーンを開いて実行してください。");
                return;
            }

            Vector3 g = grass.transform.position;
            Vector3 v = village.transform.position;

            var root = new GameObject("VillageTour");
            Undo.RegisterCreatedObjectUndo(root, "Add Village Tour");
            Transform grassLook = Marker(root, "GrassLook", g + new Vector3(0f, 0.05f, 0f));
            Transform villageLook = Marker(root, "VillageLook", v + new Vector3(0f, 0.03f, 0f));
            Transform bend = Marker(root, "Bend", g + new Vector3(0.35f, 0.42f, -0.55f));
            Transform pulledBack = Marker(root, "PulledBack", v + new Vector3(0.05f, 0.30f, -0.62f));

            var camera = new GameObject("TourCamera");
            camera.transform.SetParent(root.transform, false);
            camera.AddComponent<Camera>().nearClipPlane = 0.01f;
            Vector3 start = g + new Vector3(0.20f, 0.12f, 0.17f);
            camera.transform.SetPositionAndRotation(start, Quaternion.LookRotation(grassLook.position - start));
            // VR の Main Camera とは別の、カット用のカメラです。撮るときだけ有効にします。
            camera.SetActive(false);

            var player = camera.AddComponent<CameraCutPlayer>();
            player.Shots = new[]
            {
                new CameraShot { name = "草原を見る", kind = ShotKind.Hold, seconds = 0.8f, aim = ShotAim.LookAtTarget, aimTarget = grassLook },
                new CameraShot
                {
                    name = "草原から引く", kind = ShotKind.Curve, seconds = 5f, ease = CameraEase.EaseInOut, easeStrength = 2f,
                    end = pulledBack, controlPoints = new[] { bend },
                    aim = ShotAim.LookAtTarget, aimTarget = grassLook, aimTargetEnd = villageLook,
                },
                new CameraShot
                {
                    name = "村を回り込む", kind = ShotKind.Orbit, seconds = 4.5f, ease = CameraEase.EaseInOut, easeStrength = 2f,
                    center = villageLook, angle = -110f, radiusScale = 0.8f, heightChange = -0.08f,
                    aim = ShotAim.LookAtTarget, aimTarget = villageLook,
                },
                new CameraShot { name = "村を見渡す", kind = ShotKind.Hold, seconds = 0.8f, aim = ShotAim.LookAtTarget, aimTarget = villageLook },
            };

            Selection.activeGameObject = camera;
            Debug.Log("[VillageTour] VillageTour を置きました。TourCamera を有効にして、CameraCutPlayer の Play で撮れます。目印を動かすと、軌道が変わります。");
        }

        // 草原に寄った絵から、一直線に引きながら画角を広げるカットを組みます。
        // python 側の zoomout に当たります。
        [MenuItem("MouthOfGod/CameraWork/Add Village Zoom-Out")]
        static void BuildZoomOut()
        {
            GameObject grass = GameObject.Find(GrassPath);
            GameObject village = GameObject.Find(VillagePath);
            if (grass == null || village == null)
            {
                Debug.LogWarning("[VillageTour] 草原か村が見つかりません（" + GrassPath + " / " + VillagePath + "）。Main シーンを開いて実行してください。");
                return;
            }

            Vector3 g = grass.transform.position;
            Vector3 v = village.transform.position;
            Vector3 toward = g - v;
            toward.y = 0f;
            toward.Normalize();

            var root = new GameObject("VillageZoomOut");
            Undo.RegisterCreatedObjectUndo(root, "Add Village Zoom-Out");
            Transform grassLook = Marker(root, "GrassLook", g + new Vector3(0f, 0.05f, 0f));
            Transform villageLook = Marker(root, "VillageLook", v + new Vector3(0f, 0.03f, 0f));
            Vector3 start = v + new Vector3(0f, 0.10f, 0f) + toward * 0.12f;
            Transform pulledBack = Marker(root, "PulledBack", start - toward * 0.95f + new Vector3(0f, 0.42f, 0f));

            var camera = new GameObject("ZoomOutCamera");
            camera.transform.SetParent(root.transform, false);
            var cam = camera.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.fieldOfView = 24f;  // 草原にズームした状態から始めます。
            camera.transform.SetPositionAndRotation(start, Quaternion.LookRotation(grassLook.position - start));
            camera.SetActive(false);

            // 村の側から、草原へ寄った絵で始めます。
            // そこから真後ろへ一直線に引きながら、画角を広げます。
            // 見る点は村の中心へ移しますが、カメラは回り込みません。
            camera.AddComponent<CameraCutPlayer>().Shots = new[]
            {
                new CameraShot { name = "草原にズーム", kind = ShotKind.Hold, seconds = 1.2f, aim = ShotAim.LookAtTarget, aimTarget = grassLook },
                new CameraShot
                {
                    name = "一直線に引く", kind = ShotKind.Line, seconds = 6f, ease = CameraEase.EaseInOut, easeStrength = 2.6f,
                    end = pulledBack, fieldOfViewEnd = 62f,
                    aim = ShotAim.LookAtTarget, aimTarget = grassLook, aimTargetEnd = villageLook,
                },
                new CameraShot { name = "村の全体", kind = ShotKind.Hold, seconds = 1f, aim = ShotAim.LookAtTarget, aimTarget = villageLook },
            };

            Selection.activeGameObject = camera;
            Debug.Log("[VillageTour] VillageZoomOut を置きました。ZoomOutCamera を有効にして、CameraCutPlayer の Play で撮れます。");
        }

        static Transform Marker(GameObject root, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;
            return go.transform;
        }
    }
}
