using UnityEngine;

namespace MouthOfGod.Core
{
    /// <summary>
    /// 体験者のリグです。頭・口・デバイス・フォーク・食べ物を生む所を、1 つにまとめて持ちます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// リグの Prefab の一番上に付けます。
    /// Quest のリグと、PC で試すリグの両方にあります。
    /// 有効なリグは、いつも 1 つだけにします。
    /// 使わない方のリグは、シーンで無効にしておきます。
    /// </para>
    /// <para>
    /// 場面のスクリプトやほかの部品は、<see cref="Current"/> からリグを受け取ります。
    /// 相手が Quest か PC かは知りません。
    /// リグの中の部品は、<see cref="Of"/> で自分の親のリグを引き、デバイスと口を受け取ります。
    /// </para>
    /// <para>
    /// 中身は Inspector でつなぎます。
    /// Awake より前に読まれても困らないよう、つないだ値をそのまま返します。
    /// </para>
    /// </remarks>
    public sealed class PlayerRig : MonoBehaviour
    {
        [Tooltip("頭（目のカメラ）です。Quest なら CenterEyeAnchor、PC なら Main Camera を入れます。")]
        [SerializeField] Transform head;

        [Tooltip("口の位置と向きです。食べ物はここから生まれます。フォークの先がここに近づくと、口に入ったことになります。頭の子に置きます。")]
        [SerializeField] Transform mouth;

        [Tooltip("触感デバイスです。IDevice を実装した部品を入れます（いまは MockDevice）。実機ができたら差し替えます。")]
        [SerializeField] MonoBehaviour device;

        [Tooltip("フォークの先です。IFork を実装した部品（ForkTip）を入れます。")]
        [SerializeField] MonoBehaviour fork;

        [Tooltip("食べ物を生む所です。IFoodSpawner を実装した部品（FoodSpawner）を入れます。")]
        [SerializeField] MonoBehaviour foodSpawner;

        /// <summary>いま有効なリグです。まだ無ければ <see langword="null"/> です。</summary>
        public static PlayerRig Current { get; private set; }

        public Transform Head => head;
        public Transform Mouth => mouth;

        /// <summary>つないだデバイスです。つないでいないか、IDevice でなければ <see langword="null"/> です。</summary>
        public IDevice Device => device as IDevice;

        /// <summary>つないだフォークです。つないでいないか、IFork でなければ <see langword="null"/> です。</summary>
        public IFork Fork => fork as IFork;

        /// <summary>つないだ食べ物を生む所です。つないでいないか、IFoodSpawner でなければ <see langword="null"/> です。</summary>
        public IFoodSpawner FoodSpawner => foodSpawner as IFoodSpawner;

        /// <summary>指定した部品が属するリグを返します。</summary>
        /// <param name="from">調べる部品です。<see langword="null"/> なら <see langword="null"/> を返します。</param>
        /// <returns>親をたどって見つかったリグです。リグの外なら <see langword="null"/> です。</returns>
        /// <remarks>無効なリグも探します。</remarks>
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

        // Inspector の欄は MonoBehaviour 型なので、違う部品を入れても弾けません。ここでエラーを出して知らせます。
        void Check(MonoBehaviour value, string label, System.Type type)
        {
            if (value != null && !type.IsInstanceOfType(value))
                Debug.LogError($"[PlayerRig] {label} に入れた {value.GetType().Name} は {type.Name} ではありません。", this);
        }

        // Play のたびに空にします。ドメインのリロードを切っていても、前回のリグを残さないためです。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCurrent() => Current = null;
    }
}
