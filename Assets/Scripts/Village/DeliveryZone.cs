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

        [Tooltip("頼まれていない物（同じ物の 2 つ目や、よその物）が届いたら、その食べ物を消す。消さなければ、フィールドに残る")]
        [SerializeField] bool consumeOthers;

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
            }
            return true;
        }

        IFieldRequests ResolveField()
            => string.IsNullOrEmpty(fieldId) ? GetComponentInParent<IFieldRequests>() : Fields.Find(new FieldId(fieldId));

        // 届けた食べ物の印。同じ食べ物を 2 回届けない。
        sealed class DeliveredMark : MonoBehaviour { }
    }
}
