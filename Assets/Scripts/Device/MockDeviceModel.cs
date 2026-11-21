using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Device
{
    /// <summary>
    /// <see cref="MockDevice"/> の仮の見た目です。細長い棒で、先端に食感と内容量（%）を出します。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 右手を握ると、棒が手に付きます。離すと、その場に残ります。
    /// Hand のときは手の握りだけで持ちます。
    /// Any のときは、手の握り・右グリップ・G キーのどれでも持ちます。
    /// </para>
    /// <para>
    /// RightController のときは、握らなくても右コントローラに付いたままにします。
    /// 片手で、噛む・決定する・棒を持つをすべて行うためです。
    /// </para>
    /// <para>
    /// 見た目だけを受け持ち、<see cref="MouthOfGod.Core.IDevice"/> のイベントは出しません。
    /// </para>
    /// </remarks>
    [RequireComponent(typeof(MockDevice))]
    public sealed class MockDeviceModel : MonoBehaviour
    {
        [Tooltip("手の位置。空なら、親の OVRCameraRig の RightHandAnchor。PC で試すリグでは手が無いので、持たない")]
        [SerializeField] Transform handAnchor;

        [Tooltip("握りの強さ（0〜1）。この値以上で握ったことにする。暫定")]
        [SerializeField, Range(0f, 1f)] float graspThreshold = 0.8f;

        [Tooltip("握る操作（float）。Inspector でバインドを変えられる。暫定: OpenXR Hand Interaction の右手 graspValue / Quest 右グリップ")]
        [SerializeField] InputAction graspAction = new InputAction("Grasp", InputActionType.Value, "<HandInteraction>{RightHand}/graspValue");

        [Tooltip("棒の長さ（m）。Play 時に見た目を作るときだけ使う。Label が入っている Prefab では効かない")]
        [SerializeField] float length = 0.2f;

        [Tooltip("先端の内容量（%）の表示。Prefab に入っていればそれを使う。空なら Play 時に見た目を作る")]
        [SerializeField] TextMesh label;

        MockDevice _device;
        OVRCameraRig _rig;
        bool _holding;
        string _confirmedText;
        float _confirmedUntil;

        void Reset() => AddFallbackBindings();

        // 手が使えない環境でも確かめられるようにします。
        // エディタでは AddComponent のときに Reset から呼ばれるので、同じ割り当てを二重に足しません。
        void AddFallbackBindings()
        {
            MockDevice.AddBindingOnce(graspAction, "<XRController>{RightHand}/grip");
            MockDevice.AddBindingOnce(graspAction, "<Keyboard>/g");
        }

        void Awake()
        {
            _device = GetComponent<MockDevice>();
            // 手のアンカーはリグの Awake で決まるので、ここではリグだけを覚え、使うときに読みます。
            _rig = GetComponentInParent<OVRCameraRig>(true);
            if (label == null) BuildModel();
        }

        void OnEnable()
        {
            graspAction.Enable();
            _device.Confirmed += OnConfirmed;
        }

        void OnDisable()
        {
            graspAction.Disable();
            _device.Confirmed -= OnConfirmed;
        }

        // 決定したら、1 秒だけ先端に出します。
        void OnConfirmed(MouthOfGod.Core.TextureId texture)
        {
            _confirmedText = "OK: " + (texture.IsEmpty ? "-" : texture.ToString());
            _confirmedUntil = Time.time + 1f;
        }

        // 握りの強さを返します。モードに合う出どころの操作だけを見ます。
        // たとえば Hand のときは、手とキーボードだけを見ます。
        float GraspValue()
        {
            if (_device.InputMode == MockInputMode.Any) return graspAction.ReadValue<float>();
            float best = 0f;
            foreach (InputControl control in graspAction.controls)
            {
                if (!MockInput.Accepts(_device.InputMode, graspAction, control)) continue;
                if (control is UnityEngine.InputSystem.Controls.AxisControl axis) best = Mathf.Max(best, axis.ReadValue());
            }
            return best;
        }

        /// <summary>
        /// 棒と先端の表示を、子として作ります。<c>Label</c> が空のときに Awake から呼びます。
        /// </summary>
        /// <remarks>
        /// Play 中でなくても呼べます。そのときは DestroyImmediate を使います。
        /// 呼ぶたびに子を足すので、2 回呼ぶと見た目が重なります。
        /// </remarks>
        internal void BuildModel()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "MockDeviceBody";
            var collider = body.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
            body.transform.SetParent(transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);  // 前（z）向きに寝かせます
            body.transform.localScale = new Vector3(0.02f, length * 0.5f, 0.02f);
            body.transform.localPosition = new Vector3(0f, 0f, length * 0.5f);

            var labelGo = new GameObject("MockDeviceLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.03f, length);
            label = labelGo.AddComponent<TextMesh>();
            // フォントを指定しないと、TextMesh は何も描きません。
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.font = font;
            labelGo.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            label.anchor = TextAnchor.LowerCenter;
            label.characterSize = 0.01f;
            label.fontSize = 48;
            label.text = "0%";
        }

        void Update()
        {
            // MockDevice が無効なら、棒を出しません。実機に差し替えて Mock を切ったときなどです。
            if (!_device.enabled)
            {
                gameObject.SetActive(false);
                return;
            }

            Transform hand = handAnchor != null ? handAnchor : _rig != null ? _rig.rightHandAnchor : null;
            _holding = hand != null && (_device.InputMode == MockInputMode.RightController || GraspValue() >= graspThreshold);
            if (_holding) transform.SetPositionAndRotation(hand.position, hand.rotation);

            // 先端の 1 行目は、食感か決定した結果です。噛んでいる間は * を付けます。
            // 2 行目は内容量です。
            var current = _device.Current.IsEmpty ? "-" : _device.Current.ToString();
            var top = Time.time < _confirmedUntil ? _confirmedText : (_device.IsBiting ? "* " + current : current);
            label.text = $"{top}\n{Mathf.RoundToInt(_device.ContentPercent)}%";
            var cam = Camera.main;
            if (cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
        }
    }
}
