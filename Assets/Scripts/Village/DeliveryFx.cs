using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 物が届いたときの、フィールドの標準の反応。<see cref="FieldStages"/> と同じ物に付ける（無ければ FieldStages が自分で付ける）。
    /// 何も入れなくても、標準の反応が出る。見た目を変えたいときだけ、Inspector で差し替える。
    /// </summary>
    /// <remarks>
    /// - 欲しい物が届いた（ステップが済んだ）: 助かった村人の頭の上に喜びの粒を出し、音を 2D で鳴らす。村人の喜ぶ動きは <see cref="VillagerMood"/> が出す。
    /// - 違う物が届いた（頼まれていない物・よその物。どちらも同じ反応）: 困っている村人に、Animator のトリガー（<c>Missed Trigger</c>）を送り、がっかりした音を鳴らす。
    /// - 粒と音を空にしておくと、Resources の標準の物を使う（粒: Prefabs/Village/Fx_Joy、音: Sounds/Village/Done と Missed）。
    ///   動きを変えるときは、村人の Animator Controller の、このトリガーで入るステートの Motion を差し替える。コードは直さない。
    ///   トリガーを持たない Animator（動物など）には、何もしない。
    /// - 届いた物そのものの消え方（違う物は霞のように消える）は、<see cref="DeliveryZone"/> が受け持つ。
    /// - 独自の反応を足すときは、On Requested / On Missed に、つなぐ。
    /// </remarks>
    public sealed class DeliveryFx : MonoBehaviour
    {
        const string DefaultJoyPath = "Prefabs/Village/Fx_Joy";
        const string DefaultDoneClipPath = "Sounds/Village/Done";     // Kenney「Music Jingles」の jingles_PIZZI02（CC0）
        const string DefaultMissedClipPath = "Sounds/Village/Missed"; // このリポジトリで作った音（下がる 2 音）

        [Header("欲しい物が届いたとき")]
        [Tooltip("助かった村人の頭の上に出す粒。一度だけ出して、終わると自分で消える（Stop Action を Destroy にする）。空なら Resources/Prefabs/Village/Fx_Joy")]
        [SerializeField] ParticleSystem joy;

        [Tooltip("ステップが済んだときの音（短い物）。空なら Resources/Sounds/Village/Done")]
        [SerializeField] AudioClip doneClip;

        [Tooltip("欲しい物が届いたときに、ほかに呼ぶもの")]
        [SerializeField] UnityEvent onRequested = new UnityEvent();

        [Header("違う物が届いたとき")]
        [Tooltip("困っている村人の Animator に送るトリガーの名前。Animator Controller に、この名前のトリガーが無ければ何もしない")]
        [SerializeField] string missedTrigger = VillagerMood.MissedTrigger;

        [Tooltip("違う物が届いたときの音。空なら Resources/Sounds/Village/Missed")]
        [SerializeField] AudioClip missedClip;

        [Tooltip("違う物が届いたときに、ほかに呼ぶもの")]
        [SerializeField] UnityEvent onMissed = new UnityEvent();

        [Header("音")]
        [SerializeField, Range(0f, 1f)] float volume = 0.6f;

        FieldStages _stages;
        AudioSource _audio;

        void OnEnable()
        {
            _stages = GetComponent<FieldStages>();
            if (_audio == null)
            {
                // 2D の音にする。村が小さく、聞く人の位置で音量が変わると、鳴っても聞こえないことがあるため
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }
            if (joy == null) joy = Resources.Load<ParticleSystem>(DefaultJoyPath);
            if (doneClip == null) doneClip = Resources.Load<AudioClip>(DefaultDoneClipPath);
            if (missedClip == null) missedClip = Resources.Load<AudioClip>(DefaultMissedClipPath);
            if (_stages == null) return;
            _stages.StepDone += OnStepDone;
            _stages.Received += OnReceived;
        }

        void OnDisable()
        {
            if (_stages == null) return;
            _stages.StepDone -= OnStepDone;
            _stages.Received -= OnReceived;
        }

        void OnStepDone(FieldStep step)
        {
            if (joy != null)
            {
                var any = false;
                foreach (var mood in step.Helped)
                {
                    if (mood == null) continue;
                    Burst(HeadOf(mood));
                    any = true;
                }
                if (!any) Burst(step.Anchor.position);
            }
            Play(doneClip);
            onRequested.Invoke();
        }

        void OnReceived(Delivered delivered)
        {
            if (delivered.Kind == DeliveryKind.Requested) return; // 喜ぶのは StepDone で
            foreach (var mood in GetComponentsInChildren<VillagerMood>())
                if (mood.IsWorried) mood.React(missedTrigger);
            Play(missedClip);
            onMissed.Invoke();
        }

        void Play(AudioClip clip)
        {
            if (clip != null && _audio != null) _audio.PlayOneShot(clip, volume);
        }

        static Vector3 HeadOf(VillagerMood mood)
            => mood.WorryMark != null ? mood.WorryMark.transform.position : mood.transform.position;

        void Burst(Vector3 position) => Instantiate(joy, position, Quaternion.identity);
    }
}
