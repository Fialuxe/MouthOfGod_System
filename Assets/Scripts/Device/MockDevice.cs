using System;
using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Device
{
    /// <summary>
    /// 触感デバイスが無くても動かせる <see cref="IDevice"/> です。
    /// Quest の手やコントローラで、「噛む」「決定する」を代わりにします。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 体験者のリグ（<see cref="PlayerRig"/>）の子に置き、リグの Device に入れます。
    /// 実機のデバイスができたら、リグの Device を差し替えます。
    /// </para>
    /// <para>
    /// 噛み始めに <see cref="Bitten"/> を出します。
    /// 噛んでいる間は、一定間隔で食感を切り替えて <see cref="TextureChanged"/> を出します。
    /// 決定の操作で <see cref="Confirmed"/> を出します。
    /// </para>
    /// <para>
    /// 操作のしかたは <c>Input Mode</c> で選びます。
    /// 操作の割り当ては、Inspector の InputAction で変えられます。
    /// キーボードとゲームパッドは、どのモードでも効きます。
    /// </para>
    /// </remarks>
    public sealed class MockDevice : MonoBehaviour, IDevice
    {
        [Tooltip("噛んでいる間に順に切り替わる食感 ID。既定はプレースホルダー。実際の ID に差し替える。Play 中に変えても反映されない")]
        [SerializeField] string[] textureIds = { "placeholder-a", "placeholder-b", "placeholder-c", "placeholder-d", "placeholder-e", "placeholder-f" };

        [Tooltip("食感が切り替わる間隔（秒）。暫定。Play 中に変えても反映されない")]
        [SerializeField] float switchIntervalSeconds = 1.5f;

        [Tooltip("操作のしかた。Any: 手・コントローラのどちらでも。Hand: 両手のハンドトラッキング（右手で棒を持ち、左手を握って噛み、左手のつまみで決定）。RightController: 右コントローラ 1 本（棒は右コントローラに付いたまま、右トリガーで噛み、右 A ボタンで決定）。キーボードとゲームパッドは、どのモードでも効く。Play 中に変えてもよい")]
        [SerializeField] MockInputMode inputMode = MockInputMode.Any;

        [Tooltip("噛む操作（押している間が噛んでいる間）。Inspector でバインドを変えられる。暫定: Quest 右トリガー / 左手を握る（ハンドトラッキング） / Space / ゲームパッド A")]
        [SerializeField] InputAction biteAction = new InputAction("Bite", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");

        [Tooltip("決定操作。Inspector でバインドを変えられる。暫定: Quest 右 A ボタン / 左手のつまみ（ハンドトラッキング） / Enter / ゲームパッド Start。実機の「ハードのボタン 1 つ」の代わり")]
        [SerializeField] InputAction confirmAction = new InputAction("Confirm", InputActionType.Button, "<XRController>{RightHand}/primaryButton");

        [Tooltip("噛む・食感・決定を Console に出す。受け取る側がまだ無くても、動いているか分かるようにするため")]
        [SerializeField] bool logEvents = true;

        MockDeviceLogic _logic;

        [Tooltip("デバイスの内容量（0〜100 %）。先端に表示する。Inspector で動かして試せる")]
        [SerializeField, Range(0f, 100f)] float contentPercent;

        /// <summary>デバイスの内容量（%）です。<see cref="MockDeviceModel"/> が先端に表示します。</summary>
        /// <value>0〜100 の値です。範囲の外を入れると、範囲内に丸めます。</value>
        public float ContentPercent
        {
            get => contentPercent;
            set => contentPercent = Mathf.Clamp(value, 0f, 100f);
        }

        /// <summary>操作のしかたです。<see cref="MockDeviceModel"/> も、これに合わせて棒を持ちます。</summary>
        /// <remarks>Play 中に変えてもかまいません。変えたときは、噛んでいる状態を終えます。</remarks>
        public MockInputMode InputMode
        {
            get => inputMode;
            set => inputMode = value;
        }

        /// <inheritdoc/>
        public TextureId Current => _logic != null ? _logic.Current : TextureId.None;

        /// <summary>噛む操作を押している間 <see langword="true"/> です。<see cref="MockDeviceModel"/> が表示に使います。</summary>
        public bool IsBiting => _logic != null && _logic.IsBiting;

        /// <inheritdoc/>
        public event Action<TextureId> TextureChanged;

        /// <inheritdoc/>
        public event Action<TextureId> Confirmed;

        /// <inheritdoc/>
        public event Action Bitten;

        void Reset() => AddFallbackBindings();

        // Quest が無くても、キーボードやゲームパッドで試せるようにします。
        // エディタでは AddComponent のときに Reset から呼ばれるので、同じ割り当てを二重に足しません。
        void AddFallbackBindings()
        {
            AddBindingOnce(biteAction, "<Keyboard>/space");
            AddBindingOnce(biteAction, "<Gamepad>/buttonSouth");
            AddBindingOnce(biteAction, "<HandInteraction>{LeftHand}/graspValue");  // 左手を握っている間。右手は棒を持つのに使います
            AddBindingOnce(confirmAction, "<Keyboard>/enter");
            AddBindingOnce(confirmAction, "<Gamepad>/startButton");
            AddBindingOnce(confirmAction, "<HandInteraction>{LeftHand}/pinchValue");  // 左手のつまみ
        }

        /// <summary>
        /// 同じパスの割り当てが無いときだけ、<paramref name="action"/> に割り当てを足します。
        /// </summary>
        /// <param name="action">割り当てを足す操作です。</param>
        /// <param name="path">足す割り当てのパスです。例: <c>&lt;Keyboard&gt;/space</c></param>
        internal static void AddBindingOnce(InputAction action, string path)
        {
            foreach (var binding in action.bindings)
                if (binding.path == path) return;
            action.AddBinding(path);
        }



        void Awake()
        {
            _logic = new MockDeviceLogic(textureIds, switchIntervalSeconds);
            _logic.TextureChanged += id => TextureChanged?.Invoke(id);
            _logic.Confirmed += id => Confirmed?.Invoke(id);
            _logic.Bitten += () => Bitten?.Invoke();
            if (logEvents)
            {
                _logic.Bitten += () => Debug.Log("[MockDevice] Bitten", this);
                _logic.TextureChanged += id => Debug.Log($"[MockDevice] TextureChanged: {id}", this);
                _logic.Confirmed += id => Debug.Log($"[MockDevice] Confirmed: {(id.IsEmpty ? "(噛む前)" : id.ToString())}", this);
            }
        }

        void OnEnable()
        {
            biteAction.started += OnBiteStarted;
            biteAction.canceled += OnBiteCanceled;
            confirmAction.performed += OnConfirm;
            biteAction.Enable();
            confirmAction.Enable();
        }

        void OnDisable()
        {
            biteAction.started -= OnBiteStarted;
            biteAction.canceled -= OnBiteCanceled;
            confirmAction.performed -= OnConfirm;
            biteAction.Disable();
            confirmAction.Disable();
            _logic.EndBite();  // 押したまま無効になっても、噛んでいる状態を残しません
        }

        // Any のときだけ、action の知らせで動かします。ほかのモードは Update で読みます。
        void OnBiteStarted(InputAction.CallbackContext _)
        {
            if (inputMode == MockInputMode.Any) _logic.BeginBite();
        }

        void OnBiteCanceled(InputAction.CallbackContext _)
        {
            if (inputMode == MockInputMode.Any) _logic.EndBite();
        }

        void OnConfirm(InputAction.CallbackContext _)
        {
            if (inputMode == MockInputMode.Any) _logic.Confirm();
        }

        bool _bitePressed, _confirmPressed;
        MockInputMode _lastMode;

        void Update()
        {
            // モードを変えたら、噛んだままにしません。
            if (inputMode != _lastMode)
            {
                _lastMode = inputMode;
                _bitePressed = _confirmPressed = false;
                _logic.EndBite();
            }

            // Hand と RightController のときは、モードに合う操作だけを毎フレーム読みます。
            // action の知らせ（started など）は使いません。
            // 無視する操作（手の握りなど）で action が先に始まると、
            // あとから押した操作（右トリガーなど）の知らせが来ず、噛めなくなるためです。
            if (inputMode != MockInputMode.Any)
            {
                bool bite = MockInput.AnyPressed(inputMode, biteAction);
                if (bite && !_bitePressed) _logic.BeginBite();
                else if (!bite && _bitePressed) _logic.EndBite();
                _bitePressed = bite;

                bool confirm = MockInput.AnyPressed(inputMode, confirmAction);
                if (confirm && !_confirmPressed) _logic.Confirm();
                _confirmPressed = confirm;
            }

            _logic.Tick(Time.deltaTime);
        }
    }
}
