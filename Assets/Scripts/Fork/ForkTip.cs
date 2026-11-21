using System;
using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Fork
{
    /// <summary>
    /// 体験者が持つフォークの先です。雲に触れたら刺したとし、口に近づいたら口に入ったとします。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 体験者のリグ（<see cref="PlayerRig"/>）の子で、フォークの先の位置に置きます。
    /// デバイスと口は、親のリグから受け取ります。
    /// </para>
    /// <para>
    /// 位置の取り方は知りません。
    /// 手のトラッキングや AprilTag から位置を推定する部品が、このオブジェクトか親を動かします。
    /// </para>
    /// <para>
    /// 刺したかは、先の小さなトリガーが <see cref="Cloud"/> の当たり判定に入ったかで決めます。
    /// 口に入ったかは、先と <see cref="PlayerRig.Mouth"/> の距離で決めます。
    /// かけらは、デバイスで決定したときになくなります。食べ物が生まれるのと同じときです。
    /// </para>
    /// <para>
    /// Quest がなくても試せるよう、キーで「刺す」「口に入れる」を代わりにできます。
    /// 展示では使いません。
    /// </para>
    /// </remarks>
    public sealed class ForkTip : MonoBehaviour, IFork
    {
        [Tooltip("先と口の距離がこれ以下になったら、口に入ったとする（m）。仮の値")]
        [SerializeField, Min(0f)] float mouthEnterRadius = 0.06f;

        [Tooltip("口に入ったあと、これより離れたら出たとする（m）。境目で入る・出るをくり返さないよう、Enter より少し大きくする。仮の値")]
        [SerializeField, Min(0f)] float mouthExitRadius = 0.09f;

        [Tooltip("先の当たり判定の半径（m）。雲に触れたかを見る。大きくすると刺しやすくなるが、隣の雲も刺しやすくなる")]
        [SerializeField, Min(0.001f)] float tipRadius = 0.02f;

        [Tooltip("刺した雲のかけらの見た目。刺している間だけ表示する。空なら何も出さない")]
        [SerializeField] GameObject loadedPiece;

        [Tooltip("Quest なしで試すときの「刺す」。展示では使わない")]
        [SerializeField] InputAction pierceAction = new InputAction("Pierce", InputActionType.Button, "<Keyboard>/p");

        [Tooltip("Quest なしで試すときの「口に入れる」（押している間）。M キーのほか、ハンドトラッキングで右手を握っている間も、口の中にあることにする。展示では使わない")]
        [SerializeField] InputAction mouthAction = new InputAction("Mouth", InputActionType.Button, "<Keyboard>/m");

        [Tooltip("刺す・口に入った・口から出た・食べたを、Console に出す。動いているかを確かめるときに使う")]
        [SerializeField] bool logEvents = true;

        // 試すときに、右手を握っている間も口の中とするための割り当てです。
        const string MockMouthHandBinding = "<HandInteraction>{RightHand}/graspValue";

        ForkLogic _logic;
        IDevice _device;
        Transform _mouth;

        /// <inheritdoc/>
        public bool IsLoaded => _logic != null && _logic.IsLoaded;

        /// <inheritdoc/>
        public bool InMouth => _logic != null && _logic.InMouth;

        /// <inheritdoc/>
        public event Action Pierced;

        /// <inheritdoc/>
        public event Action MouthEntered;

        /// <inheritdoc/>
        public event Action MouthLeft;

        /// <inheritdoc/>
        public event Action Emptied;

        void Awake()
        {
            _logic = new ForkLogic(mouthEnterRadius, mouthExitRadius);
            _logic.Pierced += () => Raise(Pierced, "刺した");
            _logic.MouthEntered += () => Raise(MouthEntered, "口に入った");
            _logic.MouthLeft += () => Raise(MouthLeft, "口から出た");
            _logic.Emptied += () => Raise(Emptied, "食べた");
            PrepareTip();
            ShowPiece();
        }

        void OnEnable()
        {
            pierceAction.Enable();
            AddBindingOnce(mouthAction, MockMouthHandBinding);
            mouthAction.Enable();

            PlayerRig rig = PlayerRig.Of(this);
            _mouth = rig != null ? rig.Mouth : null;
            _device = rig != null ? rig.Device : null;
            if (_device != null) _device.Confirmed += OnConfirmed;
            else Debug.LogError("[Fork] 親のリグ（PlayerRig）にデバイスがないので、決定してもかけらがなくなりません。", this);
        }

        void OnDisable()
        {
            pierceAction.Disable();
            mouthAction.Disable();
            if (_device != null) _device.Confirmed -= OnConfirmed;
            _device = null;
        }

        static void AddBindingOnce(InputAction action, string path)
        {
            foreach (InputBinding binding in action.bindings)
                if (binding.path == path) return;
            action.AddBinding(path);
        }

        void OnConfirmed(TextureId texture) => _logic.Confirm(texture);

        void Update()
        {
            if (pierceAction.WasPressedThisFrame()) _logic.Pierce();
            _logic.UpdateMouth(MouthDistance(), mouthAction.IsPressed());
        }

        void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled) return;  // コンポーネントが無効の間も、トリガーの知らせは届くためです
            Cloud cloud = other.GetComponentInParent<Cloud>();
            if (cloud == null || !cloud.IsAvailable || _logic.IsLoaded) return;
            if (_logic.Pierce()) cloud.Take();
        }

        // 口の位置が分からなければ、無限大を返して口の外とします。
        float MouthDistance() => _mouth != null ? Vector3.Distance(transform.position, _mouth.position) : float.PositiveInfinity;

        // 先に、トリガーの小さな球と、動かない Rigidbody を付けます。
        // トリガーの知らせを受けるには、当たる側のどちらかに Rigidbody が要るためです。
        void PrepareTip()
        {
            if (!TryGetComponent(out SphereCollider tip)) tip = gameObject.AddComponent<SphereCollider>();
            tip.isTrigger = true;
            tip.radius = tipRadius;
            if (!TryGetComponent(out Rigidbody body)) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        void ShowPiece()
        {
            if (loadedPiece != null) loadedPiece.SetActive(IsLoaded);
        }

        void Raise(Action handlers, string label)
        {
            ShowPiece();
            if (logEvents) Debug.Log("[Fork] " + label);
            handlers?.Invoke();
        }
    }
}
