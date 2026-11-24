using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 物が届いたときと、ステップが済んで物が出てくるときに使う、粒・煙・音の組です。
    /// アセットは Assets/Data/DeliveryReactions.asset の 1 つで、各フィールドの <see cref="DeliveryFx"/> がつなぎます。
    /// </summary>
    /// <remarks>
    /// 落ちた場所（<see cref="DeliveryZone"/>）と出現の演出（<see cref="PopInEffect"/>）は、同じフィールドの DeliveryFx から、この組を借ります。
    /// </remarks>
    [CreateAssetMenu(fileName = "DeliveryReactions", menuName = "MouthOfGod/Village/Delivery Reactions")]
    public sealed class DeliveryReactions : ScriptableObject
    {
        [Header("欲しい物が届いたとき")]
        [Tooltip("助かった村人の頭の上に出す粒。一度だけ出して、終わると自分で消える物にする（Stop Action を Destroy）")]
        [SerializeField] ParticleSystem helpedVillagerSparkles;

        [Tooltip("欲しい物が届いて、ステップが済んだときに鳴らす短い音")]
        [SerializeField] AudioClip requestFulfilledSound;

        [Header("頼まれていない物が届いたとき")]
        [Tooltip("頼まれていない物が、小さくなって消えるときに出す煙")]
        [SerializeField] GameObject wrongFoodSmoke;

        [Tooltip("頼まれていない物が届いたときに鳴らす、がっかりした音")]
        [SerializeField] AudioClip wrongFoodSound;

        [Header("ステップが済んで、物が出てくるとき")]
        [Tooltip("新しく出てきた物の場所に、一度だけ出す粒")]
        [SerializeField] GameObject stepItemSparkles;

        [Tooltip("Step Item Sparkles の大きさの倍率。村人の頭の上の粒より目立たせる")]
        [SerializeField, Min(0f)] float stepItemSparklesScale = 2.5f;

        [Header("音")]
        [Tooltip("上の 2 つの音の大きさ")]
        [SerializeField, Range(0f, 1f)] float volume = 0.6f;

        public ParticleSystem HelpedVillagerSparkles => helpedVillagerSparkles;
        public AudioClip RequestFulfilledSound => requestFulfilledSound;
        public GameObject WrongFoodSmoke => wrongFoodSmoke;
        public AudioClip WrongFoodSound => wrongFoodSound;
        public GameObject StepItemSparkles => stepItemSparkles;
        public float StepItemSparklesScale => stepItemSparklesScale;
        public float Volume => volume;
    }
}
