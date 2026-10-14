using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 村のフィールドの置き物を、Play 時にコードで組み立てる。
    /// シーン（Main.unity）の「Village」オブジェクト（立方体の地面）に付けると、その子として並べる。
    /// 置き物は Resources/Prefabs/Village/ の Prefab を名前で読む（Prefab は VillagePrefabGenerator が素材から作る）。
    /// 配置は <see cref="VillageLayout"/>。足りない状態 / 届いた後の状態は <see cref="SetDelivered"/> で切り替える。
    /// </summary>
    public sealed class VillageBuilder : MonoBehaviour
    {
        const string PrefabFolder = "Prefabs/Village/";
        const string VillageObjectName = "Village";
        static readonly int DeliveredTrigger = Animator.StringToHash("Delivered");

        [Tooltip("置き物を並べる面の高さ（親のローカル）。Cube の上面は 0.5")]
        [SerializeField] float surfaceLocalY = 0.5f;

        readonly List<(VillageItem Item, GameObject Instance)> _items = new List<(VillageItem, GameObject)>();
        VillageField _field;
        bool _delivered;

        /// <summary>
        /// シーンの VillageBuilder を返す。無ければ、シーンの「Village」オブジェクトに付ける。
        /// 「Village」も無ければ（サンドボックスなど）、立方体の地面を作ってそこに付ける。
        /// </summary>
        public static VillageBuilder GetOrCreate()
        {
            var existing = FindFirstObjectByType<VillageBuilder>();
            if (existing != null) return existing;

            var village = GameObject.Find(VillageObjectName); // この機能自身のオブジェクト
            if (village == null)
            {
                village = GameObject.CreatePrimitive(PrimitiveType.Cube);
                village.name = VillageObjectName;
                village.transform.localScale = new Vector3(0.54f, 0.2f, 0.54f);
            }
            return village.AddComponent<VillageBuilder>();
        }

        void Awake()
        {
            _field = VillageLayout.CreateDefault().Find(f => f.Id == VillageLayout.Village);
            Build();
        }

        /// <summary>村を、届いた後の状態（true）か、足りない状態（false）にする。</summary>
        public void SetDelivered(string fieldId, bool delivered)
        {
            if (fieldId != VillageLayout.Village)
            {
                Debug.LogWarning($"[Village] 知らないフィールド: {fieldId}");
                return;
            }
            if (delivered == _delivered) return;
            _delivered = delivered;

            foreach (var (item, instance) in _items)
            {
                var visible = VillageLayout.IsVisible(item, delivered);
                instance.SetActive(visible);
                PlayAnimation(instance, delivered);
            }
        }

        void Build()
        {
            foreach (var item in _field.Items)
            {
                var instance = CreateItem(item);
                _items.Add((item, instance));
                instance.SetActive(VillageLayout.IsVisible(item, false)); // 始まりは足りない状態
            }
        }

        GameObject CreateItem(VillageItem item)
        {
            GameObject go;
            var prefab = Resources.Load<GameObject>(PrefabFolder + item.Prefab);
            if (prefab != null)
            {
                go = Instantiate(prefab, transform);
            }
            else
            {
                Debug.LogWarning($"[Village] Prefab が無いので仮の箱を置く: {PrefabFolder}{item.Prefab}");
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(transform, false);
            }
            go.name = item.Prefab;

            // 親（Village）は (0.54, 0.2, 0.54) のように縦横が違う倍率で潰れている。
            // 置き物がゆがまないよう、親の倍率を打ち消して、ワールドでの大きさを決める。
            var lossy = transform.lossyScale;
            var worldScale = item.Scale * lossy.x / _field.Size; // 地面の一辺が Size メートルに当たる
            go.transform.localPosition = new Vector3(item.X / _field.Size, surfaceLocalY, item.Z / _field.Size);
            go.transform.localRotation = Quaternion.Euler(0f, item.YawDegrees, 0f);
            go.transform.localScale = new Vector3(worldScale / lossy.x, worldScale / lossy.y, worldScale / lossy.z);
            return go;
        }

        // 村人は、足りない間は Idle のまま。届いた後に Victory を 1 回やって Idle に戻る（Animator の Delivered トリガー）。
        static void PlayAnimation(GameObject instance, bool delivered)
        {
            if (!instance.activeInHierarchy) return;
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;

            if (delivered) animator.SetTrigger(DeliveredTrigger);
            else
            {
                animator.ResetTrigger(DeliveredTrigger);
                animator.Play("Idle", 0, 0f);
            }
        }
    }
}
