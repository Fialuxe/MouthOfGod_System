using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>巫女の居場所の決め方です。</summary>
    public enum MikoMode
    {
        /// <summary>いまの場所にいます。</summary>
        Stay,

        /// <summary>
        /// 目印の Transform へ向かいます。着いたら、Go の前の居方に戻ります。
        /// </summary>
        Go,

        /// <summary>体験者のそばについて来ます。</summary>
        Follow,
    }

    /// <summary>
    /// 巫女を動かします。目印へ行く・体験者について来る・その場にいる、を切り替えます。
    /// </summary>
    /// <remarks>
    /// 位置は直接動かさず、<see cref="MikoFloat.Anchor"/> を動かします。
    /// 上下の揺れは <see cref="MikoFloat"/> が足します。
    /// </remarks>
    [RequireComponent(typeof(MikoFloat))]
    public sealed class MikoMover : MonoBehaviour
    {
        [SerializeField] MikoMode mode = MikoMode.Stay;
        [SerializeField] float moveSpeed = 0.75f;  // Go のときの最高速度（m/秒）
        [Header("Follow")]
        [SerializeField] float followDistance = 1.2f;  // 体験者の前方への距離（m）
        [SerializeField] float followSide = 0.5f;  // 体験者の右への距離（m）。負で左
        [SerializeField] float followHeight = -0.15f;  // 目の高さからの高さ（m）。負で目より下
        [SerializeField] float followSpeed = 2.5f;  // 横に追いつく最高速度（m/秒）
        [SerializeField] float followDeadZone = 0.4f;  // 目標から横にこの距離（m）までは動きません
        // 顔の上下の向きを取り入れる割合です。0 で水平、1 で顔の向きどおりです。
        // 体験者は台を見下ろすことが多く、上げると巫女が台の上へ降りてミニチュアを隠します。
        // そのため、既定は 0 にしています。
        [SerializeField, Range(0f, 1f)] float followPitch;
        [SerializeField] float followHeightSpeed = 3f;  // 高さを追いつく最高速度（m/秒）
        [SerializeField] float followHeightDeadZone = 0.08f;  // 目標から高さにこの距離（m）までは動きません

        [Header("なめらかさ")]
        [SerializeField, Min(0.01f)] float smoothTime = 0.45f;  // 目標に追いつくまでの目安（秒）。大きいほど、ゆったり動きます

        MikoFloat _float;
        // SmoothDamp が使う、いまの速さです。
        Vector3 _velocity;
        Transform _goal;
        // Go に入る前の居方です。着いたら、ここへ戻ります。
        // ついて来ていたなら、着いたあとも、またついて来ます。
        MikoMode _modeAfterGo;

        /// <summary>いまの居場所の決め方です。</summary>
        public MikoMode Mode => mode;

        void Awake() => _float = GetComponent<MikoFloat>();

        /// <summary>その場にとどまります。動いている途中なら、そこで止まります。</summary>
        public void Stay()
        {
            _velocity = Vector3.zero;
            mode = MikoMode.Stay;
            _goal = null;
        }

        /// <summary>体験者について来ます。目印へ向かう途中なら、それをやめます。</summary>
        public void Follow()
        {
            mode = MikoMode.Follow;
            _goal = null;
        }

        /// <summary>目印へ向かいます。</summary>
        /// <param name="goal">行き先の目印です。<see langword="null"/> なら何もしません。</param>
        /// <remarks>
        /// 着いたら、呼ぶ前の居方に戻ります。ついて来ていたなら、またついて来ます。
        /// 向かう途中で目印が Destroy されたときも、呼ぶ前の居方に戻ります。
        /// </remarks>
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
            // 顔の上下の向きも、followPitch の分だけ取り入れます。見上げれば上に、見下ろせば下に巫女が来ます。
            var aim = Vector3.Slerp(flat, camera.transform.forward, followPitch);
            var right = Vector3.Cross(Vector3.up, flat);
            var wanted = camera.transform.position + aim * followDistance + right * followSide + Vector3.up * followHeight;

            // 横と高さを分けて追います。不感帯の中では目標を動かさず、速さをなめらかに 0 へ落とします。
            // 高さは、小さなずれでも速く追いつきます。目の高さが変わっても、巫女が置いていかれないためです。
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
