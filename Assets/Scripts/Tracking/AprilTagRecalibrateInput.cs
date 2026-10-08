using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// タグの位置の取り直しを、アプリ内の入力で行う。Quest のコントローラーの A / X ボタン、またはキーボードの R キー。
    /// <see cref="AprilTagDetectionService"/> が自動で付ける。固定モードの <see cref="AprilTagPlace"/> すべてを取り直す。
    /// </summary>
    public sealed class AprilTagRecalibrateInput : MonoBehaviour
    {
        void Update()
        {
            var keyboard = Keyboard.current;
            var keyPressed = keyboard != null && keyboard.rKey.wasPressedThisFrame;
            var buttonPressed = OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.Touch);

            if (keyPressed || buttonPressed) AprilTagPlace.RecalibrateAll();
        }
    }
}
