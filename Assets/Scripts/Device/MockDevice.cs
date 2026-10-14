using System;
using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Device
{
    /// <summary>
    /// デバイスなしで動かす IDevice。コントローラ（ゲームパッド）またはキーボードで「噛む・決定する」を代わりにする。
    /// 噛み始め（A ボタン / Space を押した）で Bitten、噛んでいる間は一定間隔で TextureChanged、Start / Enter で Confirmed。
    /// 自動では生成しない。シーンに置くのは Bootstrap（#34）か手動。
    /// </summary>
    public sealed class MockDevice : MonoBehaviour, IDevice
    {
        [Tooltip("噛んでいる間に順に切り替わる食感 ID。既定はプレースホルダー。実際の ID に差し替える")]
        [SerializeField] string[] textureIds = { "placeholder-a", "placeholder-b", "placeholder-c" };

        [Tooltip("食感が切り替わる間隔（秒）。暫定")]
        [SerializeField] float switchIntervalSeconds = 1.5f;

        [Tooltip("噛むキー（押している間が噛んでいる間）。暫定")]
        [SerializeField] Key biteKey = Key.Space;

        [Tooltip("決定キー。暫定")]
        [SerializeField] Key confirmKey = Key.Enter;

        MockDeviceLogic _logic;

        public event Action<TextureId> TextureChanged;
        public event Action Confirmed;
        public event Action Bitten;

        void Awake()
        {
            _logic = new MockDeviceLogic(textureIds, switchIntervalSeconds);
            _logic.TextureChanged += id => TextureChanged?.Invoke(id);
            _logic.Confirmed += () => Confirmed?.Invoke();
            _logic.Bitten += () => Bitten?.Invoke();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var pad = Gamepad.current;
            if (keyboard == null && pad == null) return;

            _logic.Tick(Time.deltaTime);

            var bite = keyboard?[biteKey];
            bool pressed = (bite?.wasPressedThisFrame ?? false) || (pad?.buttonSouth.wasPressedThisFrame ?? false);
            bool released = (bite?.wasReleasedThisFrame ?? false) || (pad?.buttonSouth.wasReleasedThisFrame ?? false);
            if (pressed) _logic.BeginBite();
            if (released) _logic.EndBite();

            if ((keyboard?[confirmKey].wasPressedThisFrame ?? false) || (pad?.startButton.wasPressedThisFrame ?? false))
                _logic.Confirm();
        }
    }
}
