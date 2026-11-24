using UnityEngine;

namespace MouthOfGod.Fork
{
    /// <summary>
    /// フォークで刺せる雲です。刺されると隠れ、しばらくすると戻ります。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 雲の見た目に、当たり判定（Collider）と一緒に付けます。
    /// 隠れている間は、見た目と当たり判定の両方を切ります。
    /// </para>
    /// <para>
    /// 光らせる・近くへ寄せるなどの見せ方は、ここでは扱いません。
    /// 合図（Cues）を受ける側が足します。
    /// </para>
    /// </remarks>
    public sealed class Cloud : MonoBehaviour
    {
        [Tooltip("刺されてから、また刺せるようになるまでの秒数。仮の値")]
        [SerializeField, Min(0f)] float respawnSeconds = 3f;

        float _hiddenUntil = float.NegativeInfinity;
        bool _hidden;

        // 当たり判定をトリガーにします。口から飛ばした物が、手前の雲で跳ね返らないようにするためです。
        // フォークの先には Rigidbody があるので、トリガーどうしでも刺せます。
        void Awake()
        {
            foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.isTrigger = true;
        }

        /// <summary>隠れておらず、いま刺せるときに <see langword="true"/> です。</summary>
        public bool IsAvailable => !_hidden;

        /// <summary>
        /// 刺されたことにして雲を隠します。<c>Respawn Seconds</c> たつと戻ります。
        /// </summary>
        /// <remarks>
        /// 隠れている間に呼ぶと、戻る時刻をそこから数え直します。
        /// </remarks>
        public void Take()
        {
            _hiddenUntil = Time.time + respawnSeconds;
            SetHidden(true);
        }

        void Update()
        {
            if (_hidden && Time.time >= _hiddenUntil) SetHidden(false);
        }

        void SetHidden(bool hidden)
        {
            _hidden = hidden;
            foreach (Renderer r in GetComponentsInChildren<Renderer>()) r.enabled = !hidden;
            foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = !hidden;
        }
    }
}
