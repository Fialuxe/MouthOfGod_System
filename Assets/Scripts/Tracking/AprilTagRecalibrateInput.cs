using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// タグの位置の取り直しを、アプリ内の入力で行う。Quest の**左**コントローラーの Y ボタンを長押し（スタッフの操作）、またはキーボードの R キー（開発中）。
    /// <see cref="AprilTagDetectionService"/> が自動で付ける。固定モードの <see cref="AprilTagPlace"/> すべてを取り直す。
    /// </summary>
    /// <remarks>
    /// 右手の A ボタンは体験者の「決定」（MockDevice）、左の X 長押しはスタッフの「飛ばす」（シナリオブック）に使うので、ぶつからない左の Y の長押しにした。
    /// </remarks>
    public sealed class AprilTagRecalibrateInput : MonoBehaviour
    {
        [Tooltip("左の Y ボタンを、この秒数押し続けると取り直す")]
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
            _firedThisHold = true; // 押し続けても 1 回だけ
            AprilTagPlace.RecalibrateAll();
        }
    }
}
