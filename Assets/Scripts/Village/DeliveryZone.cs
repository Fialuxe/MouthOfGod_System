using System.Collections;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 食べ物が落ちる場所です。食べ物が入ると、そのフィールドの <see cref="IFieldRequests.Receive"/> を呼びます。
    /// Trigger の Collider と一緒に付けます。
    /// </summary>
    /// <remarks>
    /// 欲しがっていた物かどうかは、フィールドが決めます。ここでは決めません。
    /// 食べ物は Core の <see cref="IFoodItem"/> として読むので、Food のコードには依存しません。
    /// 1 つの食べ物は 1 回しか届けません。届いたあとに残した物が、もう一度入っても数えません。
    /// 入ってすぐには判定せず、<c>judgeDelaySeconds</c> だけ待ちます。その間に、体験者が何を届けたかを目で確かめられます。
    /// 判定のあと、欲しがっていた物は消し、それ以外は煙を出して小さくしながら消します。どちらも Inspector で変えられます。
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public sealed class DeliveryZone : MonoBehaviour
    {
        [Tooltip("届けるフィールドの ID（village など）。空なら、親にあるフィールド（FieldStages）に届ける")]
        [SerializeField] string fieldId = "";

        [Tooltip("食べ物が入ってから、判定するまでの秒数。この間、食べ物は落ちたまま見えている。0 ならすぐ判定する")]
        [SerializeField, Min(0f)] float judgeDelaySeconds = 1f;

        [Tooltip("頼まれた物が届いたら、その食べ物を消す")]
        [SerializeField] bool consumeRequested = true;

        [Tooltip("頼まれていない物（同じ物の 2 つ目や、よその物）が届いたら、その食べ物をすぐ消す。Vanish Others より先に効く")]
        [SerializeField] bool consumeOthers;

        [Tooltip("頼まれていない物が届いたら、煙を出して、だんだん小さくして消す（霞のように消える）。外すと、フィールドに残る")]
        [SerializeField] bool vanishOthers = true;

        [Tooltip("消えるまでの秒数")]
        [SerializeField, Min(0f)] float vanishSeconds = 1f;


        void Reset()
        {
            foreach (var collider in GetComponents<Collider>()) collider.isTrigger = true;
        }

        void OnTriggerEnter(Collider other) => Deliver(other);

        /// <summary>
        /// 入った物が食べ物なら、フィールドに届けます。判定は <c>judgeDelaySeconds</c> のあとです。
        /// 食べ物でない・もう届けた・届け先が見つからないときは false です。
        /// </summary>
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

        void Vanish(GameObject food)
        {
            var fx = DeliveryFx.Of(this);
            var smoke = fx != null && fx.Reactions != null ? fx.Reactions.WrongFoodSmoke : null;
            if (smoke != null) Instantiate(smoke, food.transform.position, Quaternion.identity, fx.FxParent);
            food.AddComponent<Vanishing>().Seconds = vanishSeconds;
        }

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

        sealed class DeliveredMark : MonoBehaviour { }
    }
}
