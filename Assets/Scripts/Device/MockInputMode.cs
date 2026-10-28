using System;
using UnityEngine.InputSystem;

namespace MouthOfGod.Device
{
    /// <summary>Mock を何で操作するか。キーボードとゲームパッドは、どのモードでも効く。</summary>
    public enum MockInputMode
    {
        Any, // 手・コントローラのどちらでも（両方の割り当てが効く）
        Hand, // ハンドトラッキングの両手: 右手を握って棒を持つ、左手を握って噛む、左手のつまみで決定
        RightController, // 右コントローラ 1 本: 棒は右コントローラに付いたまま、右トリガーで噛む、右 A ボタンで決定
    }

    /// <summary>操作がどこから来たか。割り当て（binding）の道筋で決める。</summary>
    internal enum MockInputSource
    {
        Other, // キーボード・ゲームパッドなど。どのモードでも効く
        Hand, // ハンドトラッキング（<HandInteraction> など）
        Controller, // XR コントローラ（<XRController>・<OculusTouchController> など）
    }

    /// <summary>モードと、操作の出どころの決まり。Unity の入力に依存しない部分だけ（テストできる）。</summary>
    internal static class MockInput
    {
        /// <summary>割り当ての道筋（例: <c>&lt;HandInteraction&gt;{LeftHand}/graspValue</c>）から、出どころを決める。</summary>
        internal static MockInputSource SourceOf(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath)) return MockInputSource.Other;
            if (bindingPath.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0 && bindingPath.IndexOf("Interaction", StringComparison.OrdinalIgnoreCase) >= 0)
                return MockInputSource.Hand;
            if (bindingPath.StartsWith("<XRController>", StringComparison.OrdinalIgnoreCase)
                || bindingPath.IndexOf("Controller>", StringComparison.OrdinalIgnoreCase) >= 0 && bindingPath.IndexOf("Hand}", StringComparison.OrdinalIgnoreCase) >= 0)
                return MockInputSource.Controller;
            return MockInputSource.Other;
        }

        /// <summary>そのモードで、その出どころの操作を受け付けるか。</summary>
        internal static bool Accepts(MockInputMode mode, MockInputSource source)
        {
            switch (mode)
            {
                case MockInputMode.Hand: return source != MockInputSource.Controller;
                case MockInputMode.RightController: return source != MockInputSource.Hand;
                default: return true;
            }
        }

        /// <summary>そのモードで、この操作（action の control）を受け付けるか。</summary>
        internal static bool Accepts(MockInputMode mode, InputAction action, InputControl control)
        {
            if (mode == MockInputMode.Any || control == null) return true;
            InputBinding? binding = action.GetBindingForControl(control);
            return Accepts(mode, SourceOf(binding.HasValue ? binding.Value.effectivePath : null));
        }
    }
}
