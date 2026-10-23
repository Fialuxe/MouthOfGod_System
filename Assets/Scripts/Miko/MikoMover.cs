using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>巫女の居場所の決め方。</summary>
    public enum MikoMode
    {
        Stay, // いまの場所にいる
        Go, // 指定の場所（Transform）へ行って、着いたら Stay になる
        Follow, // プレイヤーのそばについてくる
    }

    /// <summary>
    /// 巫女を動かす。開発者が決めた場所へ行くか、プレイヤーについてくるか、その場にいるかを切り替える。
    /// </summary>
    [RequireComponent(typeof(MikoFloat))]
    public sealed class MikoMover : MonoBehaviour
    {
        [SerializeField] MikoMode mode = MikoMode.Stay;
        [SerializeField] float moveSpeed = 0.75f; // Go のときの速さ（m / 秒）
        [Header("Follow")]
        [SerializeField] float followDistance = 1.2f; // プレイヤーの前、この距離
        [SerializeField] float followSide = 0.5f; // プレイヤーの右、この距離（負で左）
        [SerializeField] float followHeight = -0.15f; // 目の高さから、この高さ
        [SerializeField] float followSpeed = 2.5f; // 追いつく速さ（m / 秒）
        [SerializeField] float followDeadZone = 0.4f; // 目標からこの距離までは、動かない

        MikoFloat _float;
        Transform _goal;

        public MikoMode Mode => mode;

        void Awake() => _float = GetComponent<MikoFloat>();

        /// <summary>その場にいる。</summary>
        public void Stay()
        {
            mode = MikoMode.Stay;
            _goal = null;
        }

        /// <summary>プレイヤーについてくる。</summary>
        public void Follow()
        {
            mode = MikoMode.Follow;
            _goal = null;
        }

        /// <summary>目印へ行く。着いたら、その場にいる。</summary>
        public void GoTo(Transform goal)
        {
            if (goal == null) return;
            _goal = goal;
            mode = MikoMode.Go;
        }

        void Update()
        {
            if (_float == null) return;
            switch (mode)
            {
                case MikoMode.Go:
                    if (_goal == null) { mode = MikoMode.Stay; break; }
                    _float.Anchor = Vector3.MoveTowards(_float.Anchor, _goal.position, moveSpeed * Time.deltaTime);
                    if ((_float.Anchor - _goal.position).sqrMagnitude < 1e-4f) mode = MikoMode.Stay;
                    break;
                case MikoMode.Follow:
                    UpdateFollow();
                    break;
            }
        }

        void UpdateFollow()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var forward = camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return;
            forward.Normalize();
            var right = Vector3.Cross(Vector3.up, forward);
            var wanted = camera.transform.position + forward * followDistance + right * followSide + Vector3.up * followHeight;

            var current = _float.Anchor;
            var gap = wanted - current;
            if (gap.magnitude <= followDeadZone) return;
            // 目標のすぐ近く（deadZone の縁）まで、追いかける。
            var goal = wanted - gap.normalized * followDeadZone * 0.5f;
            _float.Anchor = Vector3.MoveTowards(current, goal, followSpeed * Time.deltaTime);
        }
    }
}
