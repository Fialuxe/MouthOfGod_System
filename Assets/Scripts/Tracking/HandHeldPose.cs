using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 手（ハンドトラッキング）の位置に、オフセットを足して、手に持ったものの先の位置に自分を動かす。
    /// フォークなら、<c>ForkTip</c> のオブジェクトか、その親に付ける。<c>ForkTip</c> は位置の取り方を知らないので、これを付けるだけでつながる。
    /// 手は、親の OVRCameraRig の手のアンカー（<c>RightHandAnchor</c>）から読む。リグの外や、PC で試すリグでは何もしない。コントローラで動かしても同じ動きになるが、握る位置が違うので、オフセットは手で測り直す。
    /// </summary>
    /// <remarks>
    /// - オフセットの測り直し: 先を口に当てて、C キー（開発中）。口の位置は、親のリグの口（<see cref="PlayerRig.Mouth"/>）。
    ///   Quest の手で押せる入力は、まだ決めていない。
    /// - 手の追跡が途切れたら、アンカーが止まるので、最後の位置に残る。
    /// </remarks>
    public sealed class HandHeldPose : MonoBehaviour
    {
        [Tooltip("手の座標系での、先の位置（m）。手の前（+Z）に 15 cm。仮の値。口に当てて C キーで測り直せる")]
        [SerializeField] Vector3 tipOffset = new Vector3(0f, 0f, 0.15f);

        [Tooltip("手のぶれをならす時定数（秒）。大きいほど滑らかだが遅れる。0 でならさない。仮の値")]
        [SerializeField, Min(0f)] float smoothingSeconds = 0.05f;

        [Tooltip("true なら右手、false なら左手")]
        [SerializeField] bool useRightHand = true;

        [Tooltip("先を口に当てて、オフセットを測り直す。開発中の入力")]
        [SerializeField] InputAction calibrateAction = new InputAction("CalibrateTip", InputActionType.Button, "<Keyboard>/c");

        [Tooltip("測り直した結果を Console に出す")]
        [SerializeField] bool logEvents = true;

        HandHeldTip _tip;
        OVRCameraRig _rig;
        Transform _mouth;

        /// <summary>手の座標系での、いまの先の位置（m）。</summary>
        public Vector3 TipOffset => _tip != null ? _tip.LocalOffset : tipOffset;

        void Awake()
        {
            _tip = new HandHeldTip(tipOffset, smoothingSeconds);
            _rig = GetComponentInParent<OVRCameraRig>(true);
            PlayerRig player = PlayerRig.Of(this);
            _mouth = player != null ? player.Mouth : null;
        }

        void OnEnable() => calibrateAction.Enable();

        void OnDisable() => calibrateAction.Disable();

        void LateUpdate()
        {
            if (!TryGetHand(out var hand)) return;

            if (calibrateAction.WasPressedThisFrame() && _mouth != null)
            {
                _tip.Calibrate(hand, _mouth.position);
                if (logEvents) Debug.Log($"[HandHeldPose] オフセットを測り直した: {_tip.LocalOffset}", this);
            }

            var pose = _tip.Update(hand, Time.deltaTime);
            transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        // 手のアンカーの姿勢。リグの外や、アンカーがまだ無いときは false。
        bool TryGetHand(out Pose hand)
        {
            hand = default;
            if (_rig == null) return false;
            var anchor = useRightHand ? _rig.rightHandAnchor : _rig.leftHandAnchor;
            if (anchor == null) return false;
            hand = new Pose(anchor.position, anchor.rotation);
            return true;
        }
    }
}
