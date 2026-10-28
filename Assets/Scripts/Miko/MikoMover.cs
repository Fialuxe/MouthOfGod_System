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
        [SerializeField, Range(0f, 1f)] float followPitch = 0.6f; // 顔の上下の向きを取り入れる割合（0 で水平、1 で顔の向きどおり）
        [SerializeField] float followHeightSpeed = 3f; // 高さを追いつく速さ（m / 秒）
        [SerializeField] float followHeightDeadZone = 0.08f; // 目標から高さにこの距離までは、動かない

        MikoFloat _float;
        Transform _goal;
        MikoMode _modeAfterGo; // Go に入る前の居方。着いたら、ここへ戻る（ついてきていたなら、またついてくる）

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
                    _float.Anchor = Vector3.MoveTowards(_float.Anchor, _goal.position, moveSpeed * Time.deltaTime);
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

            // 横と高さを分けて追う。高さは、小さなずれでも、速く追いつく（目の高さが変わっても巫女が置いていかれない）
            var current = _float.Anchor;
            var gap = wanted - current;
            var flatGap = new Vector2(gap.x, gap.z);
            var next = current;
            if (flatGap.magnitude > followDeadZone)
            {
                var goal2 = flatGap - flatGap.normalized * followDeadZone * 0.5f;
                var step = Vector2.ClampMagnitude(goal2, followSpeed * Time.deltaTime);
                next.x += step.x;
                next.z += step.y;
            }
            if (Mathf.Abs(gap.y) > followHeightDeadZone)
            {
                var goalY = gap.y - Mathf.Sign(gap.y) * followHeightDeadZone * 0.5f;
                next.y += Mathf.Clamp(goalY, -followHeightSpeed * Time.deltaTime, followHeightSpeed * Time.deltaTime);
            }
            _float.Anchor = next;
        }
    }
}
