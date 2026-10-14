using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// フィールドの Prefab の一番上に付ける。親（シーンの Village などの立方体の地面）の上面に乗せ、親の倍率を打ち消す。
    /// シーンの台は (0.54, 0.2, 0.54) のように縦が潰れているので、その子にそのまま置くと中身がゆがむ。
    /// これを付けると、Prefab の中は「1 = 1 m」のままになる。編集中もシーンで同じ見た目になる。
    /// </summary>
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
