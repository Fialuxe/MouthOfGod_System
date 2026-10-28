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
        [SerializeField] float followDeadZone = 0.4f; // 目標から横にこの距離までは、動かない
        [SerializeField, Range(0f, 1f)] float followPitch; // 顔の上下の向きを取り入れる割合（0 で水平、1 で顔の向きどおり）。体験者は台を見下ろすことが多く、上げると巫女が台の上へ降りてきて、ミニチュアを隠すので、既定は 0
        [SerializeField] float followHeightSpeed = 3f; // 高さを追いつく速さ（m / 秒）
        [SerializeField] float followHeightDeadZone = 0.08f; // 目標から高さにこの距離までは、動かない

        [Header("なめらかさ")]
        [SerializeField, Min(0.01f)] float smoothTime = 0.45f; // 目標に追いつくまでの目安（秒）。大きいほど、ゆったり動く

        MikoFloat _float;
        Vector3 _velocity; // なめらかに動かすための、いまの速さ
        Transform _goal;
        MikoMode _modeAfterGo; // Go に入る前の居方。着いたら、ここへ戻る（ついてきていたなら、またついてくる）

        public MikoMode Mode => mode;

        void Awake() => _float = GetComponent<MikoFloat>();

        /// <summary>その場にいる。</summary>
        public void Stay()
        {
            _velocity = Vector3.zero;
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
            if (mode != MikoMode.Go) _modeAfterGo = mode;
            _goal = goal;
            mode = MikoMode.Go;
        }

        void Update()
        {
            if (_float == null) return;
            switch (mode)
            {
                case MikoMode.Go:
                    if (_goal == null) { mode = _modeAfterGo; break; }
                    _float.Anchor = Vector3.SmoothDamp(_float.Anchor, _goal.position, ref _velocity, smoothTime, moveSpeed);
                    if ((_float.Anchor - _goal.position).sqrMagnitude < 1e-4f)
                    {
                        mode = _modeAfterGo;
                        _goal = null;
                    }
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
            var flat = camera.transform.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 1e-6f) return;
            flat.Normalize();
            // 顔の上下の向きも、followPitch のぶんだけ取り入れる（見上げれば上、見下ろせば下に、巫女が来る）
            var aim = Vector3.Slerp(flat, camera.transform.forward, followPitch);
            var right = Vector3.Cross(Vector3.up, flat);
            var wanted = camera.transform.position + aim * followDistance + right * followSide + Vector3.up * followHeight;

            // 横と高さを分けて追う。不感帯の中では目標を動かさず、速さをなめらかに 0 へ落とす。
            // 高さは、小さなずれでも、速く追いつく（目の高さが変わっても巫女が置いていかれない）
            var current = _float.Anchor;
            var gap = wanted - current;
            var flatGap = new Vector2(gap.x, gap.z);
            var goal = current;
            if (flatGap.magnitude > followDeadZone)
            {
                var g = flatGap - flatGap.normalized * followDeadZone * 0.5f;
                goal.x += g.x;
                goal.z += g.y;
            }
            if (Mathf.Abs(gap.y) > followHeightDeadZone)
                goal.y += gap.y - Mathf.Sign(gap.y) * followHeightDeadZone * 0.5f;

            var next = current;
            var flatNow = new Vector2(current.x, current.z);
            var flatVel = new Vector2(_velocity.x, _velocity.z);
            var flatNext = Vector2.SmoothDamp(flatNow, new Vector2(goal.x, goal.z), ref flatVel, smoothTime, followSpeed);
            float vy = _velocity.y;
            next.y = Mathf.SmoothDamp(current.y, goal.y, ref vy, smoothTime * 0.6f, followHeightSpeed);
            next.x = flatNext.x;
            next.z = flatNext.y;
            _velocity = new Vector3(flatVel.x, vy, flatVel.y);
            _float.Anchor = next;
        }
    }
}
