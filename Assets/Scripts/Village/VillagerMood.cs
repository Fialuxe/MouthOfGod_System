using UnityEngine;

namespace MouthOfGod.Village
{
    /// <summary>
    /// 村人の気持ちを、Animator と頭の上の印で見せます。悩んでいるか元気かは <see cref="FieldStages"/> が決めます。
    /// </summary>
    /// <remarks>
    /// 悩んでいる間は Worried で、ときどきうなだれます（Slump）。間隔は人ごとにばらつかせ、全員が同時に動かないようにします。
    /// 元気なときは Idle で、ステップが済むと Victory を 1 回やって喜びます。
    /// Animator Controller は Assets/Prefabs/Village/Villager.controller です。動きを直すときは、そちらを直します。
    /// </remarks>
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

        /// <summary>違う物が届いたときに送る、Animator のトリガーの名前です。</summary>
        public const string MissedTrigger = "Missed";

        public GameObject WorryMark { get => worryMark; set => worryMark = value; }

        /// <summary>欲しい物を、頭の上の吹き出しで見せます。icon が無ければ label の文字を出します。</summary>
        public void ShowWant(Texture2D icon, string label)
        {
            if (TryGetComponent(out VillagerBubble bubble)) bubble.Show(icon, label);
            else Debug.LogWarning($"[VillagerMood] {name}: 吹き出し（VillagerBubble）が無いので、欲しい物を見せられません。", this);
        }

        /// <summary>欲しい物が終わったことを、吹き出しで見せます。届いたら resolved です。</summary>
        public void CloseWant(bool resolved)
        {
            if (TryGetComponent(out VillagerBubble bubble)) bubble.Close(resolved);
        }
        public bool IsWorried => _worried;

        /// <summary>Animator に trigger を送ります。Animator Controller にそのトリガーが無ければ false です。</summary>
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

        /// <summary>悩むか元気かを決めます。celebrate なら、元気になった人が喜びます。</summary>
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
                // 最初は、人ごとに動きの途中から始め、うなだれている人と起きている人を混ぜます。
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
                if (celebrate) _animator.SetTrigger(DeliveredTrigger);
            }
            else if (!wasWorried)
            {
                ScheduleNextSwitch(firstTime: false);
            }
        }

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
            if (firstTime) seconds *= Random.value; // 最初は切り替えの時刻をずらします
            _nextSwitch = Time.time + seconds;
        }

        // FieldStages が Awake より先に呼ぶことがあるので、その場で探します。
        bool HasAnimator() => (_animator != null || (_animator = GetComponentInChildren<Animator>()) != null) && _animator.runtimeAnimatorController != null && _animator.isActiveAndEnabled;
    }
}
