using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 村人の気持ち。<see cref="FieldStages"/> が、この人の名前があるステップ（<see cref="FieldStep"/> の Helped）がすべて済むまで「悩む」にする。
    /// 悩んでいる間: 頭の上に印を出し、肩を落としてゆっくり立つ（Worried）。ときどき、がっくりうなだれる（Slump = Defeat）。
    ///   うなだれる間隔と長さは人ごとにばらつかせ、全員が同時に動かないようにする（止まって見えないように）。
    /// 元気: Idle。ステップが済んだときは、元気な人が Victory を 1 回やって喜ぶ。
    /// Animator は Villager.controller（VillagePrefabGenerator が作る）。村人の Prefab に付いている。
    /// </summary>
    public sealed class VillagerMood : MonoBehaviour
    {
        static readonly int WorriedBool = Animator.StringToHash("Worried");
        static readonly int SlumpBool = Animator.StringToHash("Slump");
        static readonly int DeliveredTrigger = Animator.StringToHash("Delivered");

        [Tooltip("悩んでいる間だけ見える、頭の上の印")]
        [SerializeField] GameObject worryMark;

        [Tooltip("体を起こしている時間（秒）の範囲。この間でばらつく")]
        [SerializeField] Vector2 uprightSeconds = new Vector2(4f, 9f);

        [Tooltip("うなだれている時間（秒）の範囲。この間でばらつく")]
        [SerializeField] Vector2 slumpSeconds = new Vector2(3f, 6f);

        Animator _animator;
        bool _applied;
        bool _worried;
        bool _slumped;
        float _nextSwitch;

        /// <summary>違う物が届いたときに送る、Animator のトリガーの名前（Villager.controller に入っている）。</summary>
        public const string MissedTrigger = "Missed";

        public GameObject WorryMark { get => worryMark; set => worryMark = value; }
        public bool IsWorried => _worried;

        /// <summary>
        /// Animator に、その名前のトリガーを送る。Animator Controller にその名前のトリガーが無ければ、何もしないで false。
        /// 動きを足すときは、Animator Controller にトリガーとステートを足し、ここに名前を渡すだけでよい（コードは直さない）。
        /// </summary>
        public bool React(string trigger)
        {
            if (string.IsNullOrEmpty(trigger) || !HasAnimator()) return false;
            foreach (var parameter in _animator.parameters)
            {
                if (parameter.type != AnimatorControllerParameterType.Trigger || parameter.name != trigger) continue;
                _animator.SetTrigger(parameter.nameHash);
                return true;
            }
            return false;
        }

        /// <summary>悩むか元気かを決める。celebrate なら、元気な人は喜ぶ（ステップが済んだとき）。<see cref="FieldStages"/> が呼ぶ。</summary>
        public void SetWorried(bool worried, bool celebrate)
        {
            var wasWorried = _worried;
            _worried = worried;
            if (worryMark != null) worryMark.SetActive(_worried);
            if (!HasAnimator()) return;

            var first = !_applied;
            _applied = true;

            _animator.SetBool(WorriedBool, _worried);
            if (first)
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
                if (celebrate) _animator.SetTrigger(DeliveredTrigger); // ステップが済んだら喜ぶ
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

        // FieldStages が先に呼んでもよいように、Awake を待たずに探す。
        bool HasAnimator() => (_animator != null || (_animator = GetComponentInChildren<Animator>()) != null) && _animator.runtimeAnimatorController != null && _animator.isActiveAndEnabled;
    }
}
