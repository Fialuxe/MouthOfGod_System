using System;
using System.Collections.Generic;
using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Device
{
    /// <summary>
    /// 実機のデバイス（Arduino）に、食感ごとの内容量を指令し、ハードウェアの確定ボタンを決定として届けます。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 体験者のリグ（<see cref="PlayerRig"/>）が持つデバイスの <see cref="IDevice.TextureChanged"/> を受けて、
    /// その食感に決めた内容量（%）まで、実機を動かします。噛む操作と、決定の操作は変えません。
    /// 噛んだことは、デバイスでは検知できないので、これまでどおり、手の追跡（左手を握る）で知らせます。
    /// </para>
    /// <para>
    /// 実機の確定ボタンが押されると、リグのデバイスの決定として届けます。
    /// 実機にボタンが付くまでは、これまでの決定の操作（左手のつまみなど）が、そのまま使えます。
    /// 実機がつながっていなくても、リグのデバイス（いまは <see cref="MockDevice"/>）は、これまでどおり動きます。
    /// </para>
    /// <para>
    /// Windows のエディタとアプリでは、Play すると自動で用意されます。シーンに置いておけば、その設定を使います。
    /// COM ポートは、決めません。実機が出す合図（<c>(h,MOG1)</c>）が届いたポートを、自動で見つけます。
    /// 通信の約束は <c>docs/device-protocol.md</c> に書いてあります。
    /// </para>
    /// <para>
    /// 実機は、起動した直後に、自分で原点出しをします（押し出す側の端まで回して、100 % と決める）。
    /// 動いている間は、指令を送りません。ずれを直したいときは、<see cref="RequestHome"/> で原点出しを頼めます。
    /// </para>
    /// <para>
    /// 指令は、実機が止まっているときにだけ送ります。動いている間に重ねると、内容量の記録がずれるためです。
    /// 動いている間に食感が変わったら、最後の 1 つだけを覚えておき、止まってから送ります。
    /// </para>
    /// </remarks>
    public sealed class DeviceActuator : MonoBehaviour
    {
        /// <summary>食感と、その食感のときの内容量です。</summary>
        [Serializable]
        public struct TextureAmount
        {
            [Tooltip("食感の ID です。リグのデバイスが出す食感と同じ文字にします。")]
            public string textureId;

            [Tooltip("この食感のときの内容量（%）です。100 が満タン、0 が空です。")]
            [Range(0, 100)]
            public int percent;
        }

        [SerializeField]
        [Tooltip("実機とつなぐ。外すと、通信せず、Mock だけで動く。")]
        private bool _useRealDevice = true;

        [SerializeField]
        [Tooltip("通信の速さ。Arduino のスケッチ（BAUD_RATE）と同じにする。")]
        private int _baudRate = 9600;

        [SerializeField]
        [Tooltip("食感ごとの内容量。実際の値は #54 で決める。いまは、a を満タンにして、f へ向かって減らす仮の値。")]
        private TextureAmount[] _textureAmounts =
        {
            new TextureAmount { textureId = "placeholder-a", percent = 100 },
            new TextureAmount { textureId = "placeholder-b", percent = 80 },
            new TextureAmount { textureId = "placeholder-c", percent = 60 },
            new TextureAmount { textureId = "placeholder-d", percent = 40 },
            new TextureAmount { textureId = "placeholder-e", percent = 20 },
            new TextureAmount { textureId = "placeholder-f", percent = 0 },
        };

        [SerializeField]
        [Range(-1, 100)]
        [Tooltip("つながったとき、実機の実際の内容量がこの値だと伝える（モーターは回さない）。実機の記録と実物がずれているときに使う。-1 なら、伝えない。Arduino は、起動直後を 100 % と覚えている。")]
        private int _tellDeviceInsideOnConnect = -1;

        [SerializeField]
        [Min(0.1f)]
        [Tooltip("指令を送ってから、実機が動き出すのを待つ最大の秒数。動かす量が小さいと、動き出しを見落とすことがあるため、この秒数で諦めて次へ進む。")]
        private float _startTimeoutSeconds = 0.5f;

        [SerializeField]
        [Tooltip("つながった・切れた・確定ボタンを、Console に出す。")]
        private bool _logEvents = true;

        private enum Phase
        {
            Idle,
            WaitingForStart,
            Driving,
        }

        private readonly Dictionary<string, int> _percentByTexture = new Dictionary<string, int>();
        private SerialLink _link;
        private IDevice _device;
        private MockDevice _mockDevice;
        private Phase _phase = Phase.Idle;
        private DeviceStatus _status;
        private bool _hasStatus;
        private bool _wasConnected;
        private int _wantedPercent = -1;
        private bool _homeRequested;
        private int _requestedSelfTestCycles;
        private float _sentAt;

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        // Windows のエディタとアプリでは、置いていなければ自動で用意する。Mock と同じく、手配線を要らなくするため。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateWhenMissing()
        {
            if (FindFirstObjectByType<DeviceActuator>() != null)
            {
                return;
            }

            var holder = new GameObject("DeviceActuator");
            holder.AddComponent<DeviceActuator>();
            DontDestroyOnLoad(holder);
        }
#endif

        /// <summary>
        /// 実機に、原点出しを頼みます。押し出す側の端まで回して、そこを 100 % と決めさせます。
        /// </summary>
        /// <remarks>
        /// 端にぶつけたまま回すので、歯車に負担がかかります。体験者が噛んでいない間に、ずれを直したいときだけ使います。
        /// 実機が動いているときは、止まってから送ります。つながっていないときは、つながってから送ります。
        /// </remarks>
        [ContextMenu("原点出しをする")]
        public void RequestHome()
        {
            _homeRequested = true;
        }

        /// <summary>
        /// 実機に、動作確認を頼みます。押す・引くを、それぞれ 50 % ぶん、指定の回数くり返します。押すほうから始まります。
        /// </summary>
        /// <param name="cycles">押す・引くを 1 往復として、くり返す回数です。1〜20 の外は、範囲内に丸めます。</param>
        /// <remarks>
        /// 内容量の記録で、端にいて動けない向きの動きは、飛ばします。起動した直後は押し切った位置なので、最初の「押す」は飛ばします。
        /// 実機が動いているときは、止まってから送ります。つながっていないときは、つながってから送ります。
        /// </remarks>
        public void RequestSelfTest(int cycles)
        {
            _requestedSelfTestCycles = cycles;
        }

        [ContextMenu("動作確認をする（押し引き 3 回）")]
        private void RequestSelfTestFromMenu()
        {
            RequestSelfTest(3);
        }

        private void Awake()
        {
            foreach (TextureAmount amount in _textureAmounts)
            {
                if (!string.IsNullOrEmpty(amount.textureId))
                {
                    _percentByTexture[amount.textureId] = Mathf.Clamp(amount.percent, DeviceProtocol.MinPercent, DeviceProtocol.MaxPercent);
                }
            }
        }

        private void OnEnable()
        {
            if (_useRealDevice)
            {
                _link = new SerialLink(_baudRate);
            }
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_link != null)
            {
                _link.Dispose();
                _link = null;
            }

            _hasStatus = false;
            _wasConnected = false;
            _phase = Phase.Idle;
        }

        private void Update()
        {
            FollowRigDevice();
            if (_link == null)
            {
                return;
            }

            while (_link.TryDequeueLine(out string line))
            {
                if (DeviceProtocol.TryParse(line, out DeviceMessage message))
                {
                    HandleMessage(message);
                }
            }

            ReportConnectionChange();
            SendNextCommandIfIdle();
        }

        // リグのデバイスは、起動の途中で決まる。決まったら、また差し替わったら、受け取り直す。
        private void FollowRigDevice()
        {
            IDevice current = PlayerRig.Current != null ? PlayerRig.Current.Device : null;
            if (ReferenceEquals(current, _device))
            {
                return;
            }

            Unsubscribe();
            _device = current;
            _mockDevice = current as MockDevice;
            if (_device != null)
            {
                _device.TextureChanged += Device_TextureChanged;
            }
        }

        private void Unsubscribe()
        {
            if (_device != null)
            {
                _device.TextureChanged -= Device_TextureChanged;
            }

            _device = null;
            _mockDevice = null;
        }

        private void Device_TextureChanged(TextureId texture)
        {
            if (_percentByTexture.TryGetValue(texture.Value, out int percent))
            {
                _wantedPercent = percent;
            }
        }

        private void HandleMessage(DeviceMessage message)
        {
            if (message.Kind == DeviceMessageKind.Hello)
            {
                return;
            }

            if (message.Kind == DeviceMessageKind.ConfirmPressed)
            {
                if (_logEvents)
                {
                    Debug.Log("[DeviceActuator] 実機の確定ボタンが押されました", this);
                }

                if (_mockDevice != null)
                {
                    _mockDevice.PressConfirm();
                }

                return;
            }

            bool isFirstStatus = !_hasStatus;
            _status = message.Status;
            _hasStatus = true;
            if (_mockDevice != null)
            {
                _mockDevice.ContentPercent = _status.PercentInside;
            }

            if (isFirstStatus)
            {
                TellDeviceInsidePercentIfSet();
            }

            AdvancePhase();
        }

        private void TellDeviceInsidePercentIfSet()
        {
            if (_tellDeviceInsideOnConnect >= 0)
            {
                _link.Send(DeviceProtocol.EncodeSetCurrent(_tellDeviceInsideOnConnect));
            }
        }

        // 実機が動き出したら Driving、止まったら Idle にする。
        private void AdvancePhase()
        {
            if (_phase == Phase.WaitingForStart && _status.IsDriving)
            {
                _phase = Phase.Driving;
            }
            else if (_phase == Phase.Driving && !_status.IsDriving)
            {
                _phase = Phase.Idle;
            }
        }

        private void ReportConnectionChange()
        {
            bool isConnected = _link.IsConnected;
            if (isConnected == _wasConnected)
            {
                return;
            }

            _wasConnected = isConnected;
            if (!isConnected)
            {
                // 切れたら、状態は古い。つなぎ直して、新しい状態が届くまで、指令を送らない。
                _hasStatus = false;
                _phase = Phase.Idle;
            }

            if (_logEvents)
            {
                Debug.Log(isConnected
                    ? "[DeviceActuator] 実機につながりました（" + _link.PortName + "）"
                    : "[DeviceActuator] 実機との通信が切れました。つなぎ直しを待ちます", this);
            }
        }

        private void SendNextCommandIfIdle()
        {
            if (!_link.IsConnected || !_hasStatus)
            {
                return;
            }

            if (_phase == Phase.WaitingForStart && Time.unscaledTime - _sentAt > _startTimeoutSeconds)
            {
                _phase = Phase.Idle;
            }

            // 実機が自分で動いている間（起動直後の原点出しなど）は、送らない。
            if (_phase != Phase.Idle || _status.IsDriving)
            {
                return;
            }

            if (_homeRequested)
            {
                _homeRequested = false;
                _link.Send(DeviceProtocol.EncodeHome());
                _phase = Phase.WaitingForStart;
                _sentAt = Time.unscaledTime;
                return;
            }

            if (_requestedSelfTestCycles > 0)
            {
                int cycles = _requestedSelfTestCycles;
                _requestedSelfTestCycles = 0;
                _link.Send(DeviceProtocol.EncodeSelfTest(cycles));
                _phase = Phase.WaitingForStart;
                _sentAt = Time.unscaledTime;
                return;
            }

            if (_wantedPercent < 0)
            {
                return;
            }

            string command = DeviceProtocol.EncodeMoveTo(_status.PercentInside, _wantedPercent);
            if (command == null)
            {
                return;
            }

            _link.Send(command);
            _phase = Phase.WaitingForStart;
            _sentAt = Time.unscaledTime;
        }
    }
}
