using System.Collections;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 食べ物が落ちる場所です。
    /// 食べ物が入り、困っている村人の近くに落ちていると、そのフィールドの <see cref="IFieldRequests.Receive"/> を呼びます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// Trigger の Collider と一緒に付けます。
    /// 欲しがっていた物かどうかは、フィールドが決めます。ここでは決めません。
    /// 食べ物は Core の <see cref="IFoodItem"/> として読みます。
    /// そのため、Food のコードには依存しません。
    /// </para>
    /// <para>
    /// 1 つの食べ物は 1 回しか届けません。
    /// 届いたあとに残した物が、もう一度入っても数えません。
    /// </para>
    /// <para>
    /// 入ってすぐには判定せず、<c>judgeDelaySeconds</c> だけ待ちます。
    /// その間に、体験者は何を届けたかを目で確かめられます。
    /// 落ちた所が、困っている村人から <c>acceptRadiusMeters</c> より遠いときは、届いたことにしません。
    /// フィールドの中でも、村人から離れた所では受け入れないためです。
    /// そのときは、煙を出して消します。
    /// 判定のあと、欲しがっていた物は消します。
    /// それ以外は、煙を出して小さくしながら消します。
    /// どちらも Inspector で変えられます。
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public sealed class DeliveryZone : MonoBehaviour
    {
        [Tooltip("届けるフィールドの ID です（village など）。空なら、親にあるフィールド（FieldStages）に届けます。")]
        [SerializeField] string fieldId = "";

        [Tooltip("食べ物が入ってから、判定するまでの秒数です。この間、食べ物は落ちたまま見えています。0 ならすぐ判定します。")]
        [SerializeField, Min(0f)] float judgeDelaySeconds = 1f;

        [Tooltip("困っている村人から、この距離（メートル）より近い所に落ちた食べ物だけを、届いたことにします。水平の距離で測ります。0 なら、フィールドのどこでも届いたことにします。")]
        [SerializeField, Min(0f)] float acceptRadiusMeters = 3.5f;

        [Tooltip("頼まれた物が届いたら、その食べ物を消します。")]
        [SerializeField] bool consumeRequested = true;

        [Tooltip("頼まれていない物が届いたら、その食べ物をすぐ消します。同じ物の 2 つ目や、よそのフィールドの物がそうです。Vanish Others より先に効きます。")]
        [SerializeField] bool consumeOthers;

        [Tooltip("頼まれていない物が届いたら、煙を出して、だんだん小さくして消します（霞のように消えます）。外すと、フィールドに残ります。")]
        [SerializeField] bool vanishOthers = true;

        [Tooltip("Vanish Others で、小さくなって消えるまでの秒数です。0 なら次のフレームで消えます。")]
        [SerializeField, Min(0f)] float vanishSeconds = 1f;


        void Reset()
        {
            foreach (var collider in GetComponents<Collider>()) collider.isTrigger = true;
        }

        void OnTriggerEnter(Collider other) => Deliver(other);

        /// <summary>
        /// 入った物が食べ物なら、フィールドに届けます。
        /// 判定は <c>judgeDelaySeconds</c> のあとに行います。
        /// </summary>
        /// <param name="other">入った物の部品です。食べ物の子でもかまいません。</param>
        /// <returns>
        /// 届けたときは <see langword="true"/> を返します。
        /// 食べ物でない・もう届けた・届け先が見つからないときは <see langword="false"/> を返します。
        /// </returns>
        /// <remarks>
        /// Play 中でないときは、待たずにすぐ判定します。
        /// コルーチンが動かないためです。
        /// </remarks>
        public bool Deliver(Component other)
        {
            var item = other.GetComponentInParent<IFoodItem>();
            var component = item as Component;
            if (component == null) return false;

            var food = component.gameObject;
            if (food.TryGetComponent<DeliveredMark>(out _)) return false;

            var field = ResolveField();
            if (field == null)
            {
                Debug.LogWarning($"[DeliveryZone] 届け先のフィールドが見つからない（Field Id: 「{fieldId}」）。食べ物は届けない", this);
                return false;
            }

            food.AddComponent<DeliveredMark>(); // 待っている間に、もう一度入っても数えないようにします
            if (judgeDelaySeconds > 0f && Application.isPlaying) StartCoroutine(JudgeLater(food, item, field));
            else Judge(food, item, field);
            return true;
        }

        IEnumerator JudgeLater(GameObject food, IFoodItem item, IFieldRequests field)
        {
            yield return new WaitForSeconds(judgeDelaySeconds);
            if (food != null) Judge(food, item, field); // 待っている間に片付けられた食べ物は、判定しません
        }

        void Judge(GameObject food, IFoodItem item, IFieldRequests field)
        {
            if (!IsNearWorriedVillager(food.transform.position, field))
            {
                if (Application.isPlaying) Vanish(food);
                else DestroyImmediate(food);
                return;
            }

            var kind = field.Receive(item.Kind);
            if (kind == DeliveryKind.Requested ? consumeRequested : consumeOthers)
            {
                if (Application.isPlaying) Destroy(food);
                else DestroyImmediate(food);
            }
            else if (vanishOthers && Application.isPlaying)
            {
                Vanish(food);
            }
        }

        // 困っている村人のうち、いちばん近い人までの水平の距離が、acceptRadiusMeters 以内かを返します。
        bool IsNearWorriedVillager(Vector3 position, IFieldRequests field)
        {
            if (acceptRadiusMeters <= 0f) return true;

            var fieldRoot = field as Component;
            if (fieldRoot == null) return true; // 村人を探せないときは、これまでどおり受け入れます

            foreach (var mood in fieldRoot.GetComponentsInChildren<VillagerMood>())
            {
                if (!mood.IsWorried) continue;
                Vector3 offset = mood.transform.position - position;
                offset.y = 0f;
                if (offset.sqrMagnitude <= acceptRadiusMeters * acceptRadiusMeters) return true;
            }
            return false;
        }

        void Vanish(GameObject food)
        {
            var fx = DeliveryFx.Of(this);
            var smoke = fx != null && fx.Reactions != null ? fx.Reactions.WrongFoodSmoke : null;
            if (smoke != null) Instantiate(smoke, food.transform.position, Quaternion.identity, fx.FxParent);
            food.AddComponent<Vanishing>().Seconds = vanishSeconds;
        }

        // 付けた物を Seconds 秒かけて小さくし、最後に Destroy します。
        sealed class Vanishing : MonoBehaviour
        {
            public float Seconds = 1f;
            Vector3 _scale;
            float _t;

            void Start() => _scale = transform.localScale;

            void Update()
            {
                _t += Time.deltaTime;
                float k = Seconds > 0f ? Mathf.Clamp01(_t / Seconds) : 1f;
                transform.localScale = _scale * (1f - k);
                if (k >= 1f) Destroy(gameObject);
            }
        }

        IFieldRequests ResolveField()
            => string.IsNullOrEmpty(fieldId) ? GetComponentInParent<IFieldRequests>() : Fields.Find(new FieldId(fieldId));

        // 届けた食べ物に付ける印です。同じ食べ物を 2 回届けないために使います。
        sealed class DeliveredMark : MonoBehaviour { }
    }
}
