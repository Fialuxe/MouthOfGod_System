using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 体験者のリグです。頭・口・デバイス・フォーク・食べ物を生む所を、1 つにまとめて持ちます。
    /// リグの Prefab の一番上に付けます。Quest のリグと、PC で試すリグの両方にあります。
    /// </summary>
    /// <remarks>
    /// 有効なリグは、いつも 1 つだけにします。使わない方のリグは、シーンで無効にしておきます。
    /// 場面のスクリプトやほかの部品は、<see cref="Current"/> からリグを受け取ります。相手が Quest か PC かは知りません。
    /// リグの中の部品（フォーク・食べ物を生む所など）は、自分の親のリグからデバイスと口を受け取ります。
    /// 中身は Inspector でつなぎます。Awake より前に読まれても困らないよう、つないだ値をそのまま返します。
    /// </remarks>
    public sealed class PlayerRig : MonoBehaviour
    {
        [Tooltip("頭（目のカメラ）。Quest なら CenterEyeAnchor、PC なら Main Camera")]
        [SerializeField] Transform head;

        [Tooltip("口の位置と向き。食べ物はここから生まれ、フォークの先がここに近づくと口に入ったことになる。頭の子に置く")]
        [SerializeField] Transform mouth;

        [Tooltip("触感デバイス。IDevice を実装した部品を入れる（いまは MockDevice）。実機ができたら差し替える")]
        [SerializeField] MonoBehaviour device;

        [Tooltip("フォークの先。IFork を実装した部品を入れる（ForkTip）")]
        [SerializeField] MonoBehaviour fork;

        [Tooltip("食べ物を生む所。IFoodSpawner を実装した部品を入れる（FoodSpawner）")]
        [SerializeField] MonoBehaviour foodSpawner;

        /// <summary>いま有効なリグです。まだ無ければ null です。</summary>
        public static PlayerRig Current { get; private set; }

        public Transform Head => head;
        public Transform Mouth => mouth;
        public IDevice Device => device as IDevice;
        public IFork Fork => fork as IFork;
        public IFoodSpawner FoodSpawner => foodSpawner as IFoodSpawner;

        /// <summary>from が属するリグです。リグの外なら null です。</summary>
        public static PlayerRig Of(Component from) => from != null ? from.GetComponentInParent<PlayerRig>(true) : null;

        void OnEnable()
        {
            if (Current != null && Current != this)
                Debug.LogWarning($"[PlayerRig] {Current.name} も有効です。使わない方のリグを、シーンで無効にしてください。{name} を使います。", this);
            Current = this;
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
        }

        void OnValidate()
        {
            Check(device, "Device", typeof(IDevice));
            Check(fork, "Fork", typeof(IFork));
            Check(foodSpawner, "Food Spawner", typeof(IFoodSpawner));
        }

        void Check(MonoBehaviour value, string label, System.Type type)
        {
            if (value != null && !type.IsInstanceOfType(value))
                Debug.LogError($"[PlayerRig] {label} に入れた {value.GetType().Name} は {type.Name} ではありません。", this);
        }

        // Play のたびに空にします。ドメインのリロードを切っていても、前回のリグを残しません。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCurrent() => Current = null;
    }
}
