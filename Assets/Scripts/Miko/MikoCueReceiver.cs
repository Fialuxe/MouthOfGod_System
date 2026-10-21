using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 合図（Cues）のうち、名前が <c>miko.</c> で始まるものを受けて、巫女に身振りをさせる。
    /// 受けた合図は、すべて Console に出す。
    /// </summary>
    public sealed class MikoCueReceiver : MonoBehaviour
    {
        const string Prefix = "miko.";
        static readonly int PointTrigger = Animator.StringToHash("Point");

        [SerializeField] Animator animator;
        [SerializeField] Transform pointTarget; // 指さす先の既定。セリフに指さす物が無いときに使う
        [SerializeField] float pointLookSeconds = 3f; // 指さしたあと、その方を向いている時間

        float _lookUntil;
        Transform _aim;

        void OnEnable() => Cues.Requested += OnCue;
        void OnDisable() => Cues.Requested -= OnCue;

        void Update()
        {
            if (_lookUntil > 0f && Time.time >= _lookUntil)
            {
                _lookUntil = 0f;
                var mikoFloat = GetComponent<MikoFloat>();
                if (mikoFloat != null) mikoFloat.LookAt = null;
            }
        }

        /// <summary>target の方を向いて、指さしをする。target が空なら、既定の先（Point Target）を使う。</summary>
        public void PointAt(Transform target)
        {
            _aim = target;
            Point();
        }

        // Editor から試す。Play 中に、コンポーネントの右上のメニュー（⋮）から選ぶ。
        [ContextMenu("Point")]
        void PointFromEditor() => Point();

        /// <summary>指さしをする。</summary>
        public void Point()
        {
            var target = _aim != null ? _aim : pointTarget;
            _aim = null;
            if (target != null)
            {
                var mikoFloat = GetComponent<MikoFloat>();
                if (mikoFloat != null)
                {
                    mikoFloat.LookAt = target;
                    _lookUntil = Time.time + pointLookSeconds;
                }
                GetComponent<MikoPointer>()?.Begin(target);
            }
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.SetTrigger(PointTrigger);
        }

        void OnCue(CueRequested request)
        {
            string name = request.Cue.Value;
            if (!name.StartsWith(Prefix)) return;
            Debug.Log($"[Miko] 合図: {name}");
            if (name == Prefix + "point") Point();
            if (name == Prefix + "hush") GetComponent<MikoSpeech>()?.Hush();

            var mover = GetComponent<MikoMover>();
            if (name == Prefix + "follow") mover?.Follow();
            if (name == Prefix + "stay") mover?.Stay();

            // セリフ: miko.say.welcome など。
            const string SayPrefix = Prefix + "say.";
            if (name.StartsWith(SayPrefix))
            {
                var speech = GetComponent<MikoSpeech>();
                if (speech == null || !speech.Say(name.Substring(SayPrefix.Length)))
                    Debug.LogWarning($"[Miko] セリフが見つかりません: {name}");
                return;
            }

            // 表情: miko.face.happy など。
            const string FacePrefix = Prefix + "face.";
            if (!name.StartsWith(FacePrefix)) return;
            var face = GetComponent<MikoFace>();
            if (face != null && !face.Set(name.Substring(FacePrefix.Length)))
                Debug.LogWarning($"[Miko] 分からない表情です: {name}");
        }
    }
}
