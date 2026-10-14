using System;
using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Device
{
    /// <summary>
    /// デバイスなしで動かす IDevice。Meta Quest のコントローラ（ゲームパッド・キーボードも可）で「噛む・決定する」を代わりにする。
    /// 噛み始めで Bitten、噛んでいる間は一定間隔で TextureChanged、決定操作で Confirmed。操作の割り当ては Inspector（InputAction）で変えられる。
    /// 自動では生成しない。シーンに置くのは Bootstrap（#34）か手動。
    /// </summary>
    public sealed class MockDevice : MonoBehaviour, IDevice
    {
        [Tooltip("噛んでいる間に順に切り替わる食感 ID。既定はプレースホルダー。実際の ID に差し替える")]
        [SerializeField] string[] textureIds = { "placeholder-a", "placeholder-b", "placeholder-c" };

        [Tooltip("食感が切り替わる間隔（秒）。暫定")]
        [SerializeField] float switchIntervalSeconds = 1.5f;

        [Tooltip("噛む操作（押している間が噛んでいる間）。Inspector でバインドを変えられる。暫定: Quest 右トリガー / Space / ゲームパッド A")]
        [SerializeField] InputAction biteAction = new InputAction("Bite", InputActionType.Button, "<XRController>{RightHand}/triggerPressed");

        [Tooltip("決定操作。Inspector でバインドを変えられる。暫定: Quest 右 A ボタン / Enter / ゲームパッド Start")]
        [SerializeField] InputAction confirmAction = new InputAction("Confirm", InputActionType.Button, "<XRController>{RightHand}/primaryButton");

        MockDeviceLogic _logic;

        public event Action<TextureId> TextureChanged;
        public event Action Confirmed;
        public event Action Bitten;

        void Reset()
        {
            // 新規にアタッチしたときの既定バインド（Quest 以外でも試せるようにキーボード・ゲームパッドも足す）。
            biteAction.AddBinding("<Keyboard>/space");
            biteAction.AddBinding("<Gamepad>/buttonSouth");
            confirmAction.AddBinding("<Keyboard>/enter");
            confirmAction.AddBinding("<Gamepad>/startButton");
        }

        void Awake()
        {
            _logic = new MockDeviceLogic(textureIds, switchIntervalSeconds);
            _logic.TextureChanged += id => TextureChanged?.Invoke(id);
            _logic.Confirmed += () => Confirmed?.Invoke();
            _logic.Bitten += () => Bitten?.Invoke();
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
            _logic.EndBite(); // 押したまま無効化されても噛み状態を残さない
        }

        void OnBiteStarted(InputAction.CallbackContext _) => _logic.BeginBite();
        void OnBiteCanceled(InputAction.CallbackContext _) => _logic.EndBite();
        void OnConfirm(InputAction.CallbackContext _) => _logic.Confirm();

        void Update() => _logic.Tick(Time.deltaTime);
    }
}
