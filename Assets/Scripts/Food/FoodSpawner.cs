using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// デバイスで決定した食感から、食べ物を引いて生み、前へ飛ばす。
    /// 食感が決まったとき（<see cref="IDevice.Confirmed"/>）に、食べ物の Prefab（<see cref="FoodItem"/>）から合う物を選び、<c>Spawn Point</c> の位置に生む。
    /// </summary>
    /// <remarks>
    /// - 体験者のリグ（<see cref="PlayerRig"/>）の子に置く。デバイスと口は、親のリグから受け取る。Mock か実機かは知らない。
    /// - 飛ばす: <c>Launch Speed</c> が 0 より大きく、生んだ食べ物に Rigidbody があれば、<c>Spawn Point</c> の前向きに初速を与える。そのあとの動きは、食べ物の Rigidbody（Mass など）と物理しだい。
    ///   体験者は、顔を向けて狙いを定め、欲しがっている人のところへ飛ばす。どこに届いたかは、落ちた所の DeliveryZone（Village）が決める。
    /// - <c>Food Parent</c> を入れると、生んだ食べ物をその子にする（位置と向きは Spawn Point のまま）。
    /// - どの食べ物かは、食べ物の Prefab に付いた <see cref="FoodItem"/>（<see cref="IFoodItem"/>）が持っている。
    /// - 「体験者の前のフィールド」は、Core の <see cref="Standing.Field"/> を読む。
    /// - 間違えたとき（欲しい物と違う食べ物が届いたとき）の反応は、ここにはない。届いた知らせ（<see cref="IFieldRequests.Received"/>）を受ける側が足す。
    /// </remarks>
    public sealed class FoodSpawner : MonoBehaviour, IFoodSpawner
    {
        [Tooltip("食べ物が生まれる位置と向き。空なら、親のリグの口（Mouth）。リグの外なら、このオブジェクトの位置と向き")]
        [SerializeField] Transform spawnPoint;

        [Tooltip("生んだ食べ物を、この子として置く（Hierarchy が散らからない・まとめて消せる）。位置と向きは Spawn Point のまま。空なら、シーンの直下。動く物や、拡大縮小した物（ミニチュアの台など）の子にすると、飛んでいる食べ物も一緒に動き・大きさが変わるので、動かない・倍率 1 の空の GameObject を入れる")]
        [SerializeField] Transform foodParent;

        [Tooltip("生んだ食べ物に、Spawn Point の前向きに与える初速（m/s）。0 以下、または食べ物に Rigidbody がなければ、与えない。そのあとの動きは、食べ物の Rigidbody（Mass など）しだい")]
        [SerializeField, Min(0f)] float launchSpeed = 2f;

        [Tooltip("生める食べ物の一覧（Assets/Prefabs/Food の Prefab）。食べ物を足したら、ここにも足す。生まれる条件は、各食べ物の FoodItem が持つ")]
        [SerializeField] FoodItem[] foods = new FoodItem[0];

        readonly Dictionary<TextureId, FoodItem> _overrides = new Dictionary<TextureId, FoodItem>(); // 場面の間だけの対応

        // 警告と例外の出し先。テストで差し替える。
        internal static Action<string> ReportWarning = Debug.LogWarning;
        internal static Action<Exception> ReportException = Debug.LogException;

        IDevice _device;

        /// <summary>生んだ食べ物の置き場（親）。空ならシーンの直下。</summary>
        public Transform FoodParent
        {
            get => foodParent;
            set => foodParent = value;
        }

        /// <summary>食べ物を生んだ直後に、1 回出る。</summary>
        public event Action<Spawned> Spawned;

        /// <summary>
        /// 食感から、生まれる食べ物の Prefab を引くだけ。生まない。
        /// チュートリアルで、噛んでいる間に、その食感で生まれる物の影を見せるときに使う。引けなければ null。
        /// </summary>
        public GameObject Preview(TextureId texture) => Resolve(Standing.Field, texture);

        /// <summary>
        /// 食感から食べ物を引いて、1 つ生む。生んだ食べ物を返す。
        /// 食感が空（噛む前の決定）なら、何もせず null。引けなければ、警告を出して null。
        /// </summary>
        public GameObject Spawn(TextureId texture)
        {
            if (texture.IsEmpty) return null;

            FieldId field = Standing.Field;
            GameObject prefab = Resolve(field, texture);
            if (prefab == null)
            {
                ReportWarning("[FoodSpawner] 食感「" + texture + "」に対応する食べ物がありません（フィールド: "
                              + (field.IsEmpty ? "なし" : field.Value) + "）。");
                return null;
            }

            Debug.Log("[FoodSpawner] " + (field.IsEmpty ? "フィールドなし" : field.Value) + " / " + texture + " → " + prefab.name);

            Transform origin = Origin();
            GameObject instance = Instantiate(prefab, origin.position, origin.rotation, foodParent);
            instance.name = prefab.name;
            if (instance.TryGetComponent(out FoodItem item)) item.SetSource(prefab);

            Launch(instance, origin);
            RaiseSpawned(new Spawned(prefab, instance.transform.position));
            return instance;
        }

        /// <summary>
        /// この場面の間だけ、texture のときはどこでも food を生むようにします。チュートリアルが、生ませたい物を決めるときに使います。
        /// 場面が終わったら <see cref="ClearOverrides"/> で戻します。
        /// </summary>
        public void Override(TextureId texture, GameObject food)
        {
            if (food != null && food.TryGetComponent(out FoodItem item)) _overrides[texture] = item;
        }

        /// <summary><see cref="Override"/> で決めた対応を、すべて戻します。</summary>
        public void ClearOverrides() => _overrides.Clear();

        /// <summary>
        /// field と texture に合う食べ物を、一覧から選びます。場面の間だけの対応があれば、それを先に使います。
        /// 次に、フィールドが合う物を選び、なければフィールドが空（どこでも）の物を選びます。合う物がなければ null です。
        /// </summary>
        GameObject Resolve(FieldId field, TextureId texture)
        {
            if (texture.IsEmpty) return null;
            if (_overrides.TryGetValue(texture, out FoodItem chosen)) return chosen.gameObject;
            foreach (FoodItem item in foods)
                if (item != null && item.IsBornFrom(field, texture, anyField: false)) return item.gameObject;
            foreach (FoodItem item in foods)
                if (item != null && item.IsBornFrom(field, texture, anyField: true)) return item.gameObject;
            return null;
        }

        // 初速（m/s）。Spawn Point の前向き × 速さ。
        internal static Vector3 LaunchVelocity(Transform origin, float speed) => origin.forward * speed;

        void Launch(GameObject instance, Transform origin)
        {
            if (launchSpeed <= 0f) return;
            Rigidbody body;
            if (!instance.TryGetComponent<Rigidbody>(out body) || body.isKinematic) return;
            // 質量によらず、速さそのものを与える。そのあとは物理（重さ・空気抵抗・重力）が動かす。
            body.AddForce(LaunchVelocity(origin, launchSpeed), ForceMode.VelocityChange);
        }

        Transform Origin()
        {
            if (spawnPoint != null) return spawnPoint;
            PlayerRig rig = PlayerRig.Of(this);
            return rig != null && rig.Mouth != null ? rig.Mouth : transform;
        }

        void RaiseSpawned(Spawned spawned)
        {
            Action<Spawned> handlers = Spawned;
            if (handlers == null) return;
            // 1 つの購読先が例外を投げても、残りには届ける（例外は Console に出す）。
            foreach (Action<Spawned> handler in handlers.GetInvocationList())
            {
                try { handler(spawned); }
                catch (Exception e) { ReportException(e); }
            }
        }

        void OnConfirmed(TextureId texture) => Spawn(texture);

        void Awake()
        {
            if (foods.Length == 0) Debug.LogError("[FoodSpawner] 生める食べ物の一覧（Foods）が空です。", this);
        }

        void OnEnable()
        {
            PlayerRig rig = PlayerRig.Of(this);
            _device = rig != null ? rig.Device : null;
            if (_device == null)
            {
                Debug.LogError("[FoodSpawner] 親のリグ（PlayerRig）にデバイスがないので、食べ物を生めません。", this);
                return;
            }
            _device.Confirmed += OnConfirmed;
        }

        void OnDisable()
        {
            if (_device != null) _device.Confirmed -= OnConfirmed;
            _device = null;
        }
    }
}
