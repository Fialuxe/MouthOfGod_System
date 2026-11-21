using MouthOfGod.Core;
using UnityEngine;
using UnityEngine.Events;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 物が届いたときの、フィールドの標準の反応です。<see cref="FieldStages"/> と同じ物に付けます。
    /// </summary>
    /// <remarks>
    /// 欲しい物が届くと、助かった村人の頭の上に粒を出して音を鳴らします。
    /// 違う物が届くと、困っている村人に Animator のトリガーを送り、がっかりした音を鳴らします。
    /// 粒と音が空なら、Resources にある標準の物を使います。
    /// 村人の動きを変えるときは、Animator Controller の、トリガーで入るステートの Motion を差し替えます。コードは直しません。
    /// トリガーを持たない Animator（動物など）には、何もしません。
    /// フィールドごとの反応を足すときは、On Requested と On Missed につなぎます。
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

        [Header("置き場")]
        [Tooltip("このフィールドの効果（喜びの粒・消える煙・出現の星）を、この子として出す。空なら、シーンの直下")]
        [SerializeField] Transform fxParent;

        [Header("音")]
        [SerializeField, Range(0f, 1f)] float volume = 0.6f;

        FieldStages _stages;
        AudioSource _audio;

        /// <summary>このフィールドの効果を置く親です。空ならシーンの直下です。</summary>
        public Transform FxParent
        {
            get => fxParent;
            set => fxParent = value;
        }

        /// <summary>from が属するフィールドの、効果を置く親です。見つからなければ null です。</summary>
        internal static Transform FxParentOf(Component from)
        {
            var fx = from != null ? from.GetComponentInParent<DeliveryFx>() : null;
            return fx != null ? fx.fxParent : null;
        }

        void OnEnable()
        {
            _stages = GetComponent<FieldStages>();
            if (_audio == null)
            {
                // 2D の音にします。ミニチュアは小さいので、3D だと聞く位置しだいで聞こえないことがあります。
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
            if (delivered.Kind == DeliveryKind.Requested) return; // 喜ぶ反応は OnStepDone で出します
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

        void Burst(Vector3 position) => Instantiate(joy, position, Quaternion.identity, fxParent);
    }
}
