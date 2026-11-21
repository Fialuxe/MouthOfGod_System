using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 物が届いたときの、フィールドの標準の反応を出します。
    /// <see cref="FieldStages"/> と同じ物に付けます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 欲しい物が届くと、助かった村人の頭の上に粒を出して音を鳴らします。
    /// 頼まれていない物が届くと、困っている村人に Animator のトリガーを送ります。
    /// そのあと、がっかりした音を鳴らします。
    /// トリガーを持たない Animator には、何もしません。動物などがそうです。
    /// </para>
    /// <para>
    /// 粒・煙・音は <see cref="DeliveryReactions"/> のアセットにまとめてあります。
    /// 同じフィールドの <see cref="DeliveryZone"/> と <see cref="PopInEffect"/> も、ここから借ります。
    /// </para>
    /// <para>
    /// 村人の動きを変えるときは、コードを直しません。
    /// Animator Controller で、トリガーで入るステートの Motion を差し替えます。
    /// フィールドごとの反応を足すときは、On Requested と On Missed につなぎます。
    /// </para>
    /// </remarks>
    public sealed class DeliveryFx : MonoBehaviour
    {
        [Tooltip("粒・煙・音の組です。Assets/Data/DeliveryReactions.asset をつなぎます。空だと Play の始めにエラーを出し、粒も音も出ません。")]
        [SerializeField] DeliveryReactions reactions;

        [Tooltip("欲しい物が届いて、ステップが済んだときに、ほかに呼ぶものです。フィールドごとの演出をつなぎます。")]
        [SerializeField] UnityEvent onRequested = new UnityEvent();

        [Tooltip("頼まれていない物が届いたとき、困っている村人の Animator に送るトリガーの名前です。Animator Controller に、この名前のトリガーが無ければ何もしません。")]
        [SerializeField] string missedTrigger = VillagerMood.MissedTrigger;

        [Tooltip("頼まれていない物が届いたときに、ほかに呼ぶものです。フィールドごとの演出をつなぎます。")]
        [SerializeField] UnityEvent onMissed = new UnityEvent();

        [Tooltip("このフィールドの粒と煙を、この物の子として出します。空なら、シーンの直下に出します。")]
        [SerializeField] Transform fxParent;

        FieldStages _stages;
        AudioSource _audio;

        /// <summary>このフィールドの粒・煙・音の組です。</summary>
        /// <value>Inspector で空のときは <see langword="null"/> です。</value>
        public DeliveryReactions Reactions => reactions;

        /// <summary>このフィールドの粒と煙を置く親です。</summary>
        /// <value><see langword="null"/> のときは、シーンの直下に置きます。</value>
        public Transform FxParent => fxParent;

        /// <summary>
        /// <paramref name="from"/> が属するフィールドの <see cref="DeliveryFx"/> を返します。
        /// </summary>
        /// <param name="from">フィールドの中にある部品です。<see langword="null"/> でもかまいません。</param>
        /// <returns>
        /// 親をたどって見つけた <see cref="DeliveryFx"/> です。
        /// 見つからないときや、<paramref name="from"/> が <see langword="null"/> のときは <see langword="null"/> を返します。
        /// </returns>
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
