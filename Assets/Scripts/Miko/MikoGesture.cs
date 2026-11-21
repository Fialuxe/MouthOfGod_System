using UnityEngine;

namespace MouthOfGod.Miko
{
    /// <summary>
    /// 巫女に指さしをさせます。指さす先の方を向き、腕を向け、Animator の Point トリガーを送ります。
    /// </summary>
    /// <remarks>
    /// 同じオブジェクトに <see cref="MikoFloat"/> があれば、しばらく指さす先の方を向かせます。
    /// <see cref="MikoPointer"/> があれば、腕を指さす先へ向けます。どちらも無くてかまいません。
    /// </remarks>
    public sealed class MikoGesture : MonoBehaviour
    {
        static readonly int PointTrigger = Animator.StringToHash("Point");

        [SerializeField] Animator animator;
        [SerializeField] Transform pointTarget;  // 指さす先の既定。PointAt に先を渡さなかったときに使います
        [SerializeField] float pointLookSeconds = 3f;  // 指さしたあと、その方を向いている秒数

        float _lookUntil;
        Transform _aim;


        void Update()
        {
            if (_lookUntil > 0f && Time.time >= _lookUntil)
            {
                _lookUntil = 0f;
                var mikoFloat = GetComponent<MikoFloat>();
                if (mikoFloat != null) mikoFloat.LookAt = null;
            }
        }

        /// <summary>指さす先の方を向いて、指さしをします。</summary>
        /// <param name="target">
        /// 指さす先です。<see langword="null"/> なら、既定の先（<c>Point Target</c>）を使います。
        /// </param>
        public void PointAt(Transform target)
        {
            _aim = target;
            Point();
        }

        // エディタで試すためのものです。Play 中に、コンポーネントの右上のメニュー（⋮）から選びます。
        [ContextMenu("Point")]
        void PointFromEditor() => Point();

        /// <summary>
        /// 既定の先（<c>Point Target</c>）へ指さしをします。
        /// </summary>
        /// <remarks>
        /// 先が無くても、Animator の Point トリガーは送ります。
        /// Animator が空なら、子から探して使います。
        /// </remarks>
        public void Point()
        {
            var target = _aim != null ? _aim : pointTarget;
            _aim = null;
            if (target != null)
            {
                var mikoFloat = GetComponent<MikoFloat>();
                if (mikoFloat != null)
                {
                    mikoFloat.LookAt = target;
                    _lookUntil = Time.time + pointLookSeconds;
                }
                GetComponent<MikoPointer>()?.Begin(target);
            }
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null) animator.SetTrigger(PointTrigger);
        }
    }
}
