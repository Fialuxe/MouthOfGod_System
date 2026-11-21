using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 物が届いたときの、フィールドの標準の反応です。<see cref="FieldStages"/> と同じ物に付けます。
    /// </summary>
    /// <remarks>
    /// 欲しい物が届くと、助かった村人の頭の上に粒を出して音を鳴らします。
    /// 頼まれていない物が届くと、困っている村人に Animator のトリガーを送り、がっかりした音を鳴らします。
    /// 粒・煙・音は <see cref="DeliveryReactions"/> のアセットにまとめてあり、同じフィールドの DeliveryZone と PopInEffect も、ここから借ります。
    /// 村人の動きを変えるときは、Animator Controller の、トリガーで入るステートの Motion を差し替えます。コードは直しません。
    /// トリガーを持たない Animator（動物など）には、何もしません。
    /// フィールドごとの反応を足すときは、On Requested と On Missed につなぎます。
    /// </remarks>
    public sealed class DeliveryFx : MonoBehaviour
    {
        [Tooltip("粒・煙・音の組（Assets/Data/DeliveryReactions.asset）")]
        [SerializeField] DeliveryReactions reactions;

        [Tooltip("欲しい物が届いたときに、ほかに呼ぶもの")]
        [SerializeField] UnityEvent onRequested = new UnityEvent();

        [Tooltip("困っている村人の Animator に送るトリガーの名前。Animator Controller に、この名前のトリガーが無ければ何もしない")]
        [SerializeField] string missedTrigger = VillagerMood.MissedTrigger;

        [Tooltip("頼まれていない物が届いたときに、ほかに呼ぶもの")]
        [SerializeField] UnityEvent onMissed = new UnityEvent();

        [Tooltip("このフィールドの粒と煙を、この子として出す。空ならシーンの直下")]
        [SerializeField] Transform fxParent;

        FieldStages _stages;
        AudioSource _audio;

        /// <summary>このフィールドの粒・煙・音の組です。</summary>
        public DeliveryReactions Reactions => reactions;

        /// <summary>このフィールドの粒と煙を置く親です。空ならシーンの直下です。</summary>
        public Transform FxParent => fxParent;

        /// <summary>from が属するフィールドの DeliveryFx です。見つからなければ null です。</summary>
        public static DeliveryFx Of(Component from) => from != null ? from.GetComponentInParent<DeliveryFx>() : null;

        void Awake()
        {
            if (reactions == null) Debug.LogError($"[DeliveryFx] {name}: 粒・煙・音の組（Reactions）が空です。", this);
            // 2D の音にします。ミニチュアは小さいので、3D だと聞く位置しだいで聞こえないことがあります。
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
        }

        void OnEnable()
        {
            _stages = GetComponent<FieldStages>();
            if (_stages == null) return;
            _stages.StepDone += OnStepDone;
            _stages.Received += OnReceived;
        }

        void OnDisable()
        {
            if (_stages == null) return;
            _stages.StepDone -= OnStepDone;
            _stages.Received -= OnReceived;
        }

        void OnStepDone(FieldStep step)
        {
            if (reactions != null && reactions.HelpedVillagerSparkles != null)
            {
                var any = false;
                foreach (var mood in step.Helped)
                {
                    if (mood == null) continue;
                    Burst(HeadOf(mood));
                    any = true;
                }
                if (!any) Burst(step.Anchor.position);
            }
            Play(reactions != null ? reactions.RequestFulfilledSound : null);
            onRequested.Invoke();
        }

        void OnReceived(Delivered delivered)
        {
            if (delivered.Kind == DeliveryKind.Requested) return; // 喜ぶ反応は OnStepDone で出します
            foreach (var mood in GetComponentsInChildren<VillagerMood>())
                if (mood.IsWorried) mood.React(missedTrigger);
            Play(reactions != null ? reactions.WrongFoodSound : null);
            onMissed.Invoke();
        }

        void Play(AudioClip clip)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, reactions.Volume);
        }

        static Vector3 HeadOf(VillagerMood mood)
            => mood.WorryMark != null ? mood.WorryMark.transform.position : mood.transform.position;

        void Burst(Vector3 position) => Instantiate(reactions.HelpedVillagerSparkles, position, Quaternion.identity, fxParent);
    }
}
