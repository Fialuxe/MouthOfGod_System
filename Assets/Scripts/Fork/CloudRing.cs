using UnityEngine;

namespace MouthOfGod.Fork
{
    /// <summary>
    /// 体験者の前に、雲（<see cref="Cloud"/>）を半円状に浮かべる。体験者が歩くと、ついて来る。
    /// シーンのどこか 1 か所に置く。雲の Prefab は Resources/Prefabs/Fork/Cloud（メニュー「MouthOfGod/Fork/Generate Cloud Prefab」で作る）。
    /// </summary>
    /// <remarks>
    /// - どこに、いくつ浮かべるかは、まだ決まっていない（シナリオブックの未決事項）。数・半径・高さは仮の値で、Inspector で変えられる。
    /// - 体験者の位置は Main Camera。向きは、置いたときの頭の向きで決め、そのあとは回らない（首を振るたびに雲が動かないように）。
    /// </remarks>
    public sealed class CloudRing : MonoBehaviour
    {
        internal const string PrefabResourcePath = "Prefabs/Fork/Cloud";

        [Tooltip("雲の Prefab。空なら Resources/Prefabs/Fork/Cloud を使う")]
        [SerializeField] Cloud cloudPrefab;

        [Tooltip("浮かべる雲の数。仮の値")]
        [SerializeField, Min(1)] int count = 5;

        [Tooltip("頭から雲までの水平の距離（m）。腕を伸ばして届く所。仮の値")]
        [SerializeField, Min(0.1f)] float radius = 0.45f;

        [Tooltip("頭から見た雲の高さ（m）。マイナスで頭より下。仮の値")]
        [SerializeField] float height = -0.3f;

        [Tooltip("雲を並べる角度の幅（度）。正面を中心に、この幅に並べる。360 で体験者を囲む")]
        [SerializeField, Range(0f, 360f)] float spreadDegrees = 120f;

        [Tooltip("上下にゆれる幅（m）")]
        [SerializeField, Min(0f)] float bobAmplitude = 0.02f;

        [Tooltip("体験者について来る速さ。大きいほど早く追いつく")]
        [SerializeField, Min(0f)] float followSpeed = 2f;

        Transform[] _clouds;
        Vector3 _center;
        Quaternion _facing;

        void Start()
        {
            Cloud prefab = cloudPrefab != null ? cloudPrefab : Resources.Load<Cloud>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[CloudRing] 雲の Prefab がありません。メニュー「MouthOfGod/Fork/Generate Cloud Prefab」で作ってください。");
                enabled = false;
                return;
            }

            Transform head = Head();
            _center = head != null ? head.position : transform.position;
            Vector3 forward = head != null ? head.forward : transform.forward;
            forward.y = 0f;
            _facing = forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : Quaternion.identity;

            _clouds = new Transform[count];
            for (int i = 0; i < count; i++)
            {
                Cloud cloud = Instantiate(prefab, transform);
                cloud.name = prefab.name + " " + (i + 1);
                _clouds[i] = cloud.transform;
            }
            Place(0f);
        }

        void Update()
        {
            Transform head = Head();
            if (head != null) _center = Vector3.Lerp(_center, head.position, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            Place(Time.time);
        }

        void Place(float time)
        {
            for (int i = 0; i < _clouds.Length; i++)
            {
                Vector3 offset = Offset(i, _clouds.Length, radius, height, spreadDegrees);
                offset.y += Mathf.Sin(time * 1.3f + i * 1.7f) * bobAmplitude;
                _clouds[i].position = _center + _facing * offset;
            }
        }

        /// <summary>
        /// i 番目の雲の位置（体験者の頭から見た向き。z が正面）。spread の幅に等間隔で並べる。
        /// 360 度のときは、最初と最後が重ならないように、count 等分にする。
        /// </summary>
        internal static Vector3 Offset(int index, int count, float radius, float height, float spreadDegrees)
        {
            float angle;
            if (count <= 1) angle = 0f;
            else if (spreadDegrees >= 360f) angle = 360f * index / count;
            else angle = -spreadDegrees / 2f + spreadDegrees * index / (count - 1);

            float rad = angle * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(rad) * radius, height, Mathf.Cos(rad) * radius);
        }

        static Transform Head() => Camera.main != null ? Camera.main.transform : null;
    }
}
