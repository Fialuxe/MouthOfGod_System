using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 村人の気持ち。<see cref="FieldStages"/> の段階が <see cref="calmFromStage"/> になるまでは悩んでいる。
    /// 悩んでいる間: 頭の上に印を出し、肩を落としてゆっくり立つ（Worried）。ときどき、がっくりうなだれる（Slump = Defeat）。
    ///   うなだれる間隔と長さは人ごとにばらつかせ、全員が同時に動かないようにする（止まって見えないように）。
    /// 元気: Idle。段階が上がったときは、元気な人が Victory を 1 回やって喜ぶ。
    /// Animator は Villager.controller（VillagePrefabGenerator が作る）。村人の Prefab に付いている。
    /// </summary>
    public sealed class VillagerMood : MonoBehaviour, IStageListener
    {
        static readonly int WorriedBool = Animator.StringToHash("Worried");
        static readonly int SlumpBool = Animator.StringToHash("Slump");
        static readonly int DeliveredTrigger = Animator.StringToHash("Delivered");

        [Tooltip("この段階になると、悩むのをやめて元気になる")]
        [SerializeField, Min(0)] int calmFromStage = 1;

        [Tooltip("悩んでいる間だけ見える、頭の上の印")]
        [SerializeField] GameObject worryMark;

        [Tooltip("体を起こしている時間（秒）の範囲。この間でばらつく")]
        [SerializeField] Vector2 uprightSeconds = new Vector2(4f, 9f);

        [Tooltip("うなだれている時間（秒）の範囲。この間でばらつく")]
        [SerializeField] Vector2 slumpSeconds = new Vector2(3f, 6f);

        Animator _animator;
        bool _worried;
        bool _slumped;
        float _nextSwitch;

        public int CalmFromStage { get => calmFromStage; set => calmFromStage = value; }
        public GameObject WorryMark { get => worryMark; set => worryMark = value; }
        public bool IsWorried => _worried;

        void Awake() => _animator = GetComponentInChildren<Animator>();

        public void OnStageChanged(int previous, int current)
        {
            var wasWorried = _worried;
            _worried = Stages.IsWorried(calmFromStage, current);
            if (worryMark != null) worryMark.SetActive(_worried);
            if (!HasAnimator()) return;

            _animator.SetBool(WorriedBool, _worried);
            if (previous < 0)
            {
                // 始まり: 人ごとに、動きの途中から始める。うなだれている人と起きている人を混ぜる
                _slumped = _worried && Random.value < 0.4f;
                _animator.SetBool(SlumpBool, _slumped);
                _animator.Play(_slumped ? "Slump" : _worried ? "Worried" : "Idle", 0, Random.value);
                ScheduleNextSwitch(firstTime: true);
                return;
            }

            if (!_worried)
            {
                _slumped = false;
                _animator.SetBool(SlumpBool, false);
                if (current > previous) _animator.SetTrigger(DeliveredTrigger); // 段階が上がったら喜ぶ
            }
            else if (!wasWorried)
            {
                ScheduleNextSwitch(firstTime: false);
            }
        }

        // 悩んでいる間、起きる ⇄ うなだれる、を繰り返す。
        void Update()
        {
            if (!_worried || !HasAnimator() || Time.time < _nextSwitch) return;
            _slumped = !_slumped;
            _animator.SetBool(SlumpBool, _slumped);
            ScheduleNextSwitch(firstTime: false);
        }

        void ScheduleNextSwitch(bool firstTime)
        {
            var range = _slumped ? slumpSeconds : uprightSeconds;
            var seconds = Random.Range(range.x, range.y);
            if (firstTime) seconds *= Random.value; // 始まりは、途中まで過ぎたことにして、切り替えの時刻をずらす
            _nextSwitch = Time.time + seconds;
        }

        bool HasAnimator() => _animator != null && _animator.runtimeAnimatorController != null && _animator.isActiveAndEnabled;
    }
}
