using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Tracking
{
    /// <summary>
    /// 手に持ったものの先の位置に、自分を動かします。
    /// 手の位置にオフセットを足して、先の位置を求めます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// フォークなら、<c>ForkTip</c> のオブジェクトか、その親に付けます。
    /// <c>ForkTip</c> は位置の取り方を知らないので、これを付けるだけでつながります。
    /// </para>
    /// <para>
    /// 手は、親の OVRCameraRig の手のアンカーから読みます。
    /// 右手なら <c>RightHandAnchor</c> です。
    /// リグの外や、PC で試すリグでは何もしません。
    /// </para>
    /// <para>
    /// コントローラーで動かしても、同じ動きになります。
    /// ただし握る位置が違うので、オフセットは手で測り直します。
    /// </para>
    /// <para>
    /// オフセットは、先を口に当てて C キーで測り直します（開発中の入力）。
    /// 口の位置は、親のリグの <see cref="PlayerRig.Mouth"/> を使います。
    /// Quest の手で押せる入力は、まだ決めていません。
    /// </para>
    /// <para>
    /// 手の追跡が途切れると、アンカーが止まります。
    /// そのため、先は最後の位置に残ります。
    /// </para>
    /// </remarks>
    public sealed class HandHeldPose : MonoBehaviour
    {
        [Tooltip("手の座標系での、先の位置（m）。手の前（+Z）に 15 cm。仮の値。口に当てて C キーで測り直せる")]
        [SerializeField] Vector3 tipOffset = new Vector3(0f, 0f, 0.15f);

        [Tooltip("手のぶれをならす時定数（秒）。大きいほど滑らかだが遅れる。0 でならさない。仮の値")]
        [SerializeField, Min(0f)] float smoothingSeconds = 0.05f;

        [Tooltip("true なら右手、false なら左手")]
        [SerializeField] bool useRightHand = true;

        [Tooltip("先を口に当てて、オフセットを測り直す。開発中の入力（既定は C キー）。親のリグに口が無いときは効かない")]
        [SerializeField] InputAction calibrateAction = new InputAction("CalibrateTip", InputActionType.Button, "<Keyboard>/c");

        [Tooltip("測り直した結果を Console に出す")]
        [SerializeField] bool logEvents = true;

        HandHeldTip _tip;
        OVRCameraRig _rig;
        Transform _mouth;

        /// <summary>
        /// 手の座標系での、いまの先の位置（m）を返します。
        /// </summary>
        /// <remarks>
        /// 測り直した後は、その値を返します。
        /// Awake の前は、Inspector の値を返します。
        /// </remarks>
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

        // 手のアンカーの姿勢を返します。リグの外や、アンカーがまだ無いときは false を返します。
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
