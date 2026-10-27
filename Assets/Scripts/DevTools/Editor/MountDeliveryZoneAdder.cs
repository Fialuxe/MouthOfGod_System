using MouthOfGod.Village;
using UnityEditor;
using UnityEngine;

namespace MouthOfGod.DevTools.Editor
{
    /// <summary>
    /// 山の Main 用 Prefab（Misc/MountVer2 Variant）に、届ける場所（DeliveryZone）と、それを受けるトリガーの箱を足す。
    /// 山には、食べ物が落ちた場所を判定する DeliveryZone が無く、山の村人に向けて飛ばしても届いたことにならなかった。
    /// 草原の Variant と同じ形にする。箱は、山の見た目（立つ場所の足あとは除く）の広さに合わせ、横に余白を、上に高さを足す。
    /// 1 回実行して、Variant をコミットする。すでに DeliveryZone があれば、何もしない（何度実行しても同じ）。
    /// </summary>
    public static class MountDeliveryZoneAdder
    {
        const string VariantPath = "Assets/Resources/Prefabs/Misc/MountVer2 Variant.prefab";
        const float SideMargin = 0.05f; // m（山の座標）。縁に落ちた物も拾う
        const float HeightAbove = 1.0f; // m（山の座標）。上から落ちてくる物を受ける高さ

        [MenuItem("MouthOfGod/Dev/Add Delivery Zone To Mount Variant")]
        public static void Add()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(VariantPath) == null)
            {
                Debug.LogError($"[MountDeliveryZoneAdder] Variant が見つかりません: {VariantPath}");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(VariantPath);
            try
            {
                if (root.GetComponent<DeliveryZone>() != null)
                {
                    Debug.Log("[MountDeliveryZoneAdder] すでに DeliveryZone があるので、何もしません。");
                    return;
                }
                if (!TryMeasure(root, out Vector3 center, out Vector3 size))
                {
                    Debug.LogError("[MountDeliveryZoneAdder] 山の見た目（Renderer）が見つからず、箱の大きさを決められません。");
                    return;
                }

                var box = root.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = center;
                box.size = size;
                root.AddComponent<DeliveryZone>(); // 付けたとき、Collider を Is Trigger にする

                PrefabUtility.SaveAsPrefabAsset(root, VariantPath);
                Debug.Log($"[MountDeliveryZoneAdder] 山の Variant に DeliveryZone を足しました。箱: 中心 {center}、大きさ {size}（山の座標）。Scene ビューで、箱が山を覆っているか確かめてください。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // 山の見た目の広さを、ルートの座標で測る。立つ場所の足あと（StandArea の下）は含めない。
        static bool TryMeasure(GameObject root, out Vector3 center, out Vector3 size)
        {
            center = default;
            size = default;
            bool found = false;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            Transform rootTransform = root.transform;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is ParticleSystemRenderer || IsUnderStandArea(renderer.transform, rootTransform)) continue;
                Bounds world = renderer.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = world.center + Vector3.Scale(world.extents, new Vector3(
                        (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    Vector3 local = rootTransform.InverseTransformPoint(corner);
                    if (!found) { min = max = local; found = true; }
                    else { min = Vector3.Min(min, local); max = Vector3.Max(max, local); }
                }
            }
            if (!found) return false;

            min -= new Vector3(SideMargin, 0f, SideMargin);
            max += new Vector3(SideMargin, HeightAbove, SideMargin);
            center = (min + max) * 0.5f;
            size = max - min;
            return true;
        }

        static bool IsUnderStandArea(Transform t, Transform stop)
        {
            for (; t != null && t != stop; t = t.parent)
                if (t.GetComponent<StandArea>() != null) return true;
            return false;
        }
    }
}
