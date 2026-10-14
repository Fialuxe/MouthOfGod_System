using System.Collections.Generic;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 村のフィールドを Play 時にコードで組み立てる。置き物は Resources/Prefabs/Village/ の Prefab を名前で読む
    /// （Prefab は VillagePrefabGenerator が素材から作る）。配置は <see cref="VillageLayout"/>。
    /// 足りない状態 / 届いた後の状態を <see cref="SetDelivered"/> で切り替える。
    /// </summary>
    public sealed class VillageBuilder : MonoBehaviour
    {
        const string PrefabFolder = "Prefabs/Village/";

        [Tooltip("素材（メートル）をミニチュアに縮める倍率。暫定")]
        [SerializeField] float miniatureScale = 0.05f;

        readonly Dictionary<string, FieldView> _fields = new Dictionary<string, FieldView>();

        sealed class FieldView
        {
            public Renderer Ground;
            public VillageField Definition;
            public List<(VillageItem Item, GameObject Instance)> Items = new List<(VillageItem, GameObject)>();
        }

        /// <summary>シーン内の VillageBuilder を返す。無ければ作る（Play するだけで村ができる）。</summary>
        public static VillageBuilder GetOrCreate()
        {
            var existing = FindFirstObjectByType<VillageBuilder>();
            if (existing != null) return existing;
            return new GameObject("Village").AddComponent<VillageBuilder>();
        }

        void Awake() => Build(VillageLayout.CreateDefault());

        /// <summary>そのフィールドを、届いた後の状態（true）か、足りない状態（false）にする。</summary>
        public void SetDelivered(string fieldId, bool delivered)
        {
            if (!_fields.TryGetValue(fieldId, out var view))
            {
                Debug.LogWarning($"[Village] 知らないフィールド: {fieldId}");
                return;
            }
            Apply(view, delivered);
        }

        void Build(List<VillageField> fields)
        {
            foreach (var field in fields)
            {
                var root = new GameObject(field.Id).transform;
                root.SetParent(transform, false);
                root.localPosition = new Vector3(field.CenterX, 0f, field.CenterZ) * miniatureScale;

                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ground.name = "Ground";
                Destroy(ground.GetComponent<Collider>());
                ground.transform.SetParent(root, false);
                ground.transform.localScale = new Vector3(field.Size, 0.2f, field.Size) * miniatureScale;
                ground.transform.localPosition = new Vector3(0f, -0.1f * miniatureScale, 0f);

                var view = new FieldView { Ground = ground.GetComponent<Renderer>(), Definition = field };
                foreach (var item in field.Items)
                    view.Items.Add((item, CreateItem(item, root, field.Id)));

                _fields[field.Id] = view;
                Apply(view, delivered: false); // 始まりは、全フィールドが足りない状態
            }
        }

        GameObject CreateItem(VillageItem item, Transform parent, string fieldId)
        {
            GameObject go;
            var prefab = Resources.Load<GameObject>(PrefabFolder + item.Prefab);
            if (prefab != null)
            {
                go = Instantiate(prefab, parent);
            }
            else
            {
                Debug.LogWarning($"[Village] Prefab が無いので仮の箱を置く: {PrefabFolder}{item.Prefab}（{fieldId}）");
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
            }

            go.name = item.Prefab;
            go.transform.localPosition = new Vector3(item.X, 0f, item.Z) * miniatureScale;
            go.transform.localRotation = Quaternion.Euler(0f, item.YawDegrees, 0f);
            go.transform.localScale = Vector3.one * (item.Scale * miniatureScale);
            return go;
        }

        static void Apply(FieldView view, bool delivered)
        {
            var c = delivered ? view.Definition.DeliveredGround : view.Definition.LackingGround;
            view.Ground.material.color = new Color(c.R, c.G, c.B);
            foreach (var (item, instance) in view.Items)
                instance.SetActive(VillageLayout.IsVisible(item, delivered));
        }
    }
}
