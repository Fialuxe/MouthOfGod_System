using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace MouthOfGod.DevTools
{
    /// <summary>
    /// Quest なしで試すための頭。Main Camera に付けると、キーボードとマウスで歩いて見回せる。
    /// </summary>
    /// <remarks>
    /// - WASD で歩く。Shift を押している間は速く歩く。マウスの右ボタンを押しながら動かすと、向きが変わる。
    /// - 高さは変えない。立っている場所（StandArea）は、頭の水平の位置で決まるため。
    /// - Quest の頭が動いているときは、何もしない（頭の位置は XR が決める）。
    /// - 展示では使わない。
    /// </remarks>
    public sealed class DesktopHead : MonoBehaviour
    {
        [Tooltip("歩く速さ（m / 秒）")]
        [SerializeField, Min(0f)] float walkSpeed = 1.2f;

        [Tooltip("Shift を押している間、歩く速さを何倍にするか")]
        [SerializeField, Min(1f)] float runMultiplier = 3f;

        [Tooltip("マウスを 1 ピクセル動かしたときに回る角度（度）")]
        [SerializeField, Min(0f)] float degreesPerPixel = 0.15f;

        [Tooltip("上下を向ける角度の上限（度）")]
        [SerializeField, Range(0f, 89f)] float maxPitch = 80f;

        float _yaw;
        float _pitch;

        void Start()
        {
            Vector3 euler = transform.eulerAngles;
            _yaw = euler.y;
            _pitch = Mathf.DeltaAngle(0f, euler.x);
        }

        void Update()
        {
            if (XRSettings.isDeviceActive) return;

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _yaw += delta.x * degreesPerPixel;
                _pitch = Mathf.Clamp(_pitch - delta.y * degreesPerPixel, -maxPitch, maxPitch);
            }
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (input == Vector2.zero) return;

            float speed = walkSpeed * (keyboard.leftShiftKey.isPressed ? runMultiplier : 1f);
            transform.position += Step(_yaw, input) * speed * Time.deltaTime;
        }

        /// <summary>水平の位置だけを移す。高さはそのまま。向きは yaw（度）にする。</summary>
        public void Teleport(Vector3 position, float yaw)
        {
            transform.position = new Vector3(position.x, transform.position.y, position.z);
            _yaw = yaw;
            _pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        /// <summary>入力（x: 右、y: 前）を、yaw（度）の向きの水平の向きに直す。長さは 1 以下。</summary>
        internal static Vector3 Step(float yaw, Vector2 input)
        {
            Vector3 move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
            return move.sqrMagnitude > 1f ? move.normalized : move;
        }
    }
}
