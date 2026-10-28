using UnityEditor;
using UnityEngine;

namespace MouthOfGod.CameraWork.Editor
{
    /// <summary>
    /// 草原から村へ引いていく「村の一連」のカットを、開いているシーンに組む。
    /// 草原（<c>MinitureBase/Content/Grass/Tree1</c>）と村（<c>.../Village/Village/Plaza</c>）の位置から、目印とカメラを置く。
    /// 動きの確認は tools/camera_preview/village_tour.py（同じ位置・同じカット）。
    /// </summary>
    public static class VillageTourBuilder
    {
        const string GrassPath = "MinitureBase/Content/Grass/Tree1";
        const string VillagePath = "MinitureBase/Content/Village/Village/Plaza";

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
            camera.SetActive(false); // 撮るときに有効にする（VR の Main Camera とは別の、カット用のカメラ）

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
            cam.fieldOfView = 24f; // 草原にズームした状態から始める
            camera.transform.SetPositionAndRotation(start, Quaternion.LookRotation(grassLook.position - start));
            camera.SetActive(false);

            // 村側から草原へ寄った絵 → 真後ろへ一直線に引きながら画角を広げ、見る点を村の中心へ移す（回らない）
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
