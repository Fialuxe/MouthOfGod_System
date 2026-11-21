using System;
using UnityEngine.InputSystem;

namespace MouthOfGod.Device
{
    /// <summary>Mock を何で操作するかを表します。キーボードとゲームパッドは、どのモードでも効きます。</summary>
    public enum MockInputMode
    {
        /// <summary>手とコントローラの、どちらの割り当ても効きます。</summary>
        Any,

        /// <summary>
        /// 両手のハンドトラッキングだけで操作します。
        /// 右手を握って棒を持ち、左手を握って噛み、左手のつまみで決定します。
        /// </summary>
        Hand,

        /// <summary>
        /// 右コントローラ 1 本だけで操作します。
        /// 棒は右コントローラに付いたままです。右トリガーで噛み、右 A ボタンで決定します。
        /// </summary>
        RightController,
    }

    /// <summary>操作がどこから来たかを表します。割り当てのパスから決めます。</summary>
    internal enum MockInputSource
    {
        /// <summary>キーボードやゲームパッドなどです。どのモードでも効きます。</summary>
        Other,

        /// <summary>ハンドトラッキングです。例: <c>&lt;HandInteraction&gt;</c></summary>
        Hand,

        /// <summary>XR コントローラです。例: <c>&lt;XRController&gt;</c>、<c>&lt;OculusTouchController&gt;</c></summary>
        Controller,
    }

    /// <summary>
    /// モードごとに、どの出どころの操作を受け付けるかを決めます。
    /// </summary>
    /// <remarks>
    /// <see cref="SourceOf"/> と <see cref="Accepts(MockInputMode, MockInputSource)"/> は、
    /// Unity の入力に依存しないので、テストで確かめられます。
    /// </remarks>
    internal static class MockInput
    {
        /// <summary>
        /// 割り当てのパスから、操作の出どころを決めます。
        /// </summary>
        /// <param name="bindingPath">
        /// 割り当てのパスです。例: <c>&lt;HandInteraction&gt;{LeftHand}/graspValue</c>
        /// </param>
        /// <returns>
        /// 出どころです。<see langword="null"/> や空なら <see cref="MockInputSource.Other"/> です。
        /// </returns>
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

        /// <summary>
        /// そのモードで、その出どころの操作を受け付けるかを返します。
        /// </summary>
        /// <param name="mode">いまの操作のしかたです。</param>
        /// <param name="source">操作の出どころです。</param>
        /// <returns>受け付けるなら <see langword="true"/> です。</returns>
        /// <remarks>
        /// Hand はコントローラを、RightController は手を受け付けません。
        /// <see cref="MockInputSource.Other"/> は、どのモードでも受け付けます。
        /// </remarks>
        internal static bool Accepts(MockInputMode mode, MockInputSource source)
        {
            switch (mode)
            {
                case MockInputMode.Hand: return source != MockInputSource.Controller;
                case MockInputMode.RightController: return source != MockInputSource.Hand;
                default: return true;
            }
        }

        /// <summary>
        /// <paramref name="action"/> に割り当てた操作のうち、受け付けるものが 1 つでも押されているかを返します。
        /// </summary>
        /// <param name="mode">いまの操作のしかたです。</param>
        /// <param name="action">調べる操作です。</param>
        /// <returns>受け付ける操作が押されていれば <see langword="true"/> です。</returns>
        /// <remarks>
        /// ボタンでない操作は、押した強さが 0.5 以上なら押したとみなします。
        /// </remarks>
        internal static bool AnyPressed(MockInputMode mode, InputAction action)
        {
            foreach (InputControl control in action.controls)
            {
                if (!Accepts(mode, action, control)) continue;
                if (control is UnityEngine.InputSystem.Controls.ButtonControl button ? button.isPressed
                    : control is UnityEngine.InputSystem.Controls.AxisControl axis && axis.ReadValue() >= 0.5f)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// そのモードで、<paramref name="action"/> の 1 つの操作を受け付けるかを返します。
        /// </summary>
        /// <param name="mode">いまの操作のしかたです。</param>
        /// <param name="action"><paramref name="control"/> を含む操作です。</param>
        /// <param name="control">調べる操作です。<see langword="null"/> なら受け付けます。</param>
        /// <returns>受け付けるなら <see langword="true"/> です。Any のときは、いつも <see langword="true"/> です。</returns>
        internal static bool Accepts(MockInputMode mode, InputAction action, InputControl control)
        {
            if (mode == MockInputMode.Any || control == null) return true;
            InputBinding? binding = action.GetBindingForControl(control);
            return Accepts(mode, SourceOf(binding.HasValue ? binding.Value.effectivePath : null));
        }
    }
}
