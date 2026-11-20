using System;
using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Device
{
    /// <summary>
    /// デバイスなしで動かす IDevice。Meta Quest のコントローラ（ゲームパッド・キーボードも可）で「噛む・決定する」を代わりにする。
    /// 噛み始めで Bitten、噛んでいる間は一定間隔で TextureChanged、決定操作で Confirmed。操作の割り当ては Inspector（InputAction）で変えられる。
    /// スクリプトで作れる（GetOrCreate）。Play すると自動で用意する。
    /// 操作のしかたは <c>Input Mode</c> で選ぶ。Any は手とコントローラのどちらでも、Hand は両手のハンドトラッキングだけ、
    /// RightController は右コントローラ 1 本だけ（片手で、噛む・決定する・棒を持つ）。キーボードとゲームパッドは、どのモードでも効く。
    /// </summary>
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

        [Tooltip("噛む・食感・決定を Console に出す（受け取る側がまだ無くても、動いているか分かるように）")]
        [SerializeField] bool logEvents = true;

        MockDeviceLogic _logic;

        [Tooltip("デバイスの内容量（0〜100 %）。先端に表示する。Inspector で動かして試せる")]
        [SerializeField, Range(0f, 100f)] float contentPercent;

        /// <summary>デバイスの内容量（0〜100 %）。見た目（MockDeviceModel）が読む。</summary>
        public float ContentPercent
        {
            get => contentPercent;
            set => contentPercent = Mathf.Clamp(value, 0f, 100f);
        }

        /// <summary>操作のしかた。見た目（MockDeviceModel）も、これに合わせて棒を持つ。</summary>
        public MockInputMode InputMode
        {
            get => inputMode;
            set => inputMode = value;
        }

        public TextureId Current => _logic != null ? _logic.Current : TextureId.None;

        /// <summary>噛んでいる間（噛む操作を押している間）。見た目（MockDeviceModel）が読む。</summary>
        public bool IsBiting => _logic != null && _logic.IsBiting;

        public event Action<TextureId> TextureChanged;
        public event Action<TextureId> Confirmed;
        public event Action Bitten;

        void Reset() => AddFallbackBindings();

        // Quest 以外（キーボード・ゲームパッド）でも試せるようにする。
        // エディタでは AddComponent のときに Reset からも呼ばれるので、同じ割り当てを二重に足さない。
        void AddFallbackBindings()
        {
            AddBindingOnce(biteAction, "<Keyboard>/space");
            AddBindingOnce(biteAction, "<Gamepad>/buttonSouth");
            AddBindingOnce(biteAction, "<HandInteraction>{LeftHand}/graspValue"); // ハンドトラッキング: 左手を握っている間（右手は棒を持つのに使う）
            AddBindingOnce(confirmAction, "<Keyboard>/enter");
            AddBindingOnce(confirmAction, "<Gamepad>/startButton");
            AddBindingOnce(confirmAction, "<HandInteraction>{LeftHand}/pinchValue"); // ハンドトラッキング: 左手のつまみ
        }

        /// <summary>同じパスの割り当てが無いときだけ足す。</summary>
        internal static void AddBindingOnce(InputAction action, string path)
        {
            foreach (var binding in action.bindings)
                if (binding.path == path) return;
            action.AddBinding(path);
        }

        /// <summary>Prefab の置き場所（Resources から読む）。メニュー「MouthOfGod/Device/Create MockDevice Prefab」で作る。</summary>
        internal const string PrefabResourcePath = "Prefabs/Device/MockDevice";

        /// <summary>
        /// シーン内の MockDevice を返す。無ければ、Prefab があれば Prefab から、無ければスクリプトで作る（既定の操作割り当て付き）。
        /// Prefab にしておくと、Inspector で変えた設定（操作の割り当て・間隔・食感 ID）が残る。
        /// </summary>
        public static MockDevice GetOrCreate()
        {
            var existing = FindFirstObjectByType<MockDevice>();
            if (existing != null) return existing;

            var prefab = Resources.Load<MockDevice>(PrefabResourcePath);
            if (prefab != null)
            {
                var instance = Instantiate(prefab);
                instance.name = prefab.name;
                DontDestroyOnLoad(instance.gameObject); // 自動で作ったものは、シーンが変わっても残す（AutoCreate は最初の 1 回しか走らない）
                return instance;
            }

            var device = CreateInactive();
            device.gameObject.SetActive(true);
            DontDestroyOnLoad(device.gameObject);
            return device;
        }

        /// <summary>既定の操作割り当てを付けた MockDevice を、非アクティブで作る。Prefab を作るメニューからも使う。</summary>
        internal static MockDevice CreateInactive()
        {
            // 非アクティブで作り、バインドを足してから有効化する（先に有効化すると足したバインドが反映されない）。
            var go = new GameObject("MockDevice");
            go.SetActive(false);
            var device = go.AddComponent<MockDevice>();
            device.AddFallbackBindings();
            go.AddComponent<MockDeviceModel>().AddFallbackBindings();
            return device;
        }

        // Play したときに自動で用意する（Inspector の設定は不要。ビルドでも用意する）。
        // シーンの Awake・OnEnable の後に呼ばれるので、実機のデバイスが先に Devices に登録していれば作らない。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (Devices.Current != null) return;
            GetOrCreate();
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
            // 使う側は Devices から受け取る。別のデバイスがすでに登録されていれば、この Mock は使わない。
            if (!Devices.Register(this))
            {
                Debug.LogWarning("[MockDevice] 別のデバイスが登録済みなので、この MockDevice は使いません。", this);
                enabled = false;
                return;
            }

            biteAction.started += OnBiteStarted;
            biteAction.canceled += OnBiteCanceled;
            confirmAction.performed += OnConfirm;
            biteAction.Enable();
            confirmAction.Enable();
        }

        void OnDisable()
        {
            Devices.Unregister(this);
            biteAction.started -= OnBiteStarted;
            biteAction.canceled -= OnBiteCanceled;
            confirmAction.performed -= OnConfirm;
            biteAction.Disable();
            confirmAction.Disable();
            _logic.EndBite(); // 押したまま無効化されても噛み状態を残さない
        }

        // Any のときは、action の知らせで動かす（これまでと同じ）。
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
            // モードを変えたら、噛んだままにしない。
            if (inputMode != _lastMode)
            {
                _lastMode = inputMode;
                _bitePressed = _confirmPressed = false;
                _logic.EndBite();
            }

            // Hand・RightController のときは、モードに合う操作だけを毎フレーム読む。
            // action の知らせ（started など）を使うと、無視した操作（手の握りなど）で action が先に始まったとき、
            // あとから押した受け付ける操作（右トリガーなど）の知らせが来ず、噛めなくなるため。
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
