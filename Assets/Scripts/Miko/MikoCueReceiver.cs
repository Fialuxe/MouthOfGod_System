using MouthOfGod.Core;
using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 合図（Cues）のうち、名前が <c>miko.</c> で始まるものを受けて、巫女に身振りをさせる。
    /// 音声と字幕は、まだ付けていない。受けた合図は、すべて Console に出す。
    /// </summary>
    public sealed class MikoCueReceiver : MonoBehaviour
    {
        const string Prefix = "miko.";
        static readonly int PointTrigger = Animator.StringToHash("Point");

        [SerializeField] Animator animator;

        void OnEnable() => Cues.Requested += OnCue;
        void OnDisable() => Cues.Requested -= OnCue;

        // Editor から試す。Play 中に、コンポーネントの右上のメニュー（⋮）から選ぶ。
        [ContextMenu("Point")]
        void PointFromEditor()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.SetTrigger(PointTrigger);
        }

        void OnCue(CueRequested request)
        {
            string name = request.Cue.Value;
            if (!name.StartsWith(Prefix)) return;
            Debug.Log($"[Miko] 合図: {name}");
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null && name == Prefix + "point") animator.SetTrigger(PointTrigger);

            // 表情: miko.face.happy など。
            const string FacePrefix = Prefix + "face.";
            if (!name.StartsWith(FacePrefix)) return;
            var face = GetComponent<MikoFace>();
            if (face != null && !face.Set(name.Substring(FacePrefix.Length)))
                Debug.LogWarning($"[Miko] 分からない表情です: {name}");
        }
    }
}
