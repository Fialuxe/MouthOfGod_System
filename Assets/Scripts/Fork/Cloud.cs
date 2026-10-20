using UnityEngine;

namespace MouthOfGod.Fork
{
    /// <summary>
    /// フォークで刺せる雲。当たり判定（Collider）と一緒に、雲の見た目に付ける。
    /// 刺されると、見た目と当たり判定を隠し、<c>Respawn Seconds</c> たったら戻る。
    /// </summary>
    /// <remarks>
    /// 光らせる・近くへ寄せるなどの見せ方は、ここには書かない。合図（Cues）を受ける側が足す。
    /// </remarks>
    public sealed class Cloud : MonoBehaviour
    {
        [Tooltip("刺されてから、また刺せるようになるまでの秒数。仮の値")]
        [SerializeField, Min(0f)] float respawnSeconds = 3f;

        float _hiddenUntil = float.NegativeInfinity;
        bool _hidden;

        /// <summary>いま刺せる（隠れていない）。</summary>
        public bool IsAvailable => !_hidden;

        /// <summary>刺された。隠して、時間がたったら戻す。</summary>
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
