using System;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Food
{
    /// <summary>
    /// デバイスで決定した食感から、食べ物を引いて生み、前へ飛ばす。
    /// 食感が決まったとき（<see cref="IDevice.Confirmed"/>）に、<see cref="FoodTables.Resolve"/> で Prefab を引き、<c>Spawn Point</c> の位置に生む。
    /// </summary>
    /// <remarks>
    /// - デバイスは Core の <see cref="Devices.WhenReady"/> で受け取る。Mock か実機かは知らない。
    /// - 飛ばす: <c>Launch Speed</c> が 0 より大きく、生んだ食べ物に Rigidbody があれば、<c>Spawn Point</c> の前向きに初速を与える。そのあとの動きは、食べ物の Rigidbody（Mass など）と物理しだい。
    ///   体験者は、顔を向けて狙いを定め、欲しがっている人のところへ飛ばす。どこに届いたかは、落ちた所の DeliveryZone（Village）が決める。
    /// - 生んだ食べ物には、どの食べ物かを渡す <see cref="IFoodItem"/>（<c>FoodItemTag</c>）を付ける。
    /// - 「体験者の前のフィールド」は、Core の <see cref="Standing.Field"/> を読む。<see cref="CurrentField"/> に関数を入れたときは、そちらを使う（テストや、別の決め方を試すとき）。
    /// - 有効になると、自分で Core の <see cref="FoodSpawners"/> に登録する。チュートリアルや影の UI は、そこから受け取る。
    /// - 間違えたとき（欲しい物と違う食べ物が届いたとき）の反応は、ここにはない。届いた知らせ（<see cref="IFieldRequests.Received"/>）を受ける側が足す。
    /// </remarks>
    public sealed class FoodSpawner : MonoBehaviour, IFoodSpawner
    {
        [Tooltip("食べ物が生まれる位置と向き（口の位置。MouthAnchor を置いたら、ここに入れる）。空なら、このオブジェクトの位置と向き")]
        [SerializeField] Transform spawnPoint;

        [Tooltip("生んだ食べ物に、Spawn Point の前向きに与える初速（m/s）。0 以下、または食べ物に Rigidbody がなければ、与えない。そのあとの動きは、食べ物の Rigidbody（Mass など）しだい")]
        [SerializeField, Min(0f)] float launchSpeed = 2f;

        // 警告と例外の出し先。テストで差し替える。
        internal static Action<string> ReportWarning = Debug.LogWarning;
        internal static Action<Exception> ReportException = Debug.LogException;

        IDevice _device;

        /// <summary>
        /// 体験者の前のフィールドを返す関数。入れたときだけ、<see cref="Standing.Field"/> の代わりに使う。
        /// 空のままなら、Core の <see cref="Standing.Field"/>（立っている場所。どこにも立っていなければ共通の表だけ）。
        /// </summary>
        public Func<FieldId> CurrentField { get; set; }

        /// <summary>食べ物を生んだ直後に、1 回出る。</summary>
        public event Action<Spawned> Spawned;

        /// <summary>
        /// 食感から、生まれる食べ物の Prefab を引くだけ。生まない。
        /// チュートリアルで、噛んでいる間に、その食感で生まれる物の影を見せるときに使う。引けなければ null。
        /// </summary>
        public GameObject Preview(TextureId texture) => FoodTables.Resolve(ResolveField(), texture);

        /// <summary>
        /// 食感から食べ物を引いて、1 つ生む。生んだ食べ物を返す。
        /// 食感が空（噛む前の決定）なら、何もせず null。引けなければ、警告を出して null。
        /// </summary>
        public GameObject Spawn(TextureId texture)
        {
            if (texture.IsEmpty) return null;

            FieldId field = ResolveField();
            GameObject prefab = FoodTables.Resolve(field, texture);
            if (prefab == null)
            {
                ReportWarning("[FoodSpawner] 食感「" + texture + "」に対応する食べ物がありません（フィールド: "
                              + (field.IsEmpty ? "なし" : field.Value) + "）。");
                return null;
            }

            Debug.Log("[FoodSpawner] " + (field.IsEmpty ? "フィールドなし（共通の表）" : field.Value) + " / " + texture + " → " + prefab.name);

            Transform origin = spawnPoint != null ? spawnPoint : transform;
            GameObject instance = Instantiate(prefab, origin.position, origin.rotation);
            instance.name = prefab.name;

            FoodId food = FoodTables.IdOf(prefab);
            FoodItemTag tag;
            if (!instance.TryGetComponent<FoodItemTag>(out tag)) tag = instance.AddComponent<FoodItemTag>();
            tag.Food = food;

            Launch(instance, origin);
            RaiseSpawned(new Spawned(food, instance.transform.position));
            return instance;
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

        FieldId ResolveField()
        {
            Func<FieldId> provider = CurrentField;
            return provider != null ? provider() : Standing.Field;
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

        // デバイスの決定を受け取る。OnEnable で頼み、OnDisable で外す。テストからも呼ぶ。
        internal void Attach() => Devices.WhenReady(Bind);

        internal void Detach()
        {
            Devices.CancelWhenReady(Bind);
            Unbind();
        }

        void Bind(IDevice device)
        {
            Unbind();
            _device = device;
            _device.Confirmed += OnConfirmed;
        }

        void Unbind()
        {
            if (_device == null) return;
            _device.Confirmed -= OnConfirmed;
            _device = null;
        }

        void OnConfirmed(TextureId texture) => Spawn(texture);

        void OnEnable()
        {
            Attach();
            FoodSpawners.Register(this);
        }

        void OnDisable()
        {
            Detach();
            FoodSpawners.Unregister(this);
        }

        // 表がそろってから（OnEnable がすべて済んでから）、食い違いを 1 回出す。
        void Start()
        {
            foreach (string warning in FoodTables.Validate())
                ReportWarning("[FoodSpawner] " + warning);
        }
    }
}
