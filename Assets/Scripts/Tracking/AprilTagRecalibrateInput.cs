using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// アプリ内の入力で、固定モードの <see cref="AprilTagPlace"/> をすべて取り直します。
    /// </summary>
    /// <remarks>
    /// <para>
    /// スタッフは、Quest の**左**コントローラーの Y ボタンを長押しします。
    /// 開発中は、キーボードの R キーでも取り直せます。
    /// AprilTag Detection のプレハブに付けてあります。
    /// </para>
    /// <para>
    /// 右の A ボタンは、体験者の「決定」に使います（MockDevice）。
    /// 左の X 長押しは、スタッフの「飛ばす」に使います（シナリオブック）。
    /// ぶつからないよう、左の Y の長押しにしました。
    /// </para>
    /// </remarks>
    public sealed class AprilTagRecalibrateInput : MonoBehaviour
    {
        [Tooltip("左の Y ボタンを、この秒数（秒）押し続けると取り直す。")]
        [SerializeField, Min(0.1f)] float holdSeconds = 2f;

        float _heldFor;
        bool _firedThisHold;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) AprilTagPlace.RecalibrateAll();

            if (!OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.LTouch))
            {
                _heldFor = 0f;
                _firedThisHold = false;
                return;
            }

            _heldFor += Time.unscaledDeltaTime;
            if (_firedThisHold || _heldFor < holdSeconds) return;
            // 押し続けても、1 回の長押しで取り直すのは 1 回だけです。
            _firedThisHold = true;
            AprilTagPlace.RecalibrateAll();
        }
    }
}
