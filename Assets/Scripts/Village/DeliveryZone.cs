using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 食べ物が落ちる場所。フィールドの上に置き、食べ物が入ったら、そのフィールドの <see cref="IFieldRequests.Receive"/> を呼ぶ。
    /// Collider（Is Trigger）と一緒に付ける。頼まれた物かどうかは、フィールドが判断する。ここでは判断しない。
    /// </summary>
    /// <remarks>
    /// - 食べ物は、Core の <see cref="IFoodItem"/> で読む。Food のコードは使わない。
    /// - 届けるフィールドは、<c>Field Id</c> に書いた ID（<see cref="Fields"/> から引く）。空なら、親にあるフィールド（FieldStages）。
    /// - 1 つの食べ物は、1 回しか届けない（残した物が、もう一度入っても数えない）。
    /// - 届けたあとの食べ物は、頼まれた物（Requested）なら消す。それ以外は残す。どちらも Inspector で変えられる。
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public sealed class DeliveryZone : MonoBehaviour
    {
        [Tooltip("届けるフィールドの ID（village など）。空なら、親にあるフィールド（FieldStages）に届ける")]
        [SerializeField] string fieldId = "";

        [Tooltip("頼まれた物が届いたら、その食べ物を消す")]
        [SerializeField] bool consumeRequested = true;

        [Tooltip("頼まれていない物（同じ物の 2 つ目や、よその物）が届いたら、その食べ物をすぐ消す。Vanish Others より先に効く")]
        [SerializeField] bool consumeOthers;

        [Tooltip("頼まれていない物が届いたら、煙を出して、だんだん小さくして消す（霞のように消える）。外すと、フィールドに残る")]
        [SerializeField] bool vanishOthers = true;

        [Tooltip("消えるまでの秒数")]
        [SerializeField, Min(0f)] float vanishSeconds = 1f;

        [Tooltip("消えるときに出す物（煙など）。空なら Resources/Prefabs/Village/Fx_Smoke")]
        [SerializeField] GameObject vanishFx;

        // Component を付けたとき、Collider を Is Trigger にする。
        void Reset()
        {
            foreach (var collider in GetComponents<Collider>()) collider.isTrigger = true;
        }

        void OnTriggerEnter(Collider other) => Deliver(other);

        /// <summary>
        /// 入った物が食べ物なら、フィールドに届ける。届けたら true。
        /// 食べ物でない・もう届けた・届け先のフィールドが見つからないときは、何もせず false。
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

            var kind = field.Receive(item.Food);
            if (kind == DeliveryKind.Requested ? consumeRequested : consumeOthers)
            {
                if (Application.isPlaying) Destroy(food);
                else DestroyImmediate(food);
            }
            else
            {
                food.AddComponent<DeliveredMark>();
                if (vanishOthers && Application.isPlaying) Vanish(food);
            }
            return true;
        }

        void Vanish(GameObject food)
        {
            var fx = vanishFx != null ? vanishFx : Resources.Load<GameObject>("Prefabs/Village/Fx_Smoke");
            if (fx != null) Instantiate(fx, food.transform.position, Quaternion.identity);
            food.AddComponent<Vanishing>().Seconds = vanishSeconds;
        }

        // 届いた違う物を、だんだん小さくして消す。
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

        // 届けた食べ物の印。同じ食べ物を 2 回届けない。
        sealed class DeliveredMark : MonoBehaviour { }
    }
}
