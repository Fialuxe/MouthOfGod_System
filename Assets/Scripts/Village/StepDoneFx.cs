using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// ステップが済んだときの音と粒。<see cref="FieldStages"/> と同じ物に付ける。
    /// 助かった村人（<see cref="FieldStep.Helped"/>）の頭の上に喜びの粒を出し、音を 2D で鳴らす。
    /// <see cref="FieldStages.StepDone"/> を聞くだけなので、ステップの下に物を置く作り方には影響しない。
    /// 何も入れなければ何もしない。
    /// </summary>
    [RequireComponent(typeof(FieldStages))]
    public sealed class StepDoneFx : MonoBehaviour
    {
        [Tooltip("助かった村人の頭の上に出す粒。一度だけ出して、終わると自分で消える（Stop Action を Destroy にする）")]
        [SerializeField] ParticleSystem joy;

        [Tooltip("ステップが済んだときの音（短い物）")]
        [SerializeField] AudioClip doneClip;

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
            _stages.StepDone += OnStepDone;
        }

        void OnDisable()
        {
            if (_stages != null) _stages.StepDone -= OnStepDone;
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
            if (doneClip != null) _audio.PlayOneShot(doneClip, volume);
            else Debug.LogWarning("[Village] StepDoneFx の Done Clip が空なので、音は鳴らない", this);
        }

        static Vector3 HeadOf(VillagerMood mood)
            => mood.WorryMark != null ? mood.WorryMark.transform.position : mood.transform.position;

        void Burst(Vector3 position) => Instantiate(joy, position, Quaternion.identity);
    }
}
