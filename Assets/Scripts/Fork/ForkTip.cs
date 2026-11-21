using System;
using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Fork
{
    /// <summary>
    /// 体験者が持つフォークの先。雲（<see cref="Cloud"/>）に触れたら刺したとし、口に近づいたら口に入ったとする。
    /// フォークの先の位置に置く。位置の取り方は知らない。手（ハンドトラッキング）や AprilTag からフォークの位置を推定する部品が、このオブジェクトか親を動かす。
    /// 体験者のリグ（<see cref="PlayerRig"/>）の子に置く。デバイスと口は、親のリグから受け取る。
    /// </summary>
    /// <remarks>
    /// - 刺す: 先の小さな当たり判定（トリガー）が、<see cref="Cloud"/> の当たり判定に入ったとき。
    /// - 口: 先と口の位置の距離で決める。口の位置は、親のリグの口（<see cref="PlayerRig.Mouth"/>）。
    /// - かけらは、デバイスで決定したときになくなる（食べ物が生まれるのと同じとき）。
    /// - Quest がなくても試せるように、キーで「刺す」「口に入れる（押している間）」を代わりにできる。展示では使わない。
    /// </remarks>
    public sealed class ForkTip : MonoBehaviour, IFork
    {
        [Tooltip("先と口の距離がこれ以下になったら、口に入ったとする（m）。仮の値")]
        [SerializeField, Min(0f)] float mouthEnterRadius = 0.06f;

        [Tooltip("口に入ったあと、これより離れたら出たとする（m）。境目で入る・出るをくり返さないよう、Enter より少し大きくする。仮の値")]
        [SerializeField, Min(0f)] float mouthExitRadius = 0.09f;

        [Tooltip("先の当たり判定の半径（m）。雲に触れたかを見る")]
        [SerializeField, Min(0.001f)] float tipRadius = 0.02f;

        [Tooltip("刺した雲のかけらの見た目。刺している間だけ表示する。空なら何も出さない")]
        [SerializeField] GameObject loadedPiece;

        [Tooltip("Quest なしで試すときの「刺す」。展示では使わない")]
        [SerializeField] InputAction pierceAction = new InputAction("Pierce", InputActionType.Button, "<Keyboard>/p");

        [Tooltip("Quest なしで試すときの「口に入れる」（押している間）。M キーのほか、ハンドトラッキングで右手を握っている間も、口の中にあることにする。展示では使わない")]
        [SerializeField] InputAction mouthAction = new InputAction("Mouth", InputActionType.Button, "<Keyboard>/m");

        [Tooltip("刺す・口・食べたを Console に出す")]
        [SerializeField] bool logEvents = true;

        // ハンドトラッキングで、右手を握っている間（モック用）。
        const string MockMouthHandBinding = "<HandInteraction>{RightHand}/graspValue";

        ForkLogic _logic;
        IDevice _device;
        Transform _mouth;

        public bool IsLoaded => _logic != null && _logic.IsLoaded;
        public bool InMouth => _logic != null && _logic.InMouth;

        public event Action Pierced;
        public event Action MouthEntered;
        public event Action MouthLeft;
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
            if (!isActiveAndEnabled) return; // 無効の間も、トリガーの知らせは届くため
            Cloud cloud = other.GetComponentInParent<Cloud>();
            if (cloud == null || !cloud.IsAvailable || _logic.IsLoaded) return;
            if (_logic.Pierce()) cloud.Take();
        }

        // 先と口の距離。口の位置が分からなければ、無限大（口の外）。
        float MouthDistance() => _mouth != null ? Vector3.Distance(transform.position, _mouth.position) : float.PositiveInfinity;

        // 先に、トリガーの小さな球と、動かない Rigidbody を付ける（トリガーの知らせを受けるのに、どちらかに Rigidbody が要る）。
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
