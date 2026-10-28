using UnityEditor;
using UnityEngine;

namespace MouthOfGod.CameraWork.Editor
{
    /// <summary>
    /// 山から村へ引いていく「村の一連」のカットを、開いているシーンに組む。
    /// 山（<c>MinitureBase/Content/MountVer2/mount</c>）と村（<c>.../Village/Village/Plaza</c>）の位置から、目印とカメラを置く。
    /// 動きの確認は tools/camera_preview/village_tour.py（同じ位置・同じカット）。
    /// </summary>
    public static class VillageTourBuilder
    {
        const string MountPath = "MinitureBase/Content/MountVer2/mount";
        const string VillagePath = "MinitureBase/Content/Village/Village/Plaza";

        [MenuItem("MouthOfGod/CameraWork/Add Village Tour")]
        static void Build()
        {
            GameObject mount = GameObject.Find(MountPath);
            GameObject village = GameObject.Find(VillagePath);
            if (mount == null || village == null)
            {
                Debug.LogWarning("[VillageTour] 山か村が見つかりません（" + MountPath + " / " + VillagePath + "）。Main シーンを開いて実行してください。");
                return;
            }

            Vector3 m = mount.transform.position;
            Vector3 v = village.transform.position;

            var root = new GameObject("VillageTour");
            Undo.RegisterCreatedObjectUndo(root, "Add Village Tour");
            Transform summit = Marker(root, "Summit", m + new Vector3(0f, 0.30f, 0f));
            Transform villageLook = Marker(root, "VillageLook", v + new Vector3(0f, 0.03f, 0f));
            Transform bend = Marker(root, "Bend", m + new Vector3(-0.55f, 0.40f, 0.25f));
            Transform pulledBack = Marker(root, "PulledBack", v + new Vector3(-0.62f, 0.30f, -0.35f));

            var camera = new GameObject("TourCamera");
            camera.transform.SetParent(root.transform, false);
            camera.AddComponent<Camera>().nearClipPlane = 0.01f;
            Vector3 start = m + new Vector3(0.10f, 0.17f, 0.20f);
            camera.transform.SetPositionAndRotation(start, Quaternion.LookRotation(summit.position - start));
            camera.SetActive(false); // 撮るときに有効にする（VR の Main Camera とは別の、カット用のカメラ）

            var player = camera.AddComponent<CameraCutPlayer>();
            player.Shots = new[]
            {
                new CameraShot { name = "山を見る", kind = ShotKind.Hold, seconds = 0.8f, aim = ShotAim.LookAtTarget, aimTarget = summit },
                new CameraShot
                {
                    name = "山から引く", kind = ShotKind.Curve, seconds = 5f, ease = CameraEase.EaseInOut, easeStrength = 2f,
                    end = pulledBack, controlPoints = new[] { bend },
                    aim = ShotAim.LookAtTarget, aimTarget = summit, aimTargetEnd = villageLook,
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

        static Transform Marker(GameObject root, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;
            return go.transform;
        }
    }
}
