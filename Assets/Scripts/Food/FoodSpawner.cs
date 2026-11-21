using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// デバイスで決定した食感から食べ物を引いて生み、前へ飛ばします。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 体験者のリグ（<see cref="PlayerRig"/>）の子に置きます。
    /// デバイスと口は、親のリグから受け取ります。Mock か実機かは知りません。
    /// </para>
    /// <para>
    /// <see cref="IDevice.Confirmed"/> を受けると、<c>Foods</c> の一覧から合う物を選びます。
    /// 生む位置と向きは <c>Spawn Point</c> です。
    /// どの食べ物がいつ生まれるかは、各 Prefab の <see cref="FoodItem"/> が持っています。
    /// 体験者の前のフィールドは、<see cref="Standing.Field"/> から読みます。
    /// </para>
    /// <para>
    /// 体験者は顔を向けて狙い、欲しがっている人のところへ飛ばします。
    /// どこに届いたかは、落ちた所の DeliveryZone（Village）が決めます。
    /// </para>
    /// <para>
    /// 欲しい物と違う食べ物が届いたときの反応は、ここでは扱いません。
    /// <see cref="IFieldRequests.Received"/> を受ける側が足します。
    /// </para>
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

        // Override で決めた、場面の間だけの対応です。一覧より先に使います。
        readonly Dictionary<TextureId, FoodItem> _overrides = new Dictionary<TextureId, FoodItem>();

        // 警告と例外の出し先です。テストで差し替えられるようにしています。
        internal static Action<string> ReportWarning = Debug.LogWarning;
        internal static Action<Exception> ReportException = Debug.LogException;

        IDevice _device;

        /// <summary>生んだ食べ物を置く親です。<see langword="null"/> ならシーンの直下に置きます。</summary>
        public Transform FoodParent
        {
            get => foodParent;
            set => foodParent = value;
        }

        /// <inheritdoc/>
        public event Action<Spawned> Spawned;

        /// <inheritdoc/>
        /// <remarks>
        /// チュートリアルで、噛んでいる間に生まれる物の影を見せるときに使います。
        /// <see cref="Override"/> で決めた対応があれば、それを返します。
        /// </remarks>
        public GameObject Preview(TextureId texture) => Resolve(Standing.Field, texture);

        /// <summary>
        /// 食感から食べ物を引いて、1 つ生みます。
        /// </summary>
        /// <param name="texture">決定したときの食感です。</param>
        /// <returns>
        /// 生んだ食べ物です。
        /// 食感が空なら、噛む前の決定なので何もせず <see langword="null"/> を返します。
        /// 合う食べ物が無ければ、警告を出して <see langword="null"/> を返します。
        /// </returns>
        /// <remarks>
        /// 生んだあと、<see cref="Spawned"/> を出します。
        /// </remarks>
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
        /// この場面の間だけ、その食感ではフィールドによらず指定の食べ物を生むようにします。
        /// </summary>
        /// <param name="texture">対応を決め直す食感です。</param>
        /// <param name="food">
        /// 生ませる食べ物の Prefab です。<see cref="FoodItem"/> が付いている必要があります。
        /// <see langword="null"/> か、<see cref="FoodItem"/> が無ければ何もしません。
        /// </param>
        /// <remarks>
        /// チュートリアルが、生ませたい物を決めるときに使います。
        /// 場面が終わったら、<see cref="ClearOverrides"/> で戻します。
        /// </remarks>
        public void Override(TextureId texture, GameObject food)
        {
            if (food != null && food.TryGetComponent(out FoodItem item)) _overrides[texture] = item;
        }

        /// <summary><see cref="Override"/> で決めた対応を、すべて戻します。</summary>
        public void ClearOverrides() => _overrides.Clear();

        /// <summary>
        /// フィールドと食感に合う食べ物を選びます。
        /// </summary>
        /// <param name="field">体験者の前のフィールドです。空でもかまいません。</param>
        /// <param name="texture">食感です。空なら <see langword="null"/> を返します。</param>
        /// <returns>
        /// 合う食べ物の Prefab です。合う物が無ければ <see langword="null"/> です。
        /// </returns>
        /// <remarks>
        /// 場面の間だけの対応があれば、それを先に使います。
        /// 次に、フィールドが合う物を選びます。
        /// それも無ければ、フィールドが空で、どこでも生まれる物を選びます。
        /// </remarks>
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

        // 初速（m/s）を返します。Spawn Point の前向きです。
        internal static Vector3 LaunchVelocity(Transform origin, float speed) => origin.forward * speed;

        void Launch(GameObject instance, Transform origin)
        {
            if (launchSpeed <= 0f) return;
            Rigidbody body;
            if (!instance.TryGetComponent<Rigidbody>(out body) || body.isKinematic) return;
            // 質量によらず、速さそのものを与えます。そのあとは、重さ・空気抵抗・重力で動きます。
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
            // 1 つの購読先が例外を投げても、残りには届けます。例外は Console に出します。
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
