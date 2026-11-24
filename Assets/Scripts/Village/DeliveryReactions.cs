using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 物が届いたときと、ステップが済んで物が出てくるときに使う、粒・煙・音の組です。
    /// </summary>
    /// <remarks>
    /// <para>
    /// アセットは Assets/Data/DeliveryReactions.asset の 1 つだけです。
    /// 各フィールドの <see cref="DeliveryFx"/> がつなぎます。
    /// </para>
    /// <para>
    /// <see cref="DeliveryZone"/> と <see cref="PopInEffect"/> は、この組を直接持ちません。
    /// 同じフィールドの <see cref="DeliveryFx"/> から借ります。
    /// </para>
    /// </remarks>
    [CreateAssetMenu(fileName = "DeliveryReactions", menuName = "MouthOfGod/Village/Delivery Reactions")]
    public sealed class DeliveryReactions : ScriptableObject
    {
        [Header("欲しい物が届いたとき")]
        [Tooltip("助かった村人の頭の上に出す粒です。一度だけ出て、終わると自分で消える物にします（Main の Stop Action を Destroy にします）。空なら粒を出しません。")]
        [SerializeField] ParticleSystem helpedVillagerSparkles;

        [Tooltip("欲しい物が届いて、ステップが済んだときに鳴らす短い音です。空なら鳴らしません。")]
        [SerializeField] AudioClip requestFulfilledSound;

        [Header("頼まれていない物が届いたとき")]
        [Tooltip("頼まれていない物が、小さくなって消えるときに出す煙です。食べ物の位置に一度だけ出します。空なら煙を出しません。")]
        [SerializeField] GameObject wrongFoodSmoke;

        [Tooltip("頼まれていない物が届いたときに鳴らす、がっかりした音です。届くたびに、この中から 1 つを選びます。空なら鳴らしません。")]
        [SerializeField] AudioClip[] wrongFoodSounds;

        [Header("ステップが済んで、物が出てくるとき")]
        [Tooltip("新しく出てきた物の場所に、一度だけ出す粒です。空なら粒を出しません。")]
        [SerializeField] GameObject stepItemSparkles;

        [Tooltip("Step Item Sparkles の大きさの倍率です。村人の頭の上の粒より目立たせるため、1 より大きくします。")]
        [SerializeField, Min(0f)] float stepItemSparklesScale = 2.5f;

        [Header("音")]
        [Tooltip("上の 2 つの音の大きさです（0〜1）。1 で、音の素材のままの大きさになります。")]
        [SerializeField, Range(0f, 1f)] float volume = 0.6f;

        public ParticleSystem HelpedVillagerSparkles => helpedVillagerSparkles;
        public AudioClip RequestFulfilledSound => requestFulfilledSound;
        public GameObject WrongFoodSmoke => wrongFoodSmoke;

        int _lastWrongFoodSoundIndex = -1;
        public GameObject StepItemSparkles => stepItemSparkles;
        public float StepItemSparklesScale => stepItemSparklesScale;
        public float Volume => volume;

        /// <summary>間違った物が届いたときの音を 1 つ選びます。</summary>
        /// <remarks>2 つ以上あるときは、直前と同じ音を続けて選びません。</remarks>
        /// <returns>選んだ音です。1 つも入れていなければ <see langword="null"/> です。</returns>
        public AudioClip PickWrongFoodSound()
        {
            if (wrongFoodSounds == null || wrongFoodSounds.Length == 0) return null;
            int index = Random.Range(0, wrongFoodSounds.Length);
            if (wrongFoodSounds.Length > 1 && index == _lastWrongFoodSoundIndex)
                index = (index + 1 + Random.Range(0, wrongFoodSounds.Length - 1)) % wrongFoodSounds.Length;
            _lastWrongFoodSoundIndex = index;
            return wrongFoodSounds[index];
        }
    }
}
