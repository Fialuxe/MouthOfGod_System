using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールドの Prefab を親の台の上面に乗せ、親の大きさの倍率を打ち消します。Prefab の一番上に付けます。
    /// </summary>
    /// <remarks>
    /// シーンの台は縦につぶれた立方体なので、そのまま子にすると中身がゆがみます。これを付けると、Prefab の中は 1 = 1 m のままです。
    /// </remarks>
    [ExecuteAlways]
    public sealed class SurfaceAnchor : MonoBehaviour
    {
        [Tooltip("親のローカルで、乗せる面の高さ。Cube の上面は 0.5")]
        [SerializeField] float surfaceLocalY = 0.5f;

        void OnEnable() => Apply();

        void Update()
        {
            if (transform.parent != null && transform.parent.hasChanged) Apply();
        }

        void OnValidate() => Apply();

        void Apply()
        {
            var parent = transform.parent;
            if (parent == null) return;
            var lossy = parent.lossyScale;
            if (lossy.x == 0f || lossy.y == 0f || lossy.z == 0f) return;
            transform.localPosition = new Vector3(0f, surfaceLocalY, 0f);
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(1f / lossy.x, 1f / lossy.y, 1f / lossy.z);
        }
    }
}
