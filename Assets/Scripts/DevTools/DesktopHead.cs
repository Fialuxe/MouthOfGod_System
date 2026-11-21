using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace MouthOfGod.DevTools
{
    /// <summary>
    /// Quest なしで試すための頭です。Main Camera に付けると、キーボードとマウスで歩いて見回せます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// WASD で歩きます。Shift を押している間は速く歩きます。
    /// マウスの右ボタンを押しながら動かすと、向きが変わります。
    /// </para>
    /// <para>
    /// 高さは変えません。立っている場所（StandArea）は、頭の水平の位置で決まるためです。
    /// Quest の頭が動いているときは、何もしません。頭の位置は XR が決めます。
    /// 展示では使いません。
    /// </para>
    /// </remarks>
    public sealed class DesktopHead : MonoBehaviour
    {
        [Tooltip("歩く速さです。単位は m / 秒です。1.2 で、人がふつうに歩く速さくらいです。")]
        [SerializeField, Min(0f)] float walkSpeed = 1.2f;

        [Tooltip("Shift を押している間、歩く速さを何倍にするかです。")]
        [SerializeField, Min(1f)] float runMultiplier = 3f;

        [Tooltip("マウスを 1 ピクセル動かしたときに回る角度です。単位は度です。")]
        [SerializeField, Min(0f)] float degreesPerPixel = 0.15f;

        [Tooltip("上下を向ける角度の上限です。単位は度です。90 にすると真上・真下で向きが乱れるので、89 までにしています。")]
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

        /// <summary>頭を指定した場所へ移し、指定した向きにします。</summary>
        /// <param name="position">移す先です。水平の位置だけを使い、高さは今のままにします。</param>
        /// <param name="yaw">向く方角です。単位は度です。上下の向きは水平に戻します。</param>
        public void Teleport(Vector3 position, float yaw)
        {
            transform.position = new Vector3(position.x, transform.position.y, position.z);
            _yaw = yaw;
            _pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        /// <summary>移動の入力を、頭の向きに合わせた水平の向きに直します。</summary>
        /// <param name="yaw">頭が向いている方角です。単位は度です。</param>
        /// <param name="input">移動の入力です。x が右、y が前です。</param>
        /// <returns>
        /// ワールドの水平の向きです。y は常に 0 です。
        /// 長さは 1 以下にします。斜めに歩いても速くならないようにするためです。
        /// </returns>
        internal static Vector3 Step(float yaw, Vector2 input)
        {
            Vector3 move = Quaternion.Euler(0f, yaw, 0f) * new Vector3(input.x, 0f, input.y);
            return move.sqrMagnitude > 1f ? move.normalized : move;
        }
    }
}
