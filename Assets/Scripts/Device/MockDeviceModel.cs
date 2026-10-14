using UnityEngine;
using UnityEngine.InputSystem;

namespace MouthOfGod.Device
{
    /// <summary>
    /// MockDevice の雑な見た目。細長い棒で、先端に内容量（%）を出す。
    /// 手（Quest のハンドトラッキング）を握ると、手に付いて保持される。離すとその場に残る。
    /// 見た目だけで、IDevice のイベントには関わらない。
    /// </summary>
    [RequireComponent(typeof(MockDevice))]
    public sealed class MockDeviceModel : MonoBehaviour
    {
        [Tooltip("手の位置。空なら、シーン内の RightHandAnchor（OVRCameraRig）を探す")]
        [SerializeField] Transform handAnchor;

        [Tooltip("握りの強さ（0〜1）。この値以上で握ったことにする。暫定")]
        [SerializeField, Range(0f, 1f)] float graspThreshold = 0.8f;

        [Tooltip("握る操作（float）。Inspector でバインドを変えられる。暫定: OpenXR Hand Interaction の右手 graspValue / Quest 右グリップ")]
        [SerializeField] InputAction graspAction = new InputAction("Grasp", InputActionType.Value, "<HandInteraction>{RightHand}/graspValue");

        [Tooltip("棒の長さ（m）。見た目を作るときに使う")]
        [SerializeField] float length = 0.2f;

        [Tooltip("先端の内容量（%）の表示。Prefab に入っていればそれを使う。空なら Play 時に見た目を作る")]
        [SerializeField] TextMesh label;

        MockDevice _device;
        bool _holding;
        float _nextHandSearch;
        string _confirmedText;
        float _confirmedUntil;

        void Reset() => AddFallbackBindings();

        // 手が使えない環境でも確認できるようにする。
        internal void AddFallbackBindings()
        {
            // エディタでは AddComponent のときに Reset からも呼ばれるので、同じ割り当てを二重に足さない。
            MockDevice.AddBindingOnce(graspAction, "<XRController>{RightHand}/grip");
            MockDevice.AddBindingOnce(graspAction, "<Keyboard>/g"); // 手が使えないときの確認用
        }

        void Awake()
        {
            _device = GetComponent<MockDevice>();
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

        // 決定したら、1 秒だけ先端に出す。
        void OnConfirmed(MouthOfGod.Core.TextureId texture)
        {
            _confirmedText = "OK: " + (texture.IsEmpty ? "-" : texture.ToString());
            _confirmedUntil = Time.time + 1f;
        }

        /// <summary>棒と先端の表示を、子として作る。Prefab を作るメニュー（エディタ）からも呼ぶ。</summary>
        internal void BuildModel()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "MockDeviceBody";
            var collider = body.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
            body.transform.SetParent(transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // 前（z）向きに寝かせる
            body.transform.localScale = new Vector3(0.02f, length * 0.5f, 0.02f);
            body.transform.localPosition = new Vector3(0f, 0f, length * 0.5f);

            var labelGo = new GameObject("MockDeviceLabel");
            labelGo.transform.SetParent(transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.03f, length);
            label = labelGo.AddComponent<TextMesh>();
            // フォントを指定しないと、TextMesh は何も描かない。
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
            // 別のデバイスが登録されて、この Mock が使われていないなら、棒を出さない。
            if (!_device.enabled)
            {
                gameObject.SetActive(false);
                return;
            }

            // 手が見つかるまで、1 秒おきに探す（毎フレーム探さない）。
            if (handAnchor == null && Time.time >= _nextHandSearch)
            {
                _nextHandSearch = Time.time + 1f;
                var found = GameObject.Find("RightHandAnchor");
                if (found != null) handAnchor = found.transform;
            }

            _holding = handAnchor != null && graspAction.ReadValue<float>() >= graspThreshold;
            if (_holding) transform.SetPositionAndRotation(handAnchor.position, handAnchor.rotation);

            // 先端: 1 行目に食感（噛んでいる間は *）か決定、2 行目に内容量。
            var current = _device.Current.IsEmpty ? "-" : _device.Current.ToString();
            var top = Time.time < _confirmedUntil ? _confirmedText : (_device.IsBiting ? "* " + current : current);
            label.text = $"{top}\n{Mathf.RoundToInt(_device.ContentPercent)}%";
            var cam = Camera.main;
            if (cam != null) label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position);
        }
    }
}
